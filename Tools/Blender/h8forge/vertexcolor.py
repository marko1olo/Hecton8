"""Vertex colour channels with baked occlusion, per the HECTON-8 semantic contract.

``3dmodel.md`` section 5 and ``3DMODEL_FLORA_CORAL.md`` section 2 fix the meaning of
every channel. Organic families:

    R = water-current sway amplitude. Anchor/root = 0. Rigid mineralized coral = 0..32.
        Flexible frond tips = 192..255.
    G = bioluminescence mask or phase. Non-emissive tissue = 0.
    B = baked ambient occlusion / cavity darkness.
    A = thickness, damage eligibility, harvest mask, or wetness -- meaning MUST be
        written into the manifest.

Hard-surface families (``3dmodel.md`` section 4): R = edge wear, G = oxidation,
B = baked AO, A = emission/decal eligibility.

The B channel is the reason this pipeline runs in Blender at all. A C# generator can
only *approximate* occlusion from local curvature; Cycles ray-traces it. The bibles
list baked AO as mandatory, and "the generator computed something AO-shaped" is not
the same artefact.

Order of operations is load-bearing: ``bpy.ops.object.bake`` writes ALL channels of
the target attribute, so occlusion is baked into a scratch attribute first and then
composed into channel B alongside the analytically-authored R/G/A. Baking last, or
into the final attribute, silently destroys the sway gradient -- and a destroyed
gradient is invisible in a normal render, which is precisely the failure mode the
project's own rule file calls silent degeneracy.
"""

from __future__ import annotations

from dataclasses import dataclass
from typing import Callable, Optional

import bpy
from mathutils import Vector

from . import law
from .blackbox import BlackBox

FINAL_ATTRIBUTE = law.VCOL_ATTRIBUTE_NAME
_SCRATCH_AO_ATTRIBUTE = "H8_AO_Scratch"


# ---------------------------------------------------------------------------
# Attribute plumbing
# ---------------------------------------------------------------------------

def ensure_color_attribute(
    mesh: bpy.types.Mesh,
    name: str = FINAL_ATTRIBUTE,
    *,
    data_type: str = "BYTE_COLOR",
    domain: str = "CORNER",
) -> bpy.types.Attribute:
    """Get or create a colour attribute and make it active for render and export.

    ``BYTE_COLOR``/``CORNER`` matches the bible's declared vertex layout
    (``Color | UNorm8 x4``) and is what Unity's FBX importer consumes as
    ``Mesh.colors32``. A ``FLOAT_COLOR``/``POINT`` attribute survives Blender but
    changes the exported layout, so the default here is not cosmetic.
    """
    existing = mesh.color_attributes.get(name)
    if existing is not None and (existing.data_type != data_type or existing.domain != domain):
        mesh.color_attributes.remove(existing)
        existing = None
    if existing is None:
        existing = mesh.color_attributes.new(name=name, type=data_type, domain=domain)
    # Blender 4.x splits "active for editing" from "used for render/export". The
    # former is an object reference on color_attributes; the latter is a NAME on
    # mesh.attributes. Setting only the first leaves the exporter reading whichever
    # layer happens to be default, which is how a correctly-authored colour set ends
    # up absent from the FBX.
    mesh.color_attributes.active_color = existing
    mesh.attributes.active_color_name = name
    mesh.attributes.default_color_name = name
    return existing


def _write_channels(
    mesh: bpy.types.Mesh,
    attribute: bpy.types.Attribute,
    per_vertex: Callable[[int, Vector], tuple],
) -> None:
    """Fill an attribute from a callback over (vertex_index, local_position).

    Handles both CORNER and POINT domains so callers do not have to branch: on the
    CORNER domain each loop resolves to its own vertex, which is what keeps a split
    seam from inheriting a neighbour's sway value.
    """
    if attribute.domain == "CORNER":
        for loop_index, loop in enumerate(mesh.loops):
            vertex_index = loop.vertex_index
            rgba = per_vertex(vertex_index, mesh.vertices[vertex_index].co)
            attribute.data[loop_index].color = rgba
    else:
        for vertex_index, vertex in enumerate(mesh.vertices):
            attribute.data[vertex_index].color = per_vertex(vertex_index, vertex.co)


def _read_channel_per_vertex(mesh: bpy.types.Mesh, attribute: bpy.types.Attribute,
                             channel: int) -> list:
    """Collapse an attribute down to one float per vertex.

    A CORNER attribute stores one value per loop; several loops share a vertex. We
    average them, which is correct for occlusion (a smooth scalar field) and is why
    this helper is not used for the sway channel, where a split seam legitimately
    holds different values on either side.
    """
    # COLD ALLOC: float[vertexCount] x2 - AO accumulation - owner: _read_channel_per_vertex
    totals = [0.0] * len(mesh.vertices)
    counts = [0] * len(mesh.vertices)
    if attribute.domain == "CORNER":
        for loop_index, loop in enumerate(mesh.loops):
            value = attribute.data[loop_index].color[channel]
            totals[loop.vertex_index] += value
            counts[loop.vertex_index] += 1
    else:
        for vertex_index in range(len(mesh.vertices)):
            totals[vertex_index] = attribute.data[vertex_index].color[channel]
            counts[vertex_index] = 1
    return [totals[i] / counts[i] if counts[i] else 1.0 for i in range(len(totals))]


# ---------------------------------------------------------------------------
# Baked ambient occlusion  --  channel B
# ---------------------------------------------------------------------------

@dataclass
class AoBakeResult:
    baked: bool
    samples: int
    min_value: float
    max_value: float
    mean_value: float
    reason: str = ""

    @property
    def has_contrast(self) -> bool:
        """Occlusion that never varies is not occlusion.

        A flat AO field means the bake silently failed (no material, wrong engine,
        zero samples) and the mesh would ship with a meaningless B channel that still
        passes a presence check. This is the guard against that.
        """
        return (self.max_value - self.min_value) > 0.04


def bake_ambient_occlusion(
    obj: bpy.types.Object,
    *,
    samples: int = 64,
    distance: float = 0.35,
    blackbox: Optional[BlackBox] = None,
) -> AoBakeResult:
    """Ray-trace occlusion into a scratch colour attribute and return per-vertex values.

    Requires Cycles. The object needs at least one material slot: Blender's bake
    refuses to run without one, and failing loudly here is better than returning a
    uniform field that looks like a successful bake.

    ``distance`` bounds the AO ray length in metres. For coral and kelp the cavity
    detail the bible asks for ("Use low values in crevices, under plates, root
    clusters, and branch intersections") lives within a few centimetres, so an
    unbounded distance washes it out into a global sky-occlusion term.
    """
    mesh = obj.data
    if not obj.material_slots or all(s.material is None for s in obj.material_slots):
        reason = "no material slot; Cycles bake requires one"
        if blackbox is not None:
            blackbox.note_invalid("bake_ao", "AO_NO_MATERIAL", reason)
        return AoBakeResult(False, 0, 0.0, 0.0, 0.0, reason)

    scene = bpy.context.scene
    previous_engine = scene.render.engine
    previous_samples = None
    scene.render.engine = "CYCLES"
    try:
        previous_samples = scene.cycles.samples
        scene.cycles.samples = samples
    except AttributeError:
        # Cycles addon present but scene property set not initialised; the bake
        # operator still honours bake settings, so this is not fatal.
        previous_samples = None

    scratch = ensure_color_attribute(mesh, _SCRATCH_AO_ATTRIBUTE,
                                     data_type="FLOAT_COLOR", domain="POINT")

    view_layer = bpy.context.view_layer
    previous_active = view_layer.objects.active
    for other in bpy.context.selected_objects:
        other.select_set(False)
    obj.select_set(True)
    view_layer.objects.active = obj

    scene.render.bake.use_selected_to_active = False

    # AO ray length lives on the WORLD, not on the bake settings. Two wrong answers were
    # tried and measured before this one, so the trail is worth recording:
    #   - `scene.render.bake.distance` does not exist on 4.5 at all. Wrapped in a bare
    #     try/except AttributeError it silently swallowed the assignment.
    #   - `scene.render.bake.max_ray_distance` exists but changes the AO statistics NOT AT
    #     ALL -- its RNA scope is selected-to-active cage matching. It looked like the
    #     rename and was not, which is worse than a missing attribute: it reads as a fix.
    # The real knob is `scene.world.light_settings.distance` (Gather -> Distance).
    #
    # It matters because unbounded rays turn local occlusion into a global sky term: every
    # branch occludes every other across the whole colony, which crushed one coral's AO
    # mean to 0.078 and buried exactly what the bible asks for -- "low values in crevices,
    # under plates, root clusters, and branch intersections".
    ao_distance_applied = False
    world = scene.world
    if world is None:
        world = bpy.data.worlds.new("H8_ForgeBakeWorld")
        scene.world = world
    light_settings = getattr(world, "light_settings", None)
    if light_settings is not None and hasattr(light_settings, "distance"):
        light_settings.distance = distance
        ao_distance_applied = True
    if not ao_distance_applied and blackbox is not None:
        blackbox.note_invalid(
            "bake_ao", "AO_DISTANCE_UNSUPPORTED",
            "world.light_settings.distance unavailable; AO rays are unbounded and local "
            "cavity contrast will be lost")

    baked = True
    reason = ""
    try:
        bpy.ops.object.bake(type="AO", target="VERTEX_COLORS", use_clear=True)
    except RuntimeError as error:
        baked = False
        reason = "Cycles AO bake failed: " + str(error)

    values = _read_channel_per_vertex(mesh, mesh.color_attributes[_SCRATCH_AO_ATTRIBUTE], 0) \
        if baked else []

    scene.render.engine = previous_engine
    if previous_samples is not None:
        try:
            scene.cycles.samples = previous_samples
        except AttributeError:
            pass
    if previous_active is not None:
        view_layer.objects.active = previous_active

    if not baked or not values:
        if blackbox is not None:
            blackbox.note_invalid("bake_ao", "AO_BAKE_FAILED", reason or "no values read")
        return AoBakeResult(False, samples, 0.0, 0.0, 0.0, reason or "no values read")

    lo = min(values)
    hi = max(values)
    mean = sum(values) / len(values)
    result = AoBakeResult(True, samples, lo, hi, mean)
    if blackbox is not None:
        blackbox.record(
            "bake_ao", vertex_count=len(mesh.vertices), triangle_count=-1,
            warning="" if result.has_contrast else
            "AO field is flat (min={lo:.3f} max={hi:.3f}); bake produced no cavity "
            "information".format(lo=lo, hi=hi),
        )
    obj["h8_ao_values"] = values
    return result


def consume_baked_ao(obj: bpy.types.Object) -> list:
    """Per-vertex AO values stashed by :func:`bake_ambient_occlusion`, then dropped.

    Kept on the object rather than returned-and-threaded so the generator can bake
    early and compose late without carrying the array through unrelated stages.
    """
    values = obj.get("h8_ao_values")
    if values is None:
        return []
    out = [float(v) for v in values]
    del obj["h8_ao_values"]
    return out


def remove_scratch_attributes(mesh: bpy.types.Mesh) -> None:
    """Drop bake scratch data so it never reaches the exported FBX."""
    scratch = mesh.color_attributes.get(_SCRATCH_AO_ATTRIBUTE)
    if scratch is not None:
        mesh.color_attributes.remove(scratch)


# ---------------------------------------------------------------------------
# Organic channel authoring
# ---------------------------------------------------------------------------

@dataclass
class SwayField:
    """Per-vertex sway amplitudes plus the evidence that the gradient is real."""

    values: list
    anchor_value: float
    tip_value: float
    min_value: float
    max_value: float
    stiffness_exponent: float
    expected_max: float = 1.0

    @property
    def relative_spread(self) -> float:
        """Observed spread as a fraction of the band this organism is allowed to use.

        Rigid organisms legitimately occupy a narrow band. 3DMODEL_FLORA_CORAL.md
        section 2 caps "Rigid mineralized coral" at 0 to 32/255, so a CORRECT mineralised
        coral has an absolute spread of at most 0.125. Judging it against an absolute
        threshold marks every compliant rigid asset as broken -- which is what an earlier
        version of this check did, and it flagged a coral whose gradient was visibly fine
        in the channel render.
        """
        band = max(1e-6, self.expected_max)
        return (self.max_value - self.min_value) / band

    @property
    def is_uniform(self) -> bool:
        """3DMODEL_FLORA_CORAL.md section 8 rejects: "Root vertices sway as much as tips."

        A uniform R channel is the single most likely silent failure in an organic
        generator: the mesh looks correct, the attribute exists, the validator sees
        data, and the shader animates the whole organism as a rigid body swinging
        from nothing. Flagging it here is cheaper than discovering it in-engine.

        Measured relative to the permitted band so stiffness and brokenness are not
        confused for each other.
        """
        return self.relative_spread < 0.25


def build_sway_field(
    mesh: bpy.types.Mesh,
    *,
    anchor_position: Vector,
    max_flexible_length: float,
    stiffness_exponent: float,
    rigid_cap: Optional[float] = None,
    distances: Optional[list] = None,
) -> SwayField:
    """Sway amplitude per vertex from the bible's leverage formula.

    ``sway = saturate(distanceFromAnchor / maxFlexibleLength) ^ stiffnessExponent``

    Distance is measured from the anchor point, not from the object origin: a coral
    whose origin sits at its centre of mass would otherwise give its holdfast a
    non-zero sway value and its lowest branches more movement than its tips.

    ``rigid_cap`` clamps mineralised tissue into the bible's 0..32/255 band while
    still preserving a gradient inside it, so the shader has something to work with
    and the uniformity gate does not misfire on legitimately stiff coral.
    """
    use_supplied = distances is not None and len(distances) == len(mesh.vertices)
    values = []
    for index, vertex in enumerate(mesh.vertices):
        # A caller that knows the branch topology should pass GEODESIC distance along the
        # skeleton. Straight-line distance from the anchor is wrong for anything that
        # bends back toward its own root: a drooping frond tip can sit physically close
        # to the holdfast while being far along the stem, and Euclidean distance then
        # tells the shader that tip is rigid.
        distance = distances[index] if use_supplied else (vertex.co - anchor_position).length
        amplitude = law.sway_amplitude(distance, max_flexible_length, stiffness_exponent)
        if rigid_cap is not None:
            amplitude = amplitude * rigid_cap
        values.append(amplitude)

    expected_max = rigid_cap if rigid_cap is not None else 1.0

    if not values:
        return SwayField([], law.SWAY_ANCHOR, law.SWAY_ANCHOR, 0.0, 0.0,
                         stiffness_exponent, expected_max)

    return SwayField(
        values=values,
        anchor_value=min(values),
        tip_value=max(values),
        min_value=min(values),
        max_value=max(values),
        stiffness_exponent=stiffness_exponent,
        expected_max=expected_max,
    )


def write_organic_channels(
    obj: bpy.types.Object,
    *,
    sway: SwayField,
    biolum: Optional[list] = None,
    ao: Optional[list] = None,
    alpha: Optional[list] = None,
    alpha_meaning: str = "harvest_mask",
    blackbox: Optional[BlackBox] = None,
) -> dict:
    """Compose R/G/B/A into the final attribute per the organic contract.

    Missing G defaults to 0 ("Non-emissive tissue = 0" -- an explicit bible value, not
    a placeholder). Missing B defaults to 1.0, i.e. fully unoccluded, because a
    darkening default would bake fake shadow into every asset whose AO bake failed,
    and ``3dmodel.md`` forbids using darkness to hide missing work.
    """
    mesh = obj.data
    attribute = ensure_color_attribute(mesh, FINAL_ATTRIBUTE)
    count = len(mesh.vertices)

    def channel(source: Optional[list], default: float) -> Callable[[int], float]:
        if source is None or len(source) != count:
            return lambda _i: default
        return lambda i: law.saturate(source[i])

    get_r = channel(sway.values, law.SWAY_ANCHOR)
    get_g = channel(biolum, 0.0)
    get_b = channel(ao, 1.0)
    get_a = channel(alpha, 1.0)

    _write_channels(mesh, attribute,
                    lambda i, _co: (get_r(i), get_g(i), get_b(i), get_a(i)))

    report = {
        "attribute": FINAL_ATTRIBUTE,
        "contract": list(law.ORGANIC_VCOL),
        "alphaMeaning": alpha_meaning,
        "swayMin": round(sway.min_value, 5),
        "swayMax": round(sway.max_value, 5),
        "swayStiffnessExponent": sway.stiffness_exponent,
        "swayUniform": sway.is_uniform,
        "biolumWritten": biolum is not None and len(biolum) == count,
        "aoWritten": ao is not None and len(ao) == count,
        "alphaWritten": alpha is not None and len(alpha) == count,
    }
    if blackbox is not None:
        blackbox.record(
            "write_organic_channels", vertex_count=count,
            warning="sway channel is uniform; flora rejection gate" if sway.is_uniform else "",
        )
    return report


def write_hard_surface_channels(
    obj: bpy.types.Object,
    *,
    edge_wear: Optional[list] = None,
    oxidation: Optional[list] = None,
    ao: Optional[list] = None,
    emission_mask: Optional[list] = None,
    blackbox: Optional[BlackBox] = None,
) -> dict:
    """Compose R/G/B/A per the hard-surface wear contract (3dmodel.md section 4)."""
    mesh = obj.data
    attribute = ensure_color_attribute(mesh, FINAL_ATTRIBUTE)
    count = len(mesh.vertices)

    def channel(source: Optional[list], default: float) -> Callable[[int], float]:
        if source is None or len(source) != count:
            return lambda _i: default
        return lambda i: law.saturate(source[i])

    get_r = channel(edge_wear, 0.0)
    get_g = channel(oxidation, 0.0)
    get_b = channel(ao, 1.0)
    get_a = channel(emission_mask, 0.0)

    _write_channels(mesh, attribute,
                    lambda i, _co: (get_r(i), get_g(i), get_b(i), get_a(i)))

    report = {
        "attribute": FINAL_ATTRIBUTE,
        "contract": list(law.HARD_SURFACE_VCOL),
        "edgeWearWritten": edge_wear is not None and len(edge_wear) == count,
        "oxidationWritten": oxidation is not None and len(oxidation) == count,
        "aoWritten": ao is not None and len(ao) == count,
        "emissionWritten": emission_mask is not None and len(emission_mask) == count,
    }
    if blackbox is not None:
        blackbox.record("write_hard_surface_channels", vertex_count=count)
    return report


def channel_stats(obj: bpy.types.Object,
                  attribute_name: str = FINAL_ATTRIBUTE) -> dict:
    """AREA-WEIGHTED per-channel statistics read straight off the mesh.

    Exists to be comparable with :func:`preview.measure_channel_png`, and that comparability
    is the whole point. A rendered tile averages over PIXELS, which weights by projected
    screen area; a naive readback averages over LOOPS, which weights by loop count. For a
    NON-UNIFORM field those two numbers legitimately differ, and comparing them produced two
    independent "the render does not match the data" reports that were really a methodology
    error, not a defect.

    Measured proof of exactly that: with a UNIFORM field the two agree to 4 decimal places
    (stored 0.2016 / 0.5029 / 0.7991 vs rendered 0.2016 / 0.5025 / 0.7988). With a
    non-uniform field the same pair reads 0.8408 stored against 0.7092 rendered -- no bug,
    two different weightings of the same data.

    So this weights each corner by a third of its triangle's world area, which is the
    closest cheap analogue of what a pixel average measures. ``min``/``max`` are weighting-
    independent and are therefore the values to compare when you want a hard assertion.
    """
    mesh = obj.data
    attribute = mesh.color_attributes.get(attribute_name)
    if attribute is None:
        return {"present": False, "attribute": attribute_name}

    mesh.calc_loop_triangles()
    weights = [0.0] * len(mesh.loops)
    for tri in mesh.loop_triangles:
        p0 = mesh.vertices[tri.vertices[0]].co
        p1 = mesh.vertices[tri.vertices[1]].co
        p2 = mesh.vertices[tri.vertices[2]].co
        area = (p1 - p0).cross(p2 - p0).length * 0.5
        share = area / 3.0
        for loop in tri.loops:
            weights[loop] += share

    totals = [0.0, 0.0, 0.0, 0.0]
    minima = [1.0, 1.0, 1.0, 1.0]
    maxima = [0.0, 0.0, 0.0, 0.0]
    weight_sum = 0.0

    if attribute.domain == "CORNER":
        for loop_index in range(len(mesh.loops)):
            colour = attribute.data[loop_index].color
            weight = weights[loop_index]
            weight_sum += weight
            for channel in range(4):
                value = colour[channel]
                totals[channel] += value * weight
                if value < minima[channel]:
                    minima[channel] = value
                if value > maxima[channel]:
                    maxima[channel] = value
    else:
        # POINT domain: fold loop weights back onto their vertices.
        vertex_weights = [0.0] * len(mesh.vertices)
        for loop_index, loop in enumerate(mesh.loops):
            vertex_weights[loop.vertex_index] += weights[loop_index]
        for vertex_index in range(len(mesh.vertices)):
            colour = attribute.data[vertex_index].color
            weight = vertex_weights[vertex_index]
            weight_sum += weight
            for channel in range(4):
                value = colour[channel]
                totals[channel] += value * weight
                if value < minima[channel]:
                    minima[channel] = value
                if value > maxima[channel]:
                    maxima[channel] = value

    if weight_sum <= 0.0:
        return {"present": True, "attribute": attribute_name, "degenerate": True}

    return {
        "present": True,
        "attribute": attribute_name,
        "domain": attribute.domain,
        "areaWeightedMean": [round(totals[c] / weight_sum, 5) for c in range(4)],
        "min": [round(minima[c], 5) for c in range(4)],
        "max": [round(maxima[c], 5) for c in range(4)],
        "comparableWithRender": "min and max are weighting-independent; compare those. "
                               "areaWeightedMean approximates a pixel average but is not "
                               "identical to it, because a render also weights by "
                               "visibility and projected foreshortening.",
    }


def curvature_edge_wear(obj: bpy.types.Object) -> list:
    """Per-vertex convexity in 0..1, for the hard-surface R (edge wear) channel.

    Convex vertices are the ones a salt current polishes and a crate corner scuffs,
    so wear tracks convexity. Measured as the agreement between the vertex normal and
    the direction from the neighbourhood centroid to the vertex: fully convex tends to
    1, flat to ~0.5, concave to 0.

    This is a geometric estimate, not a ray-traced quantity. It is honest for wear
    (a heuristic mask an artist would paint anyway) and would NOT be honest for
    occlusion, which is why channel B uses a real Cycles bake instead of this.
    """
    mesh = obj.data
    # COLD ALLOC: Vector[vertexCount] + int[vertexCount] - neighbour centroid fold
    # - owner: curvature_edge_wear
    centroids = [Vector((0.0, 0.0, 0.0)) for _ in range(len(mesh.vertices))]
    counts = [0] * len(mesh.vertices)

    for edge in mesh.edges:
        a, b = edge.vertices[0], edge.vertices[1]
        centroids[a] += mesh.vertices[b].co
        counts[a] += 1
        centroids[b] += mesh.vertices[a].co
        counts[b] += 1

    out = []
    for index, vertex in enumerate(mesh.vertices):
        if counts[index] == 0:
            out.append(0.0)
            continue
        centroid = centroids[index] / counts[index]
        offset = vertex.co - centroid
        if offset.length <= 1e-9:
            out.append(0.5)
            continue
        out.append(law.saturate(0.5 + 0.5 * vertex.normal.normalized().dot(offset.normalized())))
    return out
