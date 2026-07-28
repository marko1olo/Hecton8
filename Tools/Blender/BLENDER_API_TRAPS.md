# Blender API traps in the HECTON-8 asset forge

Symptom first, because the symptom is what you will be searching for. Every entry below was measured on
**Blender 4.5.9 LTS / Python 3.11.11** under the only invocation this pipeline uses:

```bat
blender.exe -b --factory-startup -P <script> -- <args>
```

Verified 2026-07-28 by headless probe against `C:\hades\_tools\blender\blender-4.5.9-windows-x64\blender.exe`.
Where a claim from an earlier note turned out to be wrong on this version, the entry says so — a
confident wrong doc costs more than no doc.

## The dominant failure class in this lane

**An operator that works interactively and silently cancels headless.** This pipeline ONLY ever runs
headless, so anything that depends on operator context, an interactive selection, or the bundled
essentials asset library can return `{'CANCELLED'}`, change nothing, and raise nothing. The output is
then plausible — correct topology, correct budgets, correct attributes — and wrong.

Two habits neutralise it:

1. **Assert on returned counts, not on the operator's return value.** `{'FINISHED'}` is not evidence
   that anything changed; `smooth_polygons == 0` is evidence that nothing did. Every stage that can
   no-op should return the count it achieved and record it to the black box.
2. **Never wrap a `bpy` property assignment in a bare `try/except AttributeError`.** That construct
   converts an API rename into silent wrong output. Prefer `hasattr` plus an explicit failure record so
   a renamed property surfaces as a recorded defect rather than a quietly degraded asset. Trap 2 below
   is exactly this bug — and the first attempt at fixing it reproduced the same failure in a subtler
   form, by assigning a property that exists and does something else. `hasattr` only proves a name
   resolves; it says nothing about what the property does. Measure the output.

---

## 1. Angle-based shading silently does nothing headless

**Symptom.** Assets render faceted with hard creases across smooth surfaces, despite a correct bevel
pass. You start tuning geometry to fix what is actually a shading bug. Visible in
`Docs/AgentLogs/ForgePreviews/Coral_Branching_1712_flat_three_quarter.png`: hard polygonal facets across
the trunk of a mesh whose bevel and subdivision stages both ran.

**Cause, in two layers.**

`Mesh.use_auto_smooth` was REMOVED in Blender 4.1. Measured: `hasattr(bpy.types.Mesh,
'use_auto_smooth')` is `False` and assignment raises `AttributeError: 'Mesh' object has no attribute
'use_auto_smooth'`. Code written against 3.x docs raises; code that guards it with `hasattr` skips
shading entirely and reports success.

The documented 4.1+ replacement is `bpy.ops.object.shade_auto_smooth(angle=...)`, which in the GUI adds
a "Smooth by Angle" modifier. **Measured under `-b --factory-startup`: it returns `{'CANCELLED'}`,
`obj.modifiers` stays `[]`, and every `polygon.use_smooth` remains `False`.** Blender logs
`Warning: Asset loading is unfinished` — the operator needs the bundled essentials asset (the
`smooth_by_angle` geometry-nodes group at `<blender>/4.5/datafiles/assets/geometry_nodes/smooth_by_angle.blend`),
which background Blender does not load. So following the migration guide *correctly* still gets you
nothing, and every asset the pipeline produced before this was flat-shaded. That is not cosmetic: flat
shading destroys the specular response the bevel law in `3dmodel.md` section 4 exists to create, so a
correctly beveled mesh still reads as programmer output.

**Fix — do it at DATA level.** `Tools/Blender/h8forge/mesh_ops.py:269` `apply_shading_basis`:

- `mesh_ops.py:301` mark every polygon `use_smooth = True`;
- `mesh_ops.py:314` mark edges whose `edge.calc_face_angle()` exceeds the threshold as
  `edge.smooth = False`. That IS what "Smooth by Angle" does, expressed as mesh data rather than a
  modifier. Boundary edges are skipped: no dihedral angle, and creasing them would seam an intentional
  open shell rim;
- `mesh_ops.py:328` apply `WEIGHTED_NORMAL` in `mode='FACE_AREA_WITH_ANGLE'` with `keep_sharp`, which is
  precisely the bible's `normalize(sum(faceNormal * faceArea * cornerAngleWeight))`.

It returns `ShadingResult(smooth_polygons, sharp_edges, weighted_applied)` (`mesh_ops.py:262`) and
records `failure_code="SHADING_NOT_APPLIED"` when `smooth_polygons` is zero, so a regression cannot hide.
Data-level is also deterministic and inspectable, and does not depend on operator context at all.

**Also measured, for the record:** `bpy.ops.object.shade_smooth_by_angle(angle=...)` DOES work headless
(`{'FINISHED'}`, all polygons smooth, writes the `sharp_edge` attribute). It is a valid alternative, but
it is still an operator and still subject to trap 6, so the data-level route is the one in use.

## 2. `scene.render.bake.distance` does not exist — and `max_ray_distance` is not its replacement

**Symptom.** Baked AO has no local cavity contrast. Crevices, undersides, root clusters and branch
intersections do not darken, and the AO mean collapses — one coral measured 0.078. The B channel exists,
is populated, and passes a presence check while carrying almost no information.

**Cause.** `scene.render.bake` has no `distance` attribute on 4.5. The measured writable set is
`cage_extrusion, cage_object, filepath, height, margin, margin_type, max_ray_distance, normal_b,
normal_g, normal_r, normal_space, save_mode, target, use_automatic_name, use_cage, use_clear,
use_pass_*, use_selected_to_active, use_split_materials, view_from, width`. The original code wrapped
the assignment in `try/except AttributeError`, so the AO bake ran with rays bounded only by the world
default — every branch occluding every other branch across the whole colony.

**The obvious fix is also wrong.** `max_ray_distance` exists, so `hasattr` passes and the assignment
succeeds — but its RNA description is *"The maximum ray distance for matching points between the active
and selected objects. If zero, there is no limit."* It is the selected-to-active cage-matching distance,
and this pipeline sets `use_selected_to_active = False`. Measured on a torus, 24 Cycles samples:

| setting | AO min | AO max | AO mean |
|---|---|---|---|
| `bake.max_ray_distance = 0.0` (unbounded) | 0.0000 | 1.0000 | **0.3170** |
| `bake.max_ray_distance = 0.30` | 0.0000 | 1.0000 | **0.3170** |
| `bake.max_ray_distance = 0.05` | 0.0000 | 1.0000 | **0.3170** |
| `world.light_settings.distance = 0.30` | 0.0104 | 1.0000 | **0.8740** |
| `world.light_settings.distance = 0.05` | 0.5833 | 1.0000 | **0.9778** |

`max_ray_distance` has **no effect whatsoever** on the AO bake — the three statistics are identical. The
knob that actually bounds AO ray length is **`scene.world.light_settings.distance`** (RNA: *"Distance of
object that contribute to the ambient occlusion effect"*, default 10.0 m), which moves the mean
measurably.

`max_ray_distance` is the more dangerous of the two wrong answers. A missing attribute at least raises;
this one exists, so `hasattr` passes, the assignment succeeds, and the `AO_DISTANCE_UNSUPPORTED` record
can never fire. It reads as a fix.

**Fix.** `Tools/Blender/h8forge/vertexcolor.py:217` sets `world.light_settings.distance` inside
`bake_ambient_occlusion` (`vertexcolor.py:148`), creating a world if the scene has none, and records
`AO_DISTANCE_UNSUPPORTED` at `vertexcolor.py:221` only when the property is genuinely absent. The
`distance` argument is live again (`coral_branching.py:521` passes `0.22`).

Residual: the previous `light_settings.distance` is **not** restored, although the same function
carefully restores `render.engine`, `cycles.samples` and the active object. The modified world AO
distance leaks into the rest of the session, including any `preview` render that uses that world.

This entry is the canonical example of the general principle at the top of this file: a rename swallowed
by an exception handler, then "fixed" by assigning a property that exists but does something else. The
only thing that caught either mistake was measuring the output.

## 3. Vertex colours missing from the exported FBX

**Symptom.** A correctly authored colour set is absent from the FBX, or the exporter picks a different
layer than the one you wrote.

**Cause.** `mesh.color_attributes.default_color` does not exist (measured: `hasattr(...)` is `False`).
Blender 4.x splits two different concepts:

- **active for editing** — `mesh.color_attributes.active_color`, an attribute OBJECT reference;
- **used for render/export** — `mesh.attributes.active_color_name` and
  `mesh.attributes.default_color_name`, NAME strings.

Setting only the first leaves the exporter reading whichever layer happens to be default.

**Fix.** `Tools/Blender/h8forge/vertexcolor.py:73-75` sets all three in `ensure_color_attribute`. The
four channels are PACKED into ONE attribute named by `law.VCOL_ATTRIBUTE_NAME` (`"Col"`), not four named
layers — a validator that derives layer names from the channel-role tuples rejects every asset the forge
produces, which is exactly what happened before that constant existed.

`BYTE_COLOR`/`CORNER` is not cosmetic either: it matches the bible's `Color | UNorm8 x4` stream and is
what Unity's FBX importer consumes as `Mesh.colors32`. `FLOAT_COLOR`/`POINT` survives Blender and
changes the exported layout.

## 4. The AO bake overwrites all four channels

**Symptom.** A sway or edge-wear gradient vanishes with no error. The attribute exists, the validator
sees data, the shader animates the whole organism as a rigid body. Invisible in any lit render.

**Cause, measured.** `bpy.ops.object.bake(type="AO", target="VERTEX_COLORS", use_clear=True)` writes ALL
FOUR channels of the target attribute. Starting from `(1.0, 0.5, 0.25, 0.10)` on a torus, after the bake:
R/G/B all carry the AO value in 0..1 and **A is forced to 1.0**. Nothing of the original survives.

Two preconditions, both measured:

- **Cycles is required.** Under EEVEE Next the operator raises
  `RuntimeError: Error: Current render engine does not support baking`.
- **A material slot is NOT required.** An earlier note said the bake refuses without one; measured, a
  torus with no material baked fine (`{'FINISHED'}`, 38 distinct AO values). The guard at
  `vertexcolor.py` that early-returns `AO_NO_MATERIAL` is therefore conservative rather than necessary —
  it refuses a bake Blender would have completed. It is still useful as a stage-order check (reaching
  the bake before materials are assigned means B silently defaults to 1.0, fully unoccluded), but the
  stated reason is wrong.

**Fix.** Bake into a SCRATCH attribute first, then compose into channel B alongside the analytically
authored R/G/A. `vertexcolor.py:41` `_SCRATCH_AO_ATTRIBUTE = "H8_AO_Scratch"`, read back per-vertex,
stashed on the object and consumed by `consume_baked_ao` (`vertexcolor.py:265`), composed by
`write_organic_channels` (`vertexcolor.py:383`), scratch dropped before export. Baking LAST erases the
gradient. `AoBakeResult.has_contrast` guards the other direction: occlusion that never varies is not
occlusion, it is a bake that failed while returning success.

## 5. Convex hull silently inflates the triangle count

**Symptom.** A collider reports more triangles than its hull should have, with no error and no leftover
geometry to explain it.

**Cause.** `bmesh.ops.convex_hull` ADDS hull faces without removing the source geometry. Its
`geom_interior` / `geom_unused` reports only cover input the hull did not consume, so for an
ALREADY-CONVEX input every vertex is on the hull and **both lists come back EMPTY** — nothing to delete,
and the original faces survive under the new ones.

**Measured, and this corrects an earlier claim of "exactly double":** the inflation is
input-dependent, because bmesh will not create a duplicate face on the same vertex triple.

| input (triangulated, convex) | tris in | tris after hull | ratio | `geom_interior` | `geom_unused` |
|---|---|---|---|---|---|
| cube | 12 | 18 | 1.50x | 0 | 0 |
| icosphere, 3 subdivisions | 320 | 320 | 1.00x | 0 | 0 |

The cube inflates because its triangulation splits each quad on one diagonal while the hull picks the
other for some faces; the icosphere does not inflate at all because every hull triangle coincides with
an existing one. So the magnitude is unpredictable and can be zero — do not rely on a factor, and do not
conclude the trap is absent because one test case came back clean.

**Fix.** `Tools/Blender/h8forge/mesh_ops.py:685` `_convex_hull_in_place` strips faces then edges down to
a bare point cloud before hulling, which makes the result unconditional: the hull of the vertices and
nothing else. Measured on the cube: 12 triangles, exactly the hull.

Related, in `make_convex_collider` (`mesh_ops.py:720`): decimation breaks convexity, because collapsing
a hull edge can pull a vertex inside the shell. PhysX needs the proxy convex, so it re-hulls after
reduction and judges the budget only afterwards.

## 6. Operators read the SELECTION, not the active pointer

**Symptom.** An operator returns `{'CANCELLED'}`, raises nothing, and nothing changes.

**Measured on 4.5.9, with the object ACTIVE but NOT selected:**

| call | result |
|---|---|
| `bpy.ops.object.shade_smooth()` | `{'CANCELLED'}`, no polygon changed |
| `bpy.ops.object.shade_smooth_by_angle(angle=...)` | `{'CANCELLED'}`, no polygon changed |
| `bpy.ops.object.modifier_apply(modifier=...)` | `{'FINISHED'}`, triangles 320 -> 64 |

So the trap is real for the `shade_*` family and **does not apply to `modifier_apply`**, which follows
the active object only. An earlier note attributed the CANCELLED-decimation symptom to
`modifier_apply` needing selection; that is wrong on this version.

**Fix.** `Tools/Blender/h8forge/mesh_ops.py:37` `_make_sole_active` deselects everything else, selects
the target and makes it active. Cheap, and correct for every operator regardless of which convention it
follows. `bpy.ops.object.mode_set` / `bpy.ops.uv.smart_project` also route through the active object, so
generators call it before entering edit mode (`coral_branching.py:485`).

## 7. `modifier_apply` does not rebind `obj.data` — but it does raise on shared data

**Symptom claimed earlier.** "A decimation loop reports `{'FINISHED'}` eight times while the triangle
count never moves, because `modifier_apply` rebinds `obj.data` and your captured mesh reference points
at pre-modifier geometry."

**Measured: that mechanism does not exist on 4.5.9.** With single-user mesh data, `modifier_apply`
mutates the existing datablock in place — captured pointer `1789353742088` before and after, and the
captured reference reports the NEW triangle count (1280 -> 320) exactly as `obj.data` does. Re-reading
`obj.data` every pass is harmless and remains the style used here, but it is defensive rather than
load-bearing, and the stated cause is fiction. If you see a decimation loop that reports success without
reducing, look at trap 6 (`shade_*` selection), trap 10 (topology floor), or the ratio arithmetic — not
at datablock rebinding.

**The real hazard in this area.** With **multi-user** mesh data the operator raises
`RuntimeError: Error: Modifiers cannot be applied to multi-user data`. So never share one mesh datablock
between two objects mid-pipeline. `build_lod_chain` avoids it explicitly by copying:
`clone.data = lod0.data.copy()` (`mesh_ops.py`, inside `build_lod_chain` at `mesh_ops.py:533`).

## 8. `view_layer.material_override` is ignored by EEVEE Next

**Symptom.** Four different vertex-colour channel renders come back byte-identical. That reads as "the
channel is flat", when in truth no channel was ever rendered — every tile shows the object's ORIGINAL
material.

**Measured** on an emissive-red cube with an emissive-green override, 64x64, `film_transparent`:

| engine | no override | with override | honoured |
|---|---|---|---|
| `BLENDER_EEVEE_NEXT` | `(1.0, 0.0, 0.0, 1.0)` | `(1.0, 0.0, 0.0, 1.0)` | **no** |
| `CYCLES` | `(0.98, 0.0, 0.0, 1.0)` | `(0.0, 1.0, 0.0, 1.0)` | yes |
| `BLENDER_EEVEE_NEXT`, slot swap | — | `(0.0, 1.0, 0.0, 1.0)` | yes |

**Fix.** Swap the material slots and restore them in a `finally` —
`Tools/Blender/h8forge/preview.py:405` `_apply_override_material` /
`preview.py:435` `_restore_materials`. Slot swapping works in every engine. The restore is mandatory:
the pipeline bible forbids leaving preview materials attached to a generated asset.

(For the record, EEVEE Next itself renders correctly headless — measured exact `(1,0,0,1)` for an
emissive red subject with a working alpha mask. The `gpu` module is unavailable in background mode, but
that does not block offline rendering.)

## 9. An 8-bit PNG is display-encoded; masking by luminance measures the background

**Symptom.** A measured channel value is far above the bible threshold, or four channels report
identical plausible numbers.

**Measured.** An emissive surface at linear 0.045, rendered through the `Standard` view transform to an
8-bit PNG, reads back as **0.2353** — 5.23x high and non-linearly so. Applying the sRGB inverse recovers
0.04519. Every threshold in the 3D bibles is 0..1 LINEAR, so comparing the raw readback is wrong by a
large factor.

The same number is why luminance masking fails: the preview backdrop is linear 0.045
(`PreviewSpec.background`), which encodes to 0.235 — above any sane luminance threshold. Mask by
luminance and the statistics describe the backdrop, which is how four channels report byte-identical
numbers and still look reasonable.

**Fix.** `Tools/Blender/h8forge/preview.py:706` `_srgb_to_linear` undoes the encoding;
`preview.py:721` `measure_channel_png` masks by ALPHA, which requires the tile to have been rendered
with `film_transparent` — set at `preview.py:647` in `render_channel_sheet`. `ChannelStats.subject_visible`
rejects both an empty frame (coverage below 0.0005) and a mask that caught the backdrop (above 0.98).

The channel-tile render also hides the preview rig, because the emissive scale grid would otherwise
paint saturated 1.0 pixels into the frame and every channel would report `max=1.0`.

Related composite trap, found by looking at the image rather than the code: channel tiles are rendered
with a transparent film, so copying them into the sheet wholesale carries `alpha=0` and viewers paint
the background white — at which point a legitimately uniform 1.0 channel is white geometry on a white
field, indistinguishable from an empty frame. `preview._composite` alpha-composites over the dark
backdrop instead. The numbers were already correct while the image was unreadable.

## 10. `mathutils.noise` has a process-global seed

**Symptom.** Two generators in one Blender session perturb each other, and neither is reproducible from
its own seed — which breaks the determinism `PROCEDURAL_ASSET_PIPELINE.md` requires.

**Measured.** `mathutils.noise.seed_set` is module-level process state. Seeded with 1, the stream is
`0.417022, 0.997185, 0.720325` and replays identically in isolation. Interleave a second consumer that
calls `seed_set(99)` between draws and the first stream becomes `0.417022, 0.722540` — silently
different. `numpy.random.default_rng(1)` is immune: `0.511822, 0.950464` both isolated and interleaved.

**Fix.** `numpy.random.default_rng(seed)` for sequential draws (`coral_branching.py:155`), or a
positional hash for fields that must be sampled out of order (`coral_branching.py:437` `_value_noise`,
trigonometric hashing of object-space position plus seed).

## 11. Preview sheets deleted by the run that follows them

**Symptom.** A sheet you know was rendered is not on disk.

**Cause.** `AGENTS.md` `Atomic File Delete Rule` requires physically deleting stale `.png`/`.log` before
a render run, and `preview.clear_render_dir` originally decided staleness by asset-name PREFIX. A
generator renders a studio sheet, then a flat sheet, then a channel sheet, all under one asset name — so
the second call deleted the first call's output. The file was created, then removed by the very rule
meant to protect it, with no error anywhere.

**Evidence, from the run that exposed it.** `Docs/AgentLogs/ForgePreviews/` holds
`Coral_Branching_1712_SHEET_flat.png`, the four flat view tiles and the four channel tiles — and **no**
`Coral_Branching_1712_SHEET_studio.png` and no `*_studio_*` tile, although the generator renders studio
first (`coral_branching.py:635`). The channel pass survived only because it cleared the narrower prefix
`<name>_chan`.

**Fix.** `Tools/Blender/h8forge/preview.py:112` `clear_render_dir` now decides staleness by MTIME against
`_PROCESS_START` (`preview.py:109`, wall clock captured at import): a file written after this process
started belongs to this run and is kept (`preview.py:141`). Prefix matching still narrows the scan, but it
is no longer what decides deletion. The wall-clock read is cleanup-only and never touches geometry, so it
does not weaken the determinism contract in `PROCEDURAL_ASSET_PIPELINE.md`, which governs mesh topology.

---

## Project design rules, and why a naive simplification breaks them

These are not API traps. They are decisions a future agent might "simplify" and thereby break.

**LOD targets derive from the previous level's REAL count, not from the level's own budget.**
`mesh_ops.py:533` `build_lod_chain`. Targeting each budget independently yields a NON-MONOTONIC chain
whenever LOD0 already sits far under budget: LOD1 gets aggressively reduced, LOD2 sees itself already
under its looser budget and skips decimation entirely, and the far LOD ships heavier than the near one
(observed 300/200/300). `3dmodel.md` section 7 requires LOD1 to be a reduction of LOD0 and LOD2 a
reduction of LOD1, so the chain is relative by construction.

**Decimation is preceded by splitting UV seams, sharp edges and material borders.**
`mesh_ops.py:499` `_split_uv_seams`. Blender's Decimate/COLLAPSE has **no seam-preservation flag** but
DOES preserve mesh BOUNDARIES, so converting seams into boundaries is the mechanism that makes
`3dmodel.md` section 7's preservation requirement actually hold instead of being asserted in a comment.
The duplicated seam vertices are not waste — any UV-seamed mesh duplicates them on export anyway.

**Seam splitting costs a triangle FLOOR, and the escape is legal only at the coarsest LOD.**
Because COLLAPSE will not collapse a boundary edge, a many-island unwrap can have a floor above the
budget: observed coral LOD2 stuck at 584 triangles against a 300 ceiling no matter how many passes ran.
`build_lod_chain` therefore rebuilds that level from LOD0 WITHOUT seam splitting and decimates again
(`mesh_ops.py:634`), and RECORDS `seams_dropped` in the warning (`mesh_ops.py:657`). Two reasons this is
legitimate rather than a fudge: retrying with the same constraints would be the same-failure escalation
`AGENTS.md` forbids, while changing the constraint is the strategy change it demands; and
`3DMODEL_FLORA_CORAL.md` section 6 describes LOD2 as "preserve mass and root/anchor shape" and
explicitly permits "simplified shells or cards", so UV precision is secondary at that level only. The
compromise is logged, never silent.

**`topology_report` exists so a missed budget reports a CAUSE.** `mesh_ops.py:405`, returning
`TopologyReport` (`mesh_ops.py:371`) with component count, boundary edges, non-manifold edges and an
estimated `irreducible_floor` (`mesh_ops.py:383`). Decimate collapses edges; it cannot delete a whole
shell. So an object made of many small disconnected pieces has a triangle floor no number of passes will
beat. "584 tris vs a 300 budget" teaches an author nothing; "76 disconnected components" tells them to
weld the tip clusters into the parent branch or use an impostor at that level.

**Sway amplitude uses GEODESIC distance along the skeleton, not Euclidean from the anchor.**
`vertexcolor.py:360` takes caller-supplied distances; `coral_branching.py` samples them from the skeleton
KD-tree. A frond arcing back over its own base is far along the stem but physically near it, and
Euclidean distance marks that tip rigid.

**Sway uniformity is judged RELATIVE to the permitted band.** `vertexcolor.py:303` `relative_spread`,
`vertexcolor.py:317` `is_uniform`. `3DMODEL_FLORA_CORAL.md` section 2 caps rigid mineralised coral at
32/255, so a correct mineralised coral has an absolute spread of at most 0.125 and an absolute threshold
flags every compliant rigid asset as broken. An earlier version did exactly that and rejected a coral
whose gradient was visibly fine in the channel render.

## Known gaps — recorded rather than hidden

A doc that records what is still broken is worth more than one that implies completeness.

- **The AO bake does not restore `world.light_settings.distance`.** Trap 2. The bake now bounds its rays
  correctly, but leaves the world's AO distance changed for the rest of the session.
- **Coral LOD2 lands at 584 triangles against a 300 budget**, and the seam-drop retry did not fire.
  Measured cause: `bpy.ops.uv.smart_project` sets **no** `edge.use_seam` flags at all — probed on Suzanne,
  0 seams of 1005 edges, 0 sharp, and `_split_uv_seams` split 0 edges. With `seams_split == 0` the
  `final_tris > budget and seams_split > 0` condition at `mesh_ops.py:634` cannot be true, so the escape
  hatch was unreachable on any `smart_project`-unwrapped mesh.

  **Nuance that changes the picture and needs re-measuring.** `_split_uv_seams` (`mesh_ops.py:499`) targets
  `edge.seam or not edge.smooth`. The 584 observation was taken while `apply_shading_basis` was still the
  CANCELLED-operator no-op, so every edge was smooth and BOTH halves of that condition were dead. Now that
  trap 1 is fixed and shading marks `edge.smooth = False` above the dihedral threshold, the sharp-edge half
  has real input, `seams_split` should be non-zero, and the retry becomes reachable. Do not assume either
  the 584 figure or the "retry never fires" conclusion still holds — re-run the coral and read the black box.

  Independent of the retry, the underlying floor is topological: roughly 76 disconnected tip-cluster
  shells, which Decimate cannot remove because it collapses edges and cannot delete a shell. The bible's
  answer at LOD2 is an impostor or card, not more decimation. `topology_report` exists to say so.
- **`topology_report` has no callers.** Verified: `grep -rn "topology_report" --include=*.py` over
  `Tools/Blender/` returns only its own definition. Until a budget miss actually invokes it, the "black
  box records a cause" claim in its docstring is unrealised.
- **`h8forge/__init__.py` module map lists `export_unity` in a package that imports only `law` eagerly.**
  That is deliberate (`law` is `bpy`-free so plain CPython tooling can import it), but the map is
  documentation, not the import surface — callers must import the `bpy`-dependent modules themselves.

## Environment facts worth not re-deriving

| | |
|---|---|
| `bpy.app.version_string` | `4.5.9 LTS` (hash `8bf95cbd38d1`) |
| Python | `3.11.11 [MSC v.1929 64 bit (AMD64)]` |
| `numpy` | `1.26.4`, bundled |
| `PIL` / Pillow | **absent** — `ModuleNotFoundError`. Composite and measure with numpy. |
| `gpu` module | unavailable in background mode (`GPU functions for drawing are not available`), which does NOT prevent EEVEE Next or Cycles offline rendering |
| essentials assets | `<blender>/4.5/datafiles/assets/geometry_nodes/smooth_by_angle.blend` — not loaded under `-b`, which is the root of trap 1 |
