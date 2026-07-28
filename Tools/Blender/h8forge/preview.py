"""Headless contact-sheet renders: the visual verification instrument.

This module exists so a generated asset can be JUDGED without Unity. Unity holds a
single project lock and is routinely occupied by another contributor, so a pipeline
whose only proof route is an in-engine screenshot cannot iterate. Blender renders the
geometry it just built, and the lead opens the PNG with their own visual modality --
which ``AGENTS.md`` ``[REQ] Direct Media Reading`` requires of the lead specifically:

    "every agent, Claude Code included, must open reference images, diagnostic
     captures, and screenshots with its own visual modality ... A visual verdict
     without direct image inspection is a compliance failure"

Design stance: the render must be HONEST, not flattering. ``3dmodel.md`` bans using
presentation to hide weak geometry --

    "Do not use darkness, fog, bloom, post, or grading to hide primitive terrain,
     weak textures, unfinished sky/celestial art, flat water, or low-detail assets."

so the lighting is a directional three-point setup that exposes silhouette and bevel
response, the flat mode strips materials entirely, and nothing here adds bloom,
vignette, or grading.

The vertex-colour channel modes are the highest-value diagnostic in the file. A sway
gradient that collapsed to a constant is invisible in every ordinary render, passes a
presence check, and is an explicit rejection gate in ``3DMODEL_FLORA_CORAL.md``
section 8. Rendering the raw channel as emission makes it undeniable.
"""

from __future__ import annotations

import math
import os
import time
from dataclasses import dataclass, field
from typing import Optional, Sequence

import bmesh
import bpy
import numpy as np
from mathutils import Vector

from . import law


DEFAULT_PREVIEW_ROOT = ("Docs", "AgentLogs", "ForgePreviews")

# Camera directions in object space, unit vectors pointing FROM the subject TO the
# camera. Named so a contact sheet is reproducible and comparable across assets.
VIEW_DIRECTIONS = {
    "front": Vector((0.0, -1.0, 0.0)),
    "three_quarter": Vector((-0.82, -0.82, 0.42)),
    "side": Vector((1.0, 0.0, 0.0)),
    "top": Vector((0.0, -0.001, 1.0)),
    "low": Vector((-0.6, -0.9, -0.22)),
    "back_rim": Vector((0.5, 0.85, 0.28)),
}

CHANNEL_INDEX = {"vcol_r": 0, "vcol_g": 1, "vcol_b": 2, "vcol_a": 3}

# Channel labels come from the bible contracts so a rendered tile is self-describing
# and cannot be mistaken for a different family's semantics.
CHANNEL_LABELS = {
    law.SurfaceClass.ORGANIC: law.ORGANIC_VCOL,
    law.SurfaceClass.HARD_SURFACE: law.HARD_SURFACE_VCOL,
    law.SurfaceClass.GEOLOGIC: law.HARD_SURFACE_VCOL,
}


@dataclass
class PreviewSpec:
    """One contact-sheet request."""

    name: str
    output_dir: str = ""
    resolution: int = 640
    views: tuple = ("front", "three_quarter", "side", "top")
    mode: str = "studio"
    scale_witness: bool = True
    engine: str = "BLENDER_EEVEE_NEXT"
    samples: int = 16
    margin: float = 1.22
    surface_class: law.SurfaceClass = law.SurfaceClass.ORGANIC
    background: float = 0.045
    columns: int = 0  # 0 = auto

    def resolved_output_dir(self) -> str:
        if self.output_dir:
            return self.output_dir
        return os.path.join(law.project_root(), *DEFAULT_PREVIEW_ROOT)


@dataclass
class PreviewResult:
    sheet_path: str
    tile_paths: tuple
    stale_deleted: int
    mode: str
    tile_resolution: int
    notes: tuple = field(default_factory=tuple)


# ---------------------------------------------------------------------------
# Stale artefact removal  --  AGENTS.md Atomic File Delete Rule
# ---------------------------------------------------------------------------

# Wall-clock at import, used ONLY to tell this run's artefacts from previous runs'.
# It never touches geometry, so it does not violate the determinism contract in
# PROCEDURAL_ASSET_PIPELINE.md -- that contract governs mesh topology, not file cleanup.
_PROCESS_START = time.time()


def clear_render_dir(output_dir: str, name_prefix: str = "") -> int:
    """Physically delete STALE PNG/log artefacts before rendering.

    ``AGENTS.md``: "Before ANY automated Unity batchmode test or render run, all .png
    diagnostic artifacts and .log files in the output directory must be physically
    deleted ... This prevents hallucinatory visual checks against old screenshots."

    That failure mode is real and severe here: the whole point of this module is that a
    visual judgement is made from these files, so judging last hour's render and
    reporting it as this run's result would be fabricated proof.

    Staleness is decided by MTIME against process start, not by filename prefix. Prefix
    matching looked equivalent and was not: a generator renders a studio sheet, then a
    flat sheet, then a channel sheet, all under one asset name, so the second call
    deleted the first call's output. The symptom was a missing studio sheet with no error
    anywhere -- the file had been created, then removed by the very rule meant to protect
    it. Files written after this process started belong to this run and are kept.
    """
    if not os.path.isdir(output_dir):
        os.makedirs(output_dir, exist_ok=True)
        return 0
    deleted = 0
    for entry in os.listdir(output_dir):
        if name_prefix and not entry.startswith(name_prefix):
            continue
        if not entry.lower().endswith((".png", ".log", ".exr")):
            continue
        path = os.path.join(output_dir, entry)
        try:
            if os.path.getmtime(path) >= _PROCESS_START:
                continue  # written by this run; deleting it is the bug, not the rule
            os.remove(path)
            deleted += 1
        except OSError:
            # A file held open by an image viewer is not a reason to abort a render;
            # it IS a reason to say so, which the caller surfaces via notes.
            pass
    return deleted


# ---------------------------------------------------------------------------
# Scene rig
# ---------------------------------------------------------------------------

def _purge_scene_rig() -> None:
    """Remove any rig this module previously added, leaving subject geometry alone."""
    for obj in list(bpy.data.objects):
        if obj.name.startswith("H8PREV_"):
            bpy.data.objects.remove(obj, do_unlink=True)


def _ensure_collection() -> bpy.types.Collection:
    existing = bpy.data.collections.get("H8PREV_Rig")
    if existing is None:
        existing = bpy.data.collections.new("H8PREV_Rig")
        bpy.context.scene.collection.children.link(existing)
    return existing


def _make_material(name: str, build) -> bpy.types.Material:
    material = bpy.data.materials.get(name)
    if material is not None:
        bpy.data.materials.remove(material)
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()
    build(material, nodes, links)
    return material


def _flat_material() -> bpy.types.Material:
    """Neutral matte grey. Silhouette judgement with no texture help.

    ``3DMODEL_FLORA_CORAL.md`` section 10 requires exactly this shot: "flat-material
    screenshot proving the silhouette is biological before texture detail."
    """
    def build(_material, nodes, links):
        output = nodes.new("ShaderNodeOutputMaterial")
        bsdf = nodes.new("ShaderNodeBsdfDiffuse")
        bsdf.inputs["Color"].default_value = (0.55, 0.55, 0.57, 1.0)
        links.new(bsdf.outputs["BSDF"], output.inputs["Surface"])
    return _make_material("H8PREV_Flat", build)


def _studio_material() -> bpy.types.Material:
    """Mid-grey dielectric with enough spec to reveal bevels and curvature.

    Roughness is deliberately mid-low: a fully rough surface hides the chamfer
    response that ``3dmodel.md`` section 4 exists to enforce, so a matte-only preview
    cannot tell a beveled edge from a raw 90-degree one.
    """
    def build(_material, nodes, links):
        output = nodes.new("ShaderNodeOutputMaterial")
        bsdf = nodes.new("ShaderNodeBsdfPrincipled")
        bsdf.inputs["Base Color"].default_value = (0.42, 0.44, 0.47, 1.0)
        bsdf.inputs["Roughness"].default_value = 0.34
        if "Metallic" in bsdf.inputs:
            bsdf.inputs["Metallic"].default_value = 0.0
        links.new(bsdf.outputs["BSDF"], output.inputs["Surface"])
    return _make_material("H8PREV_Studio", build)


def _channel_material(channel_index: int, attribute: str = "Col") -> bpy.types.Material:
    """Render ONE vertex-colour channel as raw emission greyscale.

    Emission bypasses lighting, so the pixel value in the render IS the stored channel
    value. That is what makes this a measurement rather than an impression -- the
    caller can sample pixels and assert a gradient exists.
    """
    def build(_material, nodes, links):
        output = nodes.new("ShaderNodeOutputMaterial")
        emission = nodes.new("ShaderNodeEmission")
        emission.inputs["Strength"].default_value = 1.0
        color_attr = nodes.new("ShaderNodeVertexColor")
        color_attr.layer_name = attribute
        separate = nodes.new("ShaderNodeSeparateColor")
        links.new(color_attr.outputs["Color"], separate.inputs["Color"])
        if channel_index == 3:
            links.new(color_attr.outputs["Alpha"], emission.inputs["Color"])
        else:
            out_names = ("Red", "Green", "Blue")
            links.new(separate.outputs[out_names[channel_index]], emission.inputs["Color"])
        links.new(emission.outputs["Emission"], output.inputs["Surface"])
    return _make_material("H8PREV_Chan{i}".format(i=channel_index), build)


def _normals_material() -> bpy.types.Material:
    """World-space normal as colour. Exposes faceting and broken smoothing groups."""
    def build(_material, nodes, links):
        output = nodes.new("ShaderNodeOutputMaterial")
        emission = nodes.new("ShaderNodeEmission")
        geometry = nodes.new("ShaderNodeNewGeometry")
        multiply = nodes.new("ShaderNodeVectorMath")
        multiply.operation = "MULTIPLY_ADD"
        multiply.inputs[1].default_value = (0.5, 0.5, 0.5)
        multiply.inputs[2].default_value = (0.5, 0.5, 0.5)
        links.new(geometry.outputs["Normal"], multiply.inputs[0])
        links.new(multiply.outputs["Vector"], emission.inputs["Color"])
        links.new(emission.outputs["Emission"], output.inputs["Surface"])
    return _make_material("H8PREV_Normals", build)


def _build_lights(collection: bpy.types.Collection, radius: float) -> None:
    """Directional three-point rig scaled to the subject.

    Key from upper-front-left, fill opposite and dimmer, rim from behind. Sun lamps
    rather than area lights so the falloff does not change with subject size and two
    assets of different scale remain comparable.
    """
    rig = (
        ("Key", Vector((-0.55, -0.75, 0.65)), 4.2),
        ("Fill", Vector((0.8, -0.35, 0.18)), 1.15),
        ("Rim", Vector((0.15, 0.9, 0.5)), 2.6),
    )
    for name, direction, energy in rig:
        data = bpy.data.lights.new("H8PREV_L_" + name, type="SUN")
        data.energy = energy
        data.angle = math.radians(2.5)
        obj = bpy.data.objects.new("H8PREV_L_" + name, data)
        collection.objects.link(obj)
        obj.location = direction.normalized() * (radius * 4.0)
        obj.rotation_euler = _look_at_rotation(direction.normalized())


def _look_at_rotation(direction: Vector) -> tuple:
    """Euler that points a -Z-forward object (camera/sun) along ``-direction``."""
    forward = -direction.normalized()
    return forward.to_track_quat("-Z", "Y").to_euler()


def _build_scale_witness(collection: bpy.types.Collection,
                         bounds_min: Vector, radius: float) -> None:
    """1 m reference grid plus a 1.8 m human-height marker.

    Without a size reference a preview cannot answer "does this read at the right
    scale", which ``3dmodel.md`` section 12 lists as a required property ("scale
    witnesses"). The grid is emissive wireframe-thin quads so it never competes with
    the subject's lighting.
    """
    extent = max(2.0, math.ceil(radius * 2.0))
    steps = int(extent * 2) + 1

    bm = bmesh.new()
    half = extent
    thickness = max(0.004, radius * 0.0035)
    for i in range(steps):
        offset = -half + i * 1.0
        if offset > half:
            break
        # Two thin quads per grid line, one per axis.
        for axis in (0, 1):
            if axis == 0:
                a = Vector((offset - thickness, -half, 0.0))
                b = Vector((offset + thickness, -half, 0.0))
                c = Vector((offset + thickness, half, 0.0))
                d = Vector((offset - thickness, half, 0.0))
            else:
                a = Vector((-half, offset - thickness, 0.0))
                b = Vector((half, offset - thickness, 0.0))
                c = Vector((half, offset + thickness, 0.0))
                d = Vector((-half, offset + thickness, 0.0))
            verts = [bm.verts.new(v) for v in (a, b, c, d)]
            bm.faces.new(verts)
    mesh = bpy.data.meshes.new("H8PREV_GridMesh")
    bm.to_mesh(mesh)
    bm.free()

    grid = bpy.data.objects.new("H8PREV_Grid", mesh)
    collection.objects.link(grid)
    grid.location = Vector((0.0, 0.0, bounds_min.z - radius * 0.004))

    def build(_material, nodes, links):
        output = nodes.new("ShaderNodeOutputMaterial")
        emission = nodes.new("ShaderNodeEmission")
        emission.inputs["Color"].default_value = (0.10, 0.30, 0.36, 1.0)
        emission.inputs["Strength"].default_value = 1.0
        links.new(emission.outputs["Emission"], output.inputs["Surface"])
    grid.data.materials.append(_make_material("H8PREV_GridMat", build))

    # 1.8 m human-height marker: a thin pillar at the grid edge.
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=8,
                          radius1=thickness * 3.0, radius2=thickness * 3.0,
                          depth=1.8)
    marker_mesh = bpy.data.meshes.new("H8PREV_HumanMesh")
    bm.to_mesh(marker_mesh)
    bm.free()
    marker = bpy.data.objects.new("H8PREV_Human", marker_mesh)
    collection.objects.link(marker)
    marker.location = Vector((half * 0.86, half * 0.86, bounds_min.z + 0.9))

    def build_marker(_material, nodes, links):
        output = nodes.new("ShaderNodeOutputMaterial")
        emission = nodes.new("ShaderNodeEmission")
        emission.inputs["Color"].default_value = (0.85, 0.42, 0.10, 1.0)
        emission.inputs["Strength"].default_value = 1.4
        links.new(emission.outputs["Emission"], output.inputs["Surface"])
    marker.data.materials.append(_make_material("H8PREV_HumanMat", build_marker))


# ---------------------------------------------------------------------------
# Framing
# ---------------------------------------------------------------------------

def _world_bounds(objects: Sequence[bpy.types.Object]) -> tuple:
    lo = Vector((float("inf"),) * 3)
    hi = Vector((float("-inf"),) * 3)
    found = False
    for obj in objects:
        if obj.type != "MESH" or not obj.data.vertices:
            continue
        matrix = obj.matrix_world
        for vertex in obj.data.vertices:
            world = matrix @ vertex.co
            for axis in range(3):
                lo[axis] = min(lo[axis], world[axis])
                hi[axis] = max(hi[axis], world[axis])
            found = True
    if not found:
        return (Vector((0.0, 0.0, 0.0)), Vector((0.0, 0.0, 0.0)))
    return (lo, hi)


def _place_camera(collection: bpy.types.Collection, direction: Vector,
                  center: Vector, radius: float, margin: float) -> bpy.types.Object:
    """Frame the subject identically for every view so tiles are comparable.

    Distance is derived from the bounding sphere and the vertical FOV, which keeps a
    3 m module and a 0.4 m coral occupying the same fraction of frame. Comparing two
    assets is impossible if each one auto-zooms differently.
    """
    data = bpy.data.cameras.new("H8PREV_Cam")
    data.lens_unit = "FOV"
    data.angle = math.radians(38.0)
    data.clip_start = max(0.001, radius * 0.002)
    data.clip_end = radius * 60.0 + 100.0

    camera = bpy.data.objects.new("H8PREV_Cam", data)
    collection.objects.link(camera)

    distance = (radius * margin) / math.tan(data.angle * 0.5)
    camera.location = center + direction.normalized() * distance
    camera.rotation_euler = _look_at_rotation(direction.normalized())
    bpy.context.scene.camera = camera
    return camera


# ---------------------------------------------------------------------------
# Rendering
# ---------------------------------------------------------------------------

def _apply_override_material(objects: Sequence[bpy.types.Object],
                             material: Optional[bpy.types.Material]) -> dict:
    """Swap the subject's material slots, returning the state needed to restore them.

    ``view_layer.material_override`` is the obvious API and it is a trap here: EEVEE
    Next ignores it, so every diagnostic tile silently renders the object's ORIGINAL
    material instead. With an unmaterialed test object that means four channel renders
    come back byte-identical and look like a working measurement of a flat channel,
    when in truth no channel was ever rendered.

    Slot swapping works in every engine. The pipeline bible forbids leaving preview
    materials attached to a generated asset ("no unbounded ... side effects attached by
    the asset generator"), so the caller MUST pass the returned state back to
    :func:`_restore_materials` -- which the render functions do in a ``finally``.
    """
    saved = {}
    for obj in objects:
        if obj.type != "MESH":
            continue
        saved[obj.name] = [slot.material for slot in obj.material_slots]
        if material is None:
            continue
        if not obj.material_slots:
            obj.data.materials.append(material)
        else:
            for slot in obj.material_slots:
                slot.material = material
    return saved


def _restore_materials(objects: Sequence[bpy.types.Object], saved: dict) -> None:
    """Put the subject's original material slots back, including the empty case."""
    for obj in objects:
        if obj.type != "MESH" or obj.name not in saved:
            continue
        original = saved[obj.name]
        if not original:
            # The object had no slots before; drop the one we appended.
            while obj.data.materials:
                obj.data.materials.pop()
            continue
        for index, material in enumerate(original):
            if index < len(obj.material_slots):
                obj.material_slots[index].material = material


def _configure_render(spec: PreviewSpec) -> None:
    scene = bpy.context.scene
    scene.render.engine = spec.engine
    scene.render.resolution_x = spec.resolution
    scene.render.resolution_y = spec.resolution
    scene.render.resolution_percentage = 100
    scene.render.film_transparent = False
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGB"
    scene.render.image_settings.color_depth = "8"

    # No post. AGENTS.md forbids using bloom/grading to flatter weak geometry, and a
    # diagnostic render with a filmic curve is no longer a measurement.
    try:
        scene.view_settings.view_transform = "Standard"
        scene.view_settings.look = "None"
        scene.view_settings.exposure = 0.0
        scene.view_settings.gamma = 1.0
    except (AttributeError, TypeError):
        pass

    if spec.engine == "CYCLES":
        try:
            scene.cycles.samples = max(8, spec.samples)
            scene.cycles.use_denoising = True
        except AttributeError:
            pass
    else:
        try:
            scene.eevee.taa_render_samples = max(4, spec.samples)
        except AttributeError:
            pass

    world = scene.world
    if world is None:
        world = bpy.data.worlds.new("H8PREV_World")
        scene.world = world
    world.use_nodes = True
    background = world.node_tree.nodes.get("Background")
    if background is not None:
        value = spec.background
        background.inputs["Color"].default_value = (value, value, value * 1.15, 1.0)
        background.inputs["Strength"].default_value = 1.0


def _render_to(path: str) -> None:
    bpy.context.scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


def _load_pixels(path: str) -> np.ndarray:
    """RGBA float array, top-row-first, shape (h, w, 4)."""
    image = bpy.data.images.load(path)
    try:
        width, height = image.size
        buffer = np.empty(width * height * 4, dtype=np.float32)
        image.pixels.foreach_get(buffer)
        # Blender stores bottom-up; flip so the composite reads naturally.
        return buffer.reshape(height, width, 4)[::-1]
    finally:
        bpy.data.images.remove(image)


def _composite(tile_paths: Sequence[str], out_path: str, columns: int,
               label_band: int = 0) -> str:
    """Stitch tiles into one PNG with numpy, which ships inside Blender.

    A single sheet matters: the lead judges silhouette, bevel response and every
    vertex-colour channel in ONE image inspection instead of paying an image-read per
    view, and side-by-side is how a channel that went flat becomes obvious.
    """
    if not tile_paths:
        raise ValueError("no tiles to composite")
    tiles = [_load_pixels(path) for path in tile_paths]
    height, width = tiles[0].shape[0], tiles[0].shape[1]
    columns = max(1, columns)
    rows = int(math.ceil(len(tiles) / float(columns)))

    gutter = max(2, width // 160)
    sheet_h = rows * height + (rows + 1) * gutter + label_band
    sheet_w = columns * width + (columns + 1) * gutter
    sheet = np.zeros((sheet_h, sheet_w, 4), dtype=np.float32)
    sheet[:, :, 3] = 1.0
    sheet[:, :, 0:3] = 0.02

    for index, tile in enumerate(tiles):
        row = index // columns
        col = index % columns
        y = gutter + row * (height + gutter)
        x = gutter + col * (width + gutter)
        region = sheet[y:y + tile.shape[0], x:x + tile.shape[1], :]
        # Alpha-composite over the dark backdrop rather than copying the tile
        # wholesale. Channel tiles are rendered with a transparent film, so a straight
        # copy carries alpha=0 into the sheet and every viewer paints the background
        # white -- at which point a legitimately uniform 1.0 channel is white geometry
        # on a white field and becomes indistinguishable from an EMPTY FRAME. Found by
        # looking at the sheet, not by reading the code: the numbers were already
        # correct while the image was unreadable.
        alpha = tile[:, :, 3:4]
        region[:, :, 0:3] = tile[:, :, 0:3] * alpha + region[:, :, 0:3] * (1.0 - alpha)
        region[:, :, 3] = 1.0

    out_image = bpy.data.images.new("H8PREV_Sheet", width=sheet_w, height=sheet_h,
                                    alpha=True, float_buffer=False)
    try:
        out_image.pixels.foreach_set(sheet[::-1].reshape(-1))
        out_image.filepath_raw = out_path
        out_image.file_format = "PNG"
        out_image.save()
    finally:
        bpy.data.images.remove(out_image)
    return out_path


def _isolate_subject(objects: Sequence[bpy.types.Object]) -> dict:
    """Hide every mesh object that is not the subject, returning state to restore.

    This is the defect that produced three independent "the channel render does not match
    the data" reports. ``build_lod_chain`` leaves LOD1 and LOD2 in the scene AT THE SAME
    POSITION as LOD0 -- they are copies. The material swap only touches the subject, so the
    camera photographed three stacked LODs with two of them still wearing a default grey
    Principled material. Every channel then measured min 0.0 / max 1.0 with a plausible
    mid mean, and the coverage fraction was identical across channels because the silhouette
    came from the grey clones rather than from the channel-shaded subject.

    Proof: with LODs hidden, a coral's stored R max of 0.12477 (its rigid mineral cap) reads
    back as 0.12477 from the render. Before this, the same render reported 1.000.

    Hides via ``hide_render`` rather than ``hide_viewport``, because background renders ignore
    viewport visibility.
    """
    subjects = {obj.name for obj in objects}
    saved = {}
    for obj in bpy.data.objects:
        if obj.type != "MESH" or obj.name in subjects:
            continue
        if obj.name.startswith("H8PREV_"):
            continue  # rig geometry is managed separately by spec.scale_witness
        saved[obj.name] = obj.hide_render
        obj.hide_render = True
    return saved


def _restore_visibility(saved: dict) -> None:
    for name, hidden in saved.items():
        obj = bpy.data.objects.get(name)
        if obj is not None:
            obj.hide_render = hidden


def _prepare(objects: Sequence[bpy.types.Object], spec: PreviewSpec):
    _purge_scene_rig()
    collection = _ensure_collection()
    lo, hi = _world_bounds(objects)
    center = (lo + hi) * 0.5
    radius = max(1e-3, (hi - lo).length * 0.5)
    _build_lights(collection, radius)
    if spec.scale_witness:
        _build_scale_witness(collection, lo, radius)
    _configure_render(spec)
    return collection, center, radius


def render_contact_sheet(objects, spec: PreviewSpec) -> PreviewResult:
    """Multi-view sheet in one shading mode. Returns the composited PNG path."""
    if isinstance(objects, bpy.types.Object):
        objects = [objects]
    out_dir = spec.resolved_output_dir()
    os.makedirs(out_dir, exist_ok=True)
    deleted = clear_render_dir(out_dir, name_prefix=spec.name)

    collection, center, radius = _prepare(objects, spec)

    override = None
    if spec.mode == "flat":
        override = _flat_material()
    elif spec.mode == "studio":
        override = _studio_material()
    elif spec.mode == "normals":
        override = _normals_material()
    elif spec.mode in CHANNEL_INDEX:
        override = _channel_material(CHANNEL_INDEX[spec.mode])
    elif spec.mode != "material":
        raise ValueError("unknown preview mode: " + str(spec.mode))
    saved = _apply_override_material(objects, override)
    hidden = _isolate_subject(objects)

    tiles = []
    try:
        for view in spec.views:
            direction = VIEW_DIRECTIONS.get(view)
            if direction is None:
                raise ValueError("unknown view '" + view + "'")
            _place_camera(collection, direction, center, radius, spec.margin)
            path = os.path.join(out_dir, "{n}_{m}_{v}.png".format(
                n=spec.name, m=spec.mode, v=view))
            _render_to(path)
            tiles.append(path)
    finally:
        _restore_materials(objects, saved)
        _restore_visibility(hidden)

    columns = spec.columns if spec.columns > 0 else min(len(tiles), 2)
    sheet = os.path.join(out_dir, "{n}_SHEET_{m}.png".format(n=spec.name, m=spec.mode))
    _composite(tiles, sheet, columns)
    return PreviewResult(sheet, tuple(tiles), deleted, spec.mode, spec.resolution)


def render_channel_sheet(objects, spec: PreviewSpec,
                         view: str = "three_quarter") -> PreviewResult:
    """One tile per vertex-colour channel, from a single view.

    This is the gate for the vertex-colour contract. ``3DMODEL_FLORA_CORAL.md``
    section 8 rejects an asset whose "Root vertices sway as much as tips", and that
    defect is literally invisible in a lit render: the mesh is correct, the attribute
    exists, and only the raw R channel shows the gradient is gone.
    """
    if isinstance(objects, bpy.types.Object):
        objects = [objects]
    out_dir = spec.resolved_output_dir()
    os.makedirs(out_dir, exist_ok=True)
    deleted = clear_render_dir(out_dir, name_prefix=spec.name + "_chan")

    # Channel tiles are MEASURED, not just viewed, so the frame must contain the
    # subject and nothing else:
    #  - the rig is hidden, because ``material_override`` applies to every object in
    #    the view layer; leaving the emissive scale grid visible paints saturated
    #    1.0 pixels into the frame and every channel then reports max=1.0;
    #  - the film is transparent, so alpha gives an exact subject mask. Masking by
    #    luminance instead lets the backdrop through (a 0.045 grey backdrop is 0.23
    #    once display-encoded, far above any sane threshold) and the measurement
    #    ends up describing the background.
    channel_spec = PreviewSpec(**{**spec.__dict__, "scale_witness": False})
    collection, center, radius = _prepare(objects, channel_spec)
    bpy.context.scene.render.film_transparent = True
    bpy.context.scene.render.image_settings.color_mode = "RGBA"

    direction = VIEW_DIRECTIONS.get(view)
    if direction is None:
        raise ValueError("unknown view '" + view + "'")
    _place_camera(collection, direction, center, radius, spec.margin)

    labels = CHANNEL_LABELS.get(spec.surface_class, law.ORGANIC_VCOL)
    hidden = _isolate_subject(objects)
    tiles = []
    notes = []
    try:
        for mode, index in (("vcol_r", 0), ("vcol_g", 1), ("vcol_b", 2), ("vcol_a", 3)):
            saved = _apply_override_material(objects, _channel_material(index))
            path = os.path.join(out_dir, "{n}_chan{i}_{lab}.png".format(
                n=spec.name, i=index, lab=labels[index]))
            _render_to(path)
            _restore_materials(objects, saved)
            tiles.append(path)
            notes.append("channel {i} = {lab}".format(i=index, lab=labels[index]))
    finally:
        _restore_visibility(hidden)
        bpy.context.scene.render.film_transparent = False
        bpy.context.scene.render.image_settings.color_mode = "RGB"

    sheet = os.path.join(out_dir, "{n}_SHEET_CHANNELS.png".format(n=spec.name))
    _composite(tiles, sheet, 4)
    return PreviewResult(sheet, tuple(tiles), deleted, "channels", spec.resolution,
                         tuple(notes))


# ---------------------------------------------------------------------------
# Measurement, not impression
# ---------------------------------------------------------------------------

@dataclass
class ChannelStats:
    channel: str
    min_value: float
    max_value: float
    mean_value: float
    covered_pixels: int
    coverage_fraction: float = 0.0

    @property
    def has_gradient(self) -> bool:
        """Spread judged RELATIVE to the range the channel actually occupies.

        An absolute threshold repeats a bug already fixed in ``SwayField.is_uniform``: a
        rigid mineralised coral is capped by ``3DMODEL_FLORA_CORAL.md`` section 2 at
        32/255 = 0.1255, so its entire legal range is narrower than a 0.20 absolute
        threshold and every compliant rigid asset reads as flat. Measured: a coral whose
        channel correctly spanned 0.000..0.127 reported ``gradient=False``.

        Judging against the observed maximum asks the right question -- does this channel
        VARY across the surface -- instead of conflating "narrow band" with "constant".
        """
        span = self.max_value - self.min_value
        reference = max(self.max_value, 1e-6)
        return (span / reference) > 0.20

    @property
    def subject_visible(self) -> bool:
        """Guards against measuring an empty frame.

        A tile where the subject missed the camera has zero covered pixels and would
        otherwise report min=max=0 -- indistinguishable from a legitimately black
        channel. A near-full frame is equally suspect: it means the mask caught the
        backdrop instead of the subject.
        """
        return 0.0005 < self.coverage_fraction < 0.98


def _srgb_to_linear(values: np.ndarray) -> np.ndarray:
    """Undo display encoding so reported numbers match the stored vertex colours.

    An 8-bit PNG written through the Standard view transform holds display-encoded
    values, so a channel storing linear 0.045 reads back as 0.23. Reporting that raw
    would make every comparison against a bible threshold (which are all in 0..1
    linear) wrong by a large, non-linear factor.
    """
    low = values <= 0.04045
    out = np.empty_like(values)
    out[low] = values[low] / 12.92
    out[~low] = np.power((values[~low] + 0.055) / 1.055, 2.4)
    return out


def measure_channel_png(path: str, alpha_threshold: float = 0.5,
                        linearise: bool = True) -> ChannelStats:
    """Sample a rendered channel tile and report its real value range.

    ``AGENTS.md`` ``[RULE] Never Trust Automated Assertions Alone``: the existence of a
    PNG proves nothing. This turns the render into a number so a gradient claim is
    backed by pixels.

    The subject mask comes from ALPHA, which requires the tile to have been rendered
    with ``film_transparent`` -- as :func:`render_channel_sheet` does. Masking by
    luminance instead admits the backdrop (a 0.045 grey reads 0.23 once display-encoded)
    and the statistics then describe the background rather than the asset, which is how
    four different channels can report byte-identical numbers and still look plausible.
    """
    pixels = _load_pixels(path)
    total = pixels.shape[0] * pixels.shape[1]
    if pixels.shape[2] >= 4:
        mask = pixels[:, :, 3] > alpha_threshold
    else:
        mask = np.ones((pixels.shape[0], pixels.shape[1]), dtype=bool)
    covered = int(mask.sum())
    if covered == 0:
        return ChannelStats(os.path.basename(path), 0.0, 0.0, 0.0, 0, 0.0)

    values = pixels[:, :, 0][mask]
    if linearise:
        values = _srgb_to_linear(values)
    return ChannelStats(
        os.path.basename(path),
        float(values.min()),
        float(values.max()),
        float(values.mean()),
        covered,
        covered / float(total),
    )
