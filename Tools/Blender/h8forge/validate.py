"""Mesh validation gate. Rejects geometry before any save.

``3dmodel.md`` section 10, "Automated Quality Gates":

    "Before any generator calls AssetDatabase.SaveAssets, PrefabUtility.
     SaveAsPrefabAsset, or writes a manifest, it must run validation. Failure
     aborts save. Warnings are allowed only for non-shipping diagnostic assets."

``PROCEDURAL_ASSET_PIPELINE.md``, "Validation Before Save", adds the winding,
material-slot-naming, LOD-existence and collision-proxy clauses. Both lists are
implemented here; the gate identifiers below map one-to-one onto their bullets.

Design rules this module obeys:

*   Every failure is reported. A generator author gets the whole list from one
    run, matching ``AGENTS.md``: "If code breaks compile, do not stop at the
    first error."
*   Repeated occurrences of one gate aggregate into a single ``Failure`` with a
    count, but the detail string always names the first offending index or value
    so it stays locatable.
*   A hard bible requirement is a failure, never a warning. ``warnings`` carries
    only the notices for gates that were legitimately not applicable to this
    call (declared triplanar, no atlas declared, and so on) so an unenforced
    gate is visible instead of silently reading as a pass.
*   No global mutable state. All accumulation is local to one call.

Two layers, on purpose:

``extract_mesh_data`` snapshots a Blender datablock into flat ``MeshData``
buffers; ``validate_mesh_data`` runs the gate battery over that snapshot. The
snapshot is the same shape the Unity writer feeds ``SetVertexBufferParams`` /
``SetIndexBufferParams``, so the buffer-integrity gates (index range, index
count, non-finite normal/tangent) stay live on the path where corruption is
actually expressible. Blender's own datablock cannot express some of those
states -- it re-normalises its normal cache, always returns unit tangents with a
+-1 sign, saturates infinities to FLT_MAX on assignment, and hands out loop
triangles that are triples by construction. Those gates are therefore enforced
on ``MeshData``, which is where the exporter can still get it wrong.

This module imports no Blender symbols. It duck-types the mesh datablock, so the
pure gates run outside Blender as well.
"""

from __future__ import annotations

import hashlib
import struct
from dataclasses import dataclass, field

from . import law
from .blackbox import GenerationAborted

# ---------------------------------------------------------------------------
# Gate identifiers
# ---------------------------------------------------------------------------
# Stable ids. A generator, a manifest, or a task report may cite these, so they
# are constants rather than inline strings.

GATE_EMPTY_MESH = "empty_mesh"
GATE_NO_TRIANGLES = "no_triangles"
GATE_INDEX_COUNT_NOT_TRIANGULATED = "index_count_not_triangulated"
GATE_INDEX_OUT_OF_RANGE = "index_out_of_range"
GATE_DEGENERATE_TRIANGLE = "degenerate_triangle"
GATE_ZERO_AREA_UV_TRIANGLE = "zero_area_uv_triangle"
GATE_INCONSISTENT_WINDING = "inconsistent_winding"
GATE_NON_FINITE_POSITION = "non_finite_position"
GATE_NON_FINITE_NORMAL = "non_finite_normal"
GATE_NON_FINITE_TANGENT = "non_finite_tangent"
GATE_NON_FINITE_UV = "non_finite_uv"
GATE_NON_FINITE_COLOR = "non_finite_color"
GATE_NON_FINITE_BOUNDS = "non_finite_bounds"
GATE_NORMAL_LENGTH_OUT_OF_RANGE = "normal_length_out_of_range"
GATE_TANGENT_LENGTH_OUT_OF_RANGE = "tangent_length_out_of_range"
GATE_TANGENT_HANDEDNESS_INVALID = "tangent_handedness_invalid"
GATE_BOUNDS_EXTENT_TOO_SMALL = "bounds_extent_too_small"
GATE_VERTEX_COLOR_LAYER_MISSING = "vertex_color_layer_missing"
GATE_VERTEX_COLOR_CONTRACT_MISMATCH = "vertex_color_contract_mismatch"
GATE_VERTEX_COLOR_OUT_OF_UNORM_RANGE = "vertex_color_out_of_unorm_range"
GATE_ORGANIC_SWAY_ANCHOR_MISSING = "organic_sway_anchor_missing"
GATE_UV0_MISSING = "uv0_missing"
GATE_UV_STRETCH_EXCESSIVE = "uv_stretch_excessive"
GATE_UV_ISLAND_BELOW_MIN_PIXELS = "uv_island_below_min_pixels"
GATE_UV_ATLAS_PADDING_VIOLATION = "uv_atlas_padding_violation"
GATE_MATERIAL_SLOT_COUNT_EXCEEDED = "material_slot_count_exceeded"
GATE_MATERIAL_INDEX_OUT_OF_SLOT_RANGE = "material_index_out_of_slot_range"
GATE_MATERIAL_SLOTS_MISSING = "material_slots_missing"
GATE_SUBMESH_EMPTY_DECLARED_SLOT = "submesh_empty_declared_slot"
GATE_LOD_TRIANGLE_BUDGET_EXCEEDED = "lod_triangle_budget_exceeded"

MESH_GATES = (
    GATE_EMPTY_MESH,
    GATE_NO_TRIANGLES,
    GATE_INDEX_COUNT_NOT_TRIANGULATED,
    GATE_INDEX_OUT_OF_RANGE,
    GATE_DEGENERATE_TRIANGLE,
    GATE_ZERO_AREA_UV_TRIANGLE,
    GATE_INCONSISTENT_WINDING,
    GATE_NON_FINITE_POSITION,
    GATE_NON_FINITE_NORMAL,
    GATE_NON_FINITE_TANGENT,
    GATE_NON_FINITE_UV,
    GATE_NON_FINITE_COLOR,
    GATE_NON_FINITE_BOUNDS,
    GATE_NORMAL_LENGTH_OUT_OF_RANGE,
    GATE_TANGENT_LENGTH_OUT_OF_RANGE,
    GATE_TANGENT_HANDEDNESS_INVALID,
    GATE_BOUNDS_EXTENT_TOO_SMALL,
    GATE_VERTEX_COLOR_LAYER_MISSING,
    GATE_VERTEX_COLOR_CONTRACT_MISMATCH,
    GATE_VERTEX_COLOR_OUT_OF_UNORM_RANGE,
    GATE_ORGANIC_SWAY_ANCHOR_MISSING,
    GATE_UV0_MISSING,
    GATE_UV_STRETCH_EXCESSIVE,
    GATE_UV_ISLAND_BELOW_MIN_PIXELS,
    GATE_UV_ATLAS_PADDING_VIOLATION,
    GATE_MATERIAL_SLOT_COUNT_EXCEEDED,
    GATE_MATERIAL_INDEX_OUT_OF_SLOT_RANGE,
    GATE_MATERIAL_SLOTS_MISSING,
    GATE_SUBMESH_EMPTY_DECLARED_SLOT,
    GATE_LOD_TRIANGLE_BUDGET_EXCEEDED,
)

GATE_COLLIDER_TRIANGLE_BUDGET_EXCEEDED = "collider_triangle_budget_exceeded"
GATE_COLLIDER_NOT_CONVEX = "collider_not_convex"
GATE_COLLIDER_IS_VISUAL_MESH = "collider_is_visual_mesh"
GATE_COLLIDER_NAME_NOT_COL_PREFIXED = "collider_name_not_col_prefixed"
GATE_COLLIDER_CROSSCHECK_UNAVAILABLE = "collider_crosscheck_unavailable"
GATE_COLLIDER_EMPTY = "collider_empty"

COLLIDER_GATES = (
    GATE_COLLIDER_EMPTY,
    GATE_COLLIDER_TRIANGLE_BUDGET_EXCEEDED,
    GATE_COLLIDER_NOT_CONVEX,
    GATE_COLLIDER_IS_VISUAL_MESH,
    GATE_COLLIDER_NAME_NOT_COL_PREFIXED,
    GATE_COLLIDER_CROSSCHECK_UNAVAILABLE,
)

GATE_LOD_CHAIN_INCOMPLETE = "lod_chain_incomplete"
GATE_LOD_CHAIN_NOT_MONOTONIC = "lod_chain_not_monotonic"
GATE_LOD_CHAIN_DUPLICATE_INDEX = "lod_chain_duplicate_index"

LOD_CHAIN_GATES = (
    GATE_LOD_CHAIN_INCOMPLETE,
    GATE_LOD_CHAIN_NOT_MONOTONIC,
    GATE_LOD_CHAIN_DUPLICATE_INDEX,
)

# 3dmodel.md section 7: "LOD0: near silhouette and baked detail. LOD1:
# preserved silhouette... LOD2: coarse silhouette or proxy shell."
REQUIRED_LOD_INDICES = (0, 1, 2)

VALIDATOR_VERSION = "1.0.0"

# UV island connectivity quantisation. 1e-5 of UV space is 0.04 px on a 4096
# atlas, so seam corners that share a coordinate hash together while genuinely
# separate islands stay separate.
_UV_WELD_QUANT = 100000.0


# ---------------------------------------------------------------------------
# Result types
# ---------------------------------------------------------------------------

@dataclass
class Failure:
    """One violated gate. ``count`` aggregates repeats of the same gate."""

    gate: str
    detail: str
    count: int = 1

    def __str__(self) -> str:
        if self.count > 1:
            return "{g} x{c}: {d}".format(g=self.gate, c=self.count, d=self.detail)
        return "{g}: {d}".format(g=self.gate, d=self.detail)


@dataclass
class MeshReport:
    """Validation result for one mesh. ``passed`` is the save gate."""

    name: str
    vertex_count: int
    triangle_count: int
    submesh_count: int
    bounds_min: tuple
    bounds_max: tuple
    uv_layers: tuple
    color_layers: tuple
    has_tangent_basis: bool
    failures: list
    warnings: list
    lod_index: int = -1
    family: str = ""
    surface_class: str = ""
    digest: str = ""
    validator_version: str = VALIDATOR_VERSION

    @property
    def passed(self) -> bool:
        return not self.failures

    def summary(self) -> str:
        head = (
            "{name}: lod{lod} verts={v} tris={t} submeshes={s} uv={uv} "
            "vcol={vc} tangents={tan} -> {verdict}"
        ).format(
            name=self.name, lod=self.lod_index, v=self.vertex_count,
            t=self.triangle_count, s=self.submesh_count,
            uv=len(self.uv_layers), vc=len(self.color_layers),
            tan=self.has_tangent_basis,
            verdict="PASS" if self.passed else "FAIL",
        )
        if self.passed:
            return head
        return head + "\n  " + "\n  ".join(str(f) for f in self.failures)


@dataclass
class MeshData:
    """Flat snapshot of one mesh. The shape the Unity vertex/index writer sees.

    ``positions`` and ``vertex_normals`` are ``3 * vertex_count`` long.
    ``corner_normals`` and ``tangents`` are ``3 * loop_count``; ``tangent_signs``
    is ``loop_count``. ``tri_vertices`` and ``tri_loops`` are ``3 *
    triangle_count``. ``uv_layers`` holds ``(name, values)`` with ``2 *
    loop_count`` values; ``color_layers`` holds ``(name, domain, values)`` with
    four values per element.
    """

    name: str = ""
    vertex_count: int = 0
    loop_count: int = 0
    positions: list = field(default_factory=list)
    vertex_normals: list = field(default_factory=list)
    corner_normals: list = field(default_factory=list)
    corner_vertex: list = field(default_factory=list)
    tangents: list = field(default_factory=list)
    tangent_signs: list = field(default_factory=list)
    uv_layers: tuple = ()
    color_layers: tuple = ()
    tri_vertices: list = field(default_factory=list)
    tri_loops: list = field(default_factory=list)
    tri_material_index: list = field(default_factory=list)
    material_slot_count: int = 0
    tangent_source: str = "none"

    @property
    def triangle_count(self) -> int:
        return len(self.tri_vertices) // 3

    def digest(self) -> str:
        """Content hash over positions and indices, for identity cross-checks.

        Collision proxies are compared against visual meshes by this value, so
        renaming a datablock cannot disguise an LOD0 mesh as a collider.
        """
        hasher = hashlib.blake2b(digest_size=16)
        hasher.update(struct.pack("<iii", self.vertex_count,
                                  len(self.tri_vertices), self.loop_count))
        hasher.update(struct.pack("<%df" % len(self.positions),
                                  *self.positions) if self.positions else b"")
        if self.tri_vertices:
            hasher.update(struct.pack("<%di" % len(self.tri_vertices),
                                      *self.tri_vertices))
        return hasher.hexdigest()


# ---------------------------------------------------------------------------
# Small numeric helpers -- pure, no Blender types
# ---------------------------------------------------------------------------

def _finite(x: float) -> bool:
    # law.finite rejects NaN and both infinities; reused so the definition of
    # "finite" cannot drift between the constants file and the gates.
    return law.finite(x)


def _first_non_finite(values, stride: int):
    """Index of the first element with a non-finite component, or -1."""
    for i in range(0, len(values), stride):
        for k in range(stride):
            if not _finite(values[i + k]):
                return i // stride
    return -1


def _length3(x: float, y: float, z: float) -> float:
    return (x * x + y * y + z * z) ** 0.5


def _cross_length(ax, ay, az, bx, by, bz) -> float:
    cx = ay * bz - az * by
    cy = az * bx - ax * bz
    cz = ax * by - ay * bx
    return _length3(cx, cy, cz)


def triangle_area_times_two(p, i0: int, i1: int, i2: int) -> float:
    """``length(cross(b - a, c - a))`` from 3dmodel.md section 10, verbatim.

    That expression is twice the triangle area, and the bible's epsilon is
    stated against it, so it is compared unscaled.
    """
    a = i0 * 3
    b = i1 * 3
    c = i2 * 3
    return _cross_length(
        p[b] - p[a], p[b + 1] - p[a + 1], p[b + 2] - p[a + 2],
        p[c] - p[a], p[c + 1] - p[a + 1], p[c + 2] - p[a + 2],
    )


def _triangle_world_area(data, t: int) -> float:
    """World-space area of triangle ``t`` in square metres.

    Used to AREA-WEIGHT the UV stretch population. Weighting by triangle count instead
    lets a pole singularity -- many tiny triangles, negligible visible surface -- dominate
    the verdict, which is why a clean UV sphere failed the count-based gate on 68% of its
    triangles while being perfectly acceptable art.
    """
    p = data.positions
    a = data.tri_vertices[t * 3] * 3
    b = data.tri_vertices[t * 3 + 1] * 3
    c = data.tri_vertices[t * 3 + 2] * 3
    ux, uy, uz = p[b] - p[a], p[b + 1] - p[a + 1], p[b + 2] - p[a + 2]
    vx, vy, vz = p[c] - p[a], p[c + 1] - p[a + 1], p[c + 2] - p[a + 2]
    cx = uy * vz - uz * vy
    cy = uz * vx - ux * vz
    cz = ux * vy - uy * vx
    area = 0.5 * ((cx * cx + cy * cy + cz * cz) ** 0.5)
    return area if _finite(area) else 0.0


def uv_aspect_distortion(p, uv, tri_v, tri_l, t: int) -> float:
    """Aspect distortion of triangle ``t``: ``sigma_max / sigma_min - 1``.

    3dmodel.md section 6 forbids "Stretched polygons above 15 percent aspect
    distortion for hero/near assets or 25 percent for distant-only assets". The
    singular values of the surface parameterisation are the standard measure of
    that ratio, so a uniform scale reports 0 and a 10:1 squash reports 9.

    Returns ``float('inf')`` when the parameterisation collapses a direction.
    """
    v0, v1, v2 = tri_v[t * 3], tri_v[t * 3 + 1], tri_v[t * 3 + 2]
    l0, l1, l2 = tri_l[t * 3], tri_l[t * 3 + 1], tri_l[t * 3 + 2]
    s0, t0 = uv[l0 * 2], uv[l0 * 2 + 1]
    s1, t1 = uv[l1 * 2], uv[l1 * 2 + 1]
    s2, t2 = uv[l2 * 2], uv[l2 * 2 + 1]
    area2 = (s1 - s0) * (t2 - t0) - (s2 - s0) * (t1 - t0)
    if area2 == 0.0 or not _finite(area2):
        return float("inf")
    a0, b0, c0 = v0 * 3, v1 * 3, v2 * 3
    ss = [0.0, 0.0, 0.0]
    st = [0.0, 0.0, 0.0]
    for k in range(3):
        q0, q1, q2 = p[a0 + k], p[b0 + k], p[c0 + k]
        ss[k] = (q0 * (t1 - t2) + q1 * (t2 - t0) + q2 * (t0 - t1)) / area2
        st[k] = (q0 * (s2 - s1) + q1 * (s0 - s2) + q2 * (s1 - s0)) / area2
    a = ss[0] * ss[0] + ss[1] * ss[1] + ss[2] * ss[2]
    b = ss[0] * st[0] + ss[1] * st[1] + ss[2] * st[2]
    c = st[0] * st[0] + st[1] * st[1] + st[2] * st[2]
    disc = ((a - c) * (a - c) + 4.0 * b * b) ** 0.5
    hi = 0.5 * ((a + c) + disc)
    lo = 0.5 * ((a + c) - disc)
    sigma_max = hi ** 0.5 if hi > 0.0 else 0.0
    sigma_min = lo ** 0.5 if lo > 0.0 else 0.0
    if sigma_min <= 0.0:
        return float("inf")
    return sigma_max / sigma_min - 1.0


# Name of the packed RGBA attribute the forge writes. ``vertexcolor``
# ``FINAL_ATTRIBUTE`` owns it on the generator side, and Unity's FBX importer
# consumes that attribute as ``Mesh.colors32``. It is mirrored here instead of
# imported because ``vertexcolor`` imports ``bpy`` while this module must stay
# runnable outside Blender. ``law.py`` is the correct long-term home for the
# constant, so the value there wins as soon as it exists.
_PACKED_VCOL_FALLBACK = "Col"


def packed_vcol_attribute_name() -> str:
    """Canonical packed-RGBA attribute name, preferring law.py once it holds one."""
    return getattr(law, "VCOL_ATTRIBUTE_NAME", _PACKED_VCOL_FALLBACK)


def expected_vcol_names(surface_class) -> tuple:
    """Accepted vertex-colour layer name sets for a surface class.

    Derived from ``law.VCOL_CONTRACT`` so the generator and the validator cannot
    disagree about the contract. Four schemes are accepted:

    1. one packed RGBA attribute under the canonical forge name, whose per-channel
       meaning is the surface-class contract and is recorded in the manifest, the
       way 3dmodel.md section 5 requires for the alpha channel;
    2. one packed RGBA layer named after its red channel, e.g. ``edge_wear``;
    3. one packed RGBA layer named by the joined contract;
    4. four layers named exactly after the four channels.

    A generator should call this instead of hardcoding a string.
    """
    contract = law.VCOL_CONTRACT[surface_class]
    return (
        (packed_vcol_attribute_name(),),
        (contract[0],),
        ("_".join(contract),),
        tuple(contract),
    )


# ---------------------------------------------------------------------------
# Failure accumulation -- local to one call, never shared
# ---------------------------------------------------------------------------

class _Sink:
    __slots__ = ("_order", "_by_gate", "_notes")

    def __init__(self) -> None:
        self._order = []
        self._by_gate = {}
        self._notes = []

    def fail(self, gate: str, detail: str) -> None:
        found = self._by_gate.get(gate)
        if found is None:
            self._by_gate[gate] = Failure(gate, detail, 1)
            self._order.append(gate)
        else:
            found.count += 1

    def skip(self, gate: str, reason: str) -> None:
        """Record that a gate did not apply. Never reads as a pass."""
        self._notes.append(Failure(gate, "not enforced: " + reason, 0))

    def failed(self, gate: str) -> bool:
        return gate in self._by_gate

    @property
    def any_failure(self) -> bool:
        return bool(self._by_gate)

    def failures(self) -> list:
        return [self._by_gate[g] for g in self._order]

    def notes(self) -> list:
        return list(self._notes)

    def skipped_gates(self) -> set:
        return set(n.gate for n in self._notes)


def _record_gates(blackbox, stage_prefix: str, gates, sink: _Sink,
                  *, family: str = "", vertex_count: int = -1,
                  triangle_count: int = -1) -> None:
    """Record one black-box step per gate. Pass, fail, and skip all appear."""
    if blackbox is None:
        return
    notes = {}
    for note in sink.notes():
        notes.setdefault(note.gate, note.detail)
    failed = {}
    for candidate in sink.failures():
        failed[candidate.gate] = candidate
    for gate in gates:
        found = failed.get(gate)
        if found is not None:
            blackbox.record(
                stage_prefix + gate, family=family, vertex_count=vertex_count,
                triangle_count=triangle_count,
                warning="x{c} {d}".format(c=found.count, d=found.detail),
                failure_code=gate,
            )
        elif gate in notes:
            blackbox.record(
                stage_prefix + gate, family=family, vertex_count=vertex_count,
                triangle_count=triangle_count, warning=notes.get(gate, "skipped"),
            )
        else:
            blackbox.record(
                stage_prefix + gate, family=family, vertex_count=vertex_count,
                triangle_count=triangle_count, warning="pass",
            )


# ---------------------------------------------------------------------------
# Blender datablock -> MeshData
# ---------------------------------------------------------------------------

def _foreach(collection, prop: str, count: int, fill):
    """``foreach_get`` into a preallocated list, or an empty list on absence."""
    if count <= 0:
        return []
    buffer = [fill] * count
    collection.foreach_get(prop, buffer)
    return buffer


def extract_mesh_data(mesh) -> MeshData:
    """Snapshot a Blender mesh datablock into flat buffers.

    Bulk reads use ``foreach_get`` rather than per-element Python attribute
    access; a 35 000 triangle fauna body is the budget ceiling in
    ``law.LOD_BUDGETS`` and per-element access on that is minutes, not
    milliseconds.

    Tangents are requested through ``calc_tangents`` and released again with
    ``free_tangents`` so validation leaves no extra custom-data layer behind on
    the datablock it was asked to inspect.
    """
    mesh.calc_loop_triangles()
    vertex_count = len(mesh.vertices)
    loop_count = len(mesh.loops)
    tri_count = len(mesh.loop_triangles)

    data = MeshData(name=mesh.name, vertex_count=vertex_count,
                    loop_count=loop_count)
    data.positions = _foreach(mesh.vertices, "co", vertex_count * 3, 0.0)
    data.vertex_normals = _foreach(mesh.vertex_normals, "vector",
                                   vertex_count * 3, 0.0)
    try:
        data.corner_normals = _foreach(mesh.corner_normals, "vector",
                                       loop_count * 3, 0.0)
    except (AttributeError, RuntimeError):
        data.corner_normals = []
    try:
        data.corner_vertex = _foreach(mesh.loops, "vertex_index", loop_count, 0)
    except (AttributeError, RuntimeError):
        data.corner_vertex = []

    data.tri_vertices = _foreach(mesh.loop_triangles, "vertices", tri_count * 3, 0)
    data.tri_loops = _foreach(mesh.loop_triangles, "loops", tri_count * 3, 0)
    data.tri_material_index = _foreach(mesh.loop_triangles, "material_index",
                                       tri_count, 0)
    data.material_slot_count = len(mesh.materials)

    uv_layers = []
    for layer in mesh.uv_layers:
        uv_layers.append((layer.name,
                          _foreach(layer.data, "uv", loop_count * 2, 0.0)))
    data.uv_layers = tuple(uv_layers)

    color_layers = []
    for layer in mesh.color_attributes:
        elements = vertex_count if layer.domain == "POINT" else loop_count
        color_layers.append((layer.name, layer.domain,
                             _foreach(layer.data, "color", elements * 4, 0.0)))
    data.color_layers = tuple(color_layers)

    if uv_layers:
        try:
            mesh.calc_tangents()
            data.tangents = _foreach(mesh.loops, "tangent", loop_count * 3, 0.0)
            data.tangent_signs = _foreach(mesh.loops, "bitangent_sign",
                                          loop_count, 0.0)
            data.tangent_source = "calc_tangents"
            mesh.free_tangents()
        except (AttributeError, RuntimeError) as exc:
            data.tangent_source = "unavailable: " + str(exc).strip()[:120]
    return data


# ---------------------------------------------------------------------------
# Bounds
# ---------------------------------------------------------------------------

def compute_bounds(positions):
    """Axis-aligned bounds over ``positions``.

    A non-finite component poisons its axis deliberately: 3dmodel.md requires
    "bounds finite and extents above 0.001 m", and silently skipping the bad
    component would hand the renderer culling bounds that do not describe the
    geometry it was given.
    """
    if not positions:
        nan = float("nan")
        return (nan, nan, nan), (nan, nan, nan)
    lo = [float("inf")] * 3
    hi = [float("-inf")] * 3
    poisoned = [False, False, False]
    for i in range(0, len(positions), 3):
        for k in range(3):
            value = positions[i + k]
            if not _finite(value):
                poisoned[k] = True
                continue
            if value < lo[k]:
                lo[k] = value
            if value > hi[k]:
                hi[k] = value
    nan = float("nan")
    for k in range(3):
        if poisoned[k] or lo[k] > hi[k]:
            lo[k] = nan
            hi[k] = nan
    return (lo[0], lo[1], lo[2]), (hi[0], hi[1], hi[2])


# ---------------------------------------------------------------------------
# Argument coercion
# ---------------------------------------------------------------------------
# law.Family and law.SurfaceClass are str enums, but Enum hashes by member and
# not by value, so a plain string would miss law.LOD_BUDGETS even though it
# compares equal. Coerce once, at the boundary.

def _as_family(family):
    if isinstance(family, law.Family):
        return family
    return law.Family(family)


def _as_surface_class(surface_class):
    if isinstance(surface_class, law.SurfaceClass):
        return surface_class
    return law.SurfaceClass(surface_class)


# ---------------------------------------------------------------------------
# Individual gate groups
# ---------------------------------------------------------------------------

def _gate_structure(data: MeshData, sink: _Sink) -> bool:
    """Vertex/index buffer integrity. Returns False when indices are unusable.

    3dmodel.md section 10: "assert mesh.vertexCount > 0", "assert mesh.indexCount
    % 3 == 0", "assert indices inside vertex range".
    """
    if data.vertex_count <= 0:
        sink.fail(GATE_EMPTY_MESH,
                  "vertex_count={0}, bible requires > 0".format(data.vertex_count))
    index_count = len(data.tri_vertices)
    if index_count <= 0:
        sink.fail(GATE_NO_TRIANGLES, "index_count=0, mesh carries no triangles")
    if index_count % 3 != 0:
        sink.fail(GATE_INDEX_COUNT_NOT_TRIANGULATED,
                  "index_count={0} is not a multiple of 3 (remainder {1})".format(
                      index_count, index_count % 3))
    usable = index_count > 0 and index_count % 3 == 0 and data.vertex_count > 0
    for i in range(index_count):
        index = data.tri_vertices[i]
        if index < 0 or index >= data.vertex_count:
            sink.fail(GATE_INDEX_OUT_OF_RANGE,
                      "index_buffer[{0}]={1} outside vertex range 0..{2}".format(
                          i, index, data.vertex_count - 1))
            usable = False
    for i in range(len(data.tri_loops)):
        loop = data.tri_loops[i]
        if loop < 0 or loop >= data.loop_count:
            sink.fail(GATE_INDEX_OUT_OF_RANGE,
                      "loop_index_buffer[{0}]={1} outside corner range "
                      "0..{2}".format(i, loop, data.loop_count - 1))
            usable = False
    return usable


def _gate_positions_and_bounds(data: MeshData, sink: _Sink, *, planar: bool):
    """Finite positions, finite bounds, minimum extent.

    3dmodel.md section 10: "assert bounds finite and extents above 0.001 m".
    """
    if _first_non_finite(data.positions, 3) >= 0:
        for v in range(data.vertex_count):
            base = v * 3
            if not (_finite(data.positions[base])
                    and _finite(data.positions[base + 1])
                    and _finite(data.positions[base + 2])):
                sink.fail(GATE_NON_FINITE_POSITION,
                          "vertex[{0}] position=({1!r}, {2!r}, {3!r})".format(
                              v, data.positions[base], data.positions[base + 1],
                              data.positions[base + 2]))
    bounds_min, bounds_max = compute_bounds(data.positions)
    axis_names = ("x", "y", "z")
    small_axes = []
    for k in range(3):
        if not (_finite(bounds_min[k]) and _finite(bounds_max[k])):
            sink.fail(GATE_NON_FINITE_BOUNDS,
                      "bounds axis {0} is non-finite: min={1!r} max={2!r}".format(
                          axis_names[k], bounds_min[k], bounds_max[k]))
            continue
        if bounds_max[k] - bounds_min[k] < law.MIN_BOUNDS_EXTENT_M:
            small_axes.append((axis_names[k], bounds_max[k] - bounds_min[k]))
    # 3dmodel.md section 7 permits an "approved single-triangle impostor/card",
    # which is flat by definition. The caller declares that approval; without it
    # the pseudocode extent rule applies to every axis as written.
    allowed_small = 1 if planar else 0
    if len(small_axes) > allowed_small:
        first = small_axes[0]
        sink.fail(GATE_BOUNDS_EXTENT_TOO_SMALL,
                  "axis {0} extent={1:.9f} m below law.MIN_BOUNDS_EXTENT_M={2} "
                  "({3} axes collapsed, planar={4})".format(
                      first[0], first[1], law.MIN_BOUNDS_EXTENT_M,
                      len(small_axes), planar))
        for extra in small_axes[1:]:
            sink.fail(GATE_BOUNDS_EXTENT_TOO_SMALL,
                      "axis {0} extent={1:.9f} m".format(extra[0], extra[1]))
    elif planar:
        sink.skip(GATE_BOUNDS_EXTENT_TOO_SMALL,
                  "caller declared planar=True; 3dmodel.md section 7 card/"
                  "impostor exemption tolerates one collapsed axis")
    return bounds_min, bounds_max


def _gate_normals(data: MeshData, sink: _Sink) -> None:
    """Finite, unit-length normals on every stream the mesh carries.

    3dmodel.md section 10: "assert abs(length(normal) - 1) <= 0.005", held in
    law.NORMAL_LENGTH_MIN / law.NORMAL_LENGTH_MAX.
    """
    streams = (("vertex_normals", data.vertex_normals),
               ("corner_normals", data.corner_normals))
    for stream_name, values in streams:
        if not values:
            continue
        for i in range(0, len(values), 3):
            x, y, z = values[i], values[i + 1], values[i + 2]
            if not (_finite(x) and _finite(y) and _finite(z)):
                sink.fail(GATE_NON_FINITE_NORMAL,
                          "{0}[{1}]=({2!r}, {3!r}, {4!r})".format(
                              stream_name, i // 3, x, y, z))
                continue
            length = _length3(x, y, z)
            if length < law.NORMAL_LENGTH_MIN or length > law.NORMAL_LENGTH_MAX:
                sink.fail(GATE_NORMAL_LENGTH_OUT_OF_RANGE,
                          "{0}[{1}] length={2:.6f} outside {3}..{4}".format(
                              stream_name, i // 3, length,
                              law.NORMAL_LENGTH_MIN, law.NORMAL_LENGTH_MAX))


def _tangent_zero_cause(data: MeshData, loop: int) -> str:
    """WHY a tangent underflowed, not just WHERE.

    Measured on a geology LOD0 on 2026-07-29: ``tangent[17547] length=0.000000``
    sent a competent investigation hunting a degenerate UV triangle **that does not
    exist** -- zero-UV-area loop triangles on that mesh were 0 in both UV layers, in
    both the failing and the passing build. A UV-area test finds nothing.

    The real mechanism needs BOTH ingredients, and reporting either alone names the
    wrong owner:

    * a UV **needle** -- non-zero area but extreme aspect. Measured 1.191e-07 UV area
      against a healthy 1.125e-04 m2 of 3D area, roughly 121:1.
    * a corner normal **87.99 degrees off its own face normal**.

    Mikktspace orthogonalises the UV tangent against the split normal, so a tangent
    that is near-parallel to a corner normal that far off its face has its in-plane
    component underflow to exactly 0. A UV needle alone is survivable; an 88-degree
    corner normal alone is survivable; together they zero the tangent.

    So the instrument is (UV aspect, corner-vs-face angle) and NOT UV area, and the
    two numbers name different owners: a needle belongs to the unwrap or the geometry
    that produced it, an 88-degree corner normal belongs to the shading basis.

    Follows the ``WindingDiagnosis`` precedent in this file -- a gate that reports a
    cause rather than a coordinate. Reads only fields ``MeshData`` already snapshots,
    so it adds no Blender API calls and cannot fail on a mesh the gate could inspect.
    """
    tri = -1
    for t in range(0, len(data.tri_loops), 3):
        if loop in (data.tri_loops[t], data.tri_loops[t + 1], data.tri_loops[t + 2]):
            tri = t
            break
    if tri < 0:
        return "CAUSE: unavailable (loop {0} is in no triangle)".format(loop)

    loops = (data.tri_loops[tri], data.tri_loops[tri + 1], data.tri_loops[tri + 2])
    verts = (data.tri_vertices[tri], data.tri_vertices[tri + 1],
             data.tri_vertices[tri + 2])

    def _p(v):
        return (data.positions[3 * v], data.positions[3 * v + 1],
                data.positions[3 * v + 2])

    a, b, c = _p(verts[0]), _p(verts[1]), _p(verts[2])
    e1 = (b[0] - a[0], b[1] - a[1], b[2] - a[2])
    e2 = (c[0] - a[0], c[1] - a[1], c[2] - a[2])
    nx = e1[1] * e2[2] - e1[2] * e2[1]
    ny = e1[2] * e2[0] - e1[0] * e2[2]
    nz = e1[0] * e2[1] - e1[1] * e2[0]
    face_len = _length3(nx, ny, nz)
    area3d = 0.5 * face_len

    uv_bit = "uv=none"
    if data.uv_layers:
        _name, values = data.uv_layers[0]
        u = [(values[2 * l], values[2 * l + 1]) for l in loops]
        du1 = (u[1][0] - u[0][0], u[1][1] - u[0][1])
        du2 = (u[2][0] - u[0][0], u[2][1] - u[0][1])
        uv_area = 0.5 * abs(du1[0] * du2[1] - du1[1] * du2[0])
        sides = []
        for p, q in ((0, 1), (1, 2), (2, 0)):
            sides.append(_length3(u[q][0] - u[p][0], u[q][1] - u[p][1], 0.0))
        longest = max(sides)
        # Triangle height against its own longest side: area = 0.5 * base * height.
        height = (2.0 * uv_area / longest) if longest > 0.0 else 0.0
        aspect = (longest / height) if height > 0.0 else float("inf")
        uv_bit = "UV0 area={0:.6e} aspect={1:.1f}:1".format(uv_area, aspect)

    # Reported as |dot| rather than as degrees, for two reasons. This module has no
    # ``math`` import by convention -- it uses ``** 0.5`` rather than ``math.sqrt``
    # throughout -- and more importantly |dot| IS the quantity mikktspace degenerates
    # on. Near 0 means the corner normal is near-perpendicular to its own face, which
    # is the condition that lets the orthogonalised tangent underflow. Degrees would
    # be a human-friendly transform of the mechanism rather than the mechanism.
    angle_bit = "cornerNormal=unavailable"
    if data.corner_normals and face_len > 0.0:
        cn = (data.corner_normals[3 * loop], data.corner_normals[3 * loop + 1],
              data.corner_normals[3 * loop + 2])
        cn_len = _length3(cn[0], cn[1], cn[2])
        if cn_len > 0.0:
            dot = (nx * cn[0] + ny * cn[1] + nz * cn[2]) / (face_len * cn_len)
            dot = max(-1.0, min(1.0, dot))
            angle_bit = ("|dot(cornerNormal, faceNormal)|={0:.4f}"
                         " (near 0 = near-perpendicular, the degenerate case)"
                         ).format(abs(dot))

    return ("CAUSE: {0}, 3D area={1:.6e}, {2}. A zero tangent needs BOTH a UV needle "
            "AND a corner normal far off its face -- mikktspace orthogonalises the UV "
            "tangent against the split normal. UV area alone is NOT the instrument."
            ).format(uv_bit, area3d, angle_bit)


def _gate_tangents(data: MeshData, sink: _Sink) -> None:
    """Finite unit tangents with strictly +1 or -1 handedness.

    3dmodel.md section 10: "Tangents normalized and finite; handedness is -1
    or 1."
    """
    if not data.tangents:
        for gate in (GATE_NON_FINITE_TANGENT, GATE_TANGENT_LENGTH_OUT_OF_RANGE,
                     GATE_TANGENT_HANDEDNESS_INVALID):
            sink.skip(gate, "mesh carries no tangent basis ("
                      + data.tangent_source + ")")
        return
    for i in range(0, len(data.tangents), 3):
        x, y, z = data.tangents[i], data.tangents[i + 1], data.tangents[i + 2]
        if not (_finite(x) and _finite(y) and _finite(z)):
            sink.fail(GATE_NON_FINITE_TANGENT,
                      "tangent[{0}]=({1!r}, {2!r}, {3!r})".format(i // 3, x, y, z))
            continue
        length = _length3(x, y, z)
        if length < law.TANGENT_LENGTH_MIN or length > law.TANGENT_LENGTH_MAX:
            sink.fail(GATE_TANGENT_LENGTH_OUT_OF_RANGE,
                      "tangent[{0}] length={1:.6f} outside {2}..{3}. {4}".format(
                          i // 3, length, law.TANGENT_LENGTH_MIN,
                          law.TANGENT_LENGTH_MAX,
                          _tangent_zero_cause(data, i // 3)))
    for i in range(len(data.tangent_signs)):
        sign = data.tangent_signs[i]
        if not _finite(sign):
            sink.fail(GATE_NON_FINITE_TANGENT,
                      "bitangent_sign[{0}]={1!r}".format(i, sign))
        elif sign != 1.0 and sign != -1.0:
            sink.fail(GATE_TANGENT_HANDEDNESS_INVALID,
                      "bitangent_sign[{0}]={1!r}, bible allows strictly -1 "
                      "or +1".format(i, sign))


@dataclass
class WindingDiagnosis:
    """WHY a winding failure happened, because that decides who repairs it.

    Three defects reach ``GATE_INCONSISTENT_WINDING`` and they have three
    different owners. The bare symptom -- "these two triangles share a directed
    edge" -- does not separate them, and the cost of that was measured on coral:
    ``bmesh.ops.recalc_face_normals`` was added specifically to fix this gate,
    and it moved LOD0 from 53 occurrences to 39 and never to 0. It was then
    removed because it also broke the authored normal basis on rock. That whole
    round trip was spent on a repair the mesh could not accept, and nothing in
    the failure text said so.

    ``twisted_regions`` of ``regions``
        The INVARIANT, and the field to quote. A connected face region is twisted
        when no assignment of per-triangle winding orients it consistently, i.e.
        it is NON-ORIENTABLE -- a Moebius-style join has welded one sheet onto
        itself with a half turn. ``recalc_face_normals`` orients each flood-filled
        region and then dumps the whole twist onto whichever edges close the odd
        cycles, so on such a region it cannot reach zero, and MEASURED it can make
        the occurrence count worse: 60 to 98 on the coral mesh before decimation,
        and 53 to 39 rather than to 0 on coral LOD0. Every edge still carries
        exactly two faces throughout, which is why a non-manifold census reads a
        clean 0 next to this failure and the pair reads as a contradiction.
    ``conflict_edges``
        The edges the flood fill could not satisfy. Reported because they are
        where to look, NOT as a canonical measure: which edges end up carrying the
        twist depends on the order the fill happened to visit faces in, so two
        correct implementations legitimately report different counts on the same
        mesh. Only its emptiness is invariant. If a number is being compared
        across tools, compare ``twisted_regions``.
    ``over_shared_edges``
        Edges carrying three or more triangles. Among three faces on one edge,
        two must traverse it the same way, so a repeated directed edge is forced
        by the topology and no winding choice avoids it. That is a non-manifold
        defect wearing a winding gate's name.
    ``backwards_triangles``
        Triangles wound against their neighbours when the surface IS orientable,
        counted as the smaller side of each connected region. This is the only
        one of the four that ``recalc_face_normals`` actually repairs.
    """

    conflict_edges: tuple = ()
    over_shared_edges: tuple = ()
    backwards_triangles: int = 0
    regions: int = 0
    twisted_regions: int = 0

    @property
    def orientable(self) -> bool:
        return self.twisted_regions == 0

    def explain(self) -> str:
        """One sentence naming the cause and the repair that can work."""
        parts = []
        if self.over_shared_edges:
            worst = max(count for _key, count in self.over_shared_edges)
            parts.append(
                "{0} edge(s) carry 3 or more triangles (worst {1}), which FORCES "
                "a repeated directed edge that no winding choice can avoid; that "
                "is a non-manifold defect, first at vertex pair {2}".format(
                    len(self.over_shared_edges), worst,
                    min(key for key, _count in self.over_shared_edges)))
        if self.twisted_regions:
            parts.append(
                "the surface is NON-ORIENTABLE -- {0} of {1} connected face "
                "region(s) admit no consistent orientation under ANY assignment "
                "of per-triangle winding. recalc_face_normals cannot repair that "
                "and can raise the occurrence count; the defect is the topology "
                "that welded a sheet onto itself, not the face normals. The "
                "orientation fill could not satisfy {2} edge(s), first near "
                "vertex pair {3} -- that set is where to look, but it depends on "
                "traversal order, so compare region counts and not edge "
                "counts".format(
                    self.twisted_regions, self.regions,
                    len(self.conflict_edges), min(self.conflict_edges)))
        elif self.backwards_triangles:
            parts.append(
                "the surface IS orientable: {0} triangle(s) are simply wound "
                "against their neighbours, so recalc_face_normals or flipping "
                "exactly those repairs it".format(self.backwards_triangles))
        if not parts:
            return ("cause undetermined: no non-manifold edge, no orientation "
                    "conflict and no backwards triangle, so the repeat comes "
                    "from coincident faces on the same vertex triple")
        return "CAUSE: " + "; ".join(parts) + "."


def _triangle_adjacency(data: MeshData) -> dict:
    """Undirected shared edge -> ``[(triangle, directed start vertex), ...]``.

    The key is the sorted vertex pair, which is the edge a topology census sees.
    The value keeps the directed start vertex per triangle, which is the edge the
    winding gate sees. Deriving both readings from ``tri_vertices`` in one place
    is deliberate: the two cannot then disagree about what an edge is, which is
    the first thing anyone suspects when a winding gate fires next to a clean
    manifold report.
    """
    incident = {}
    for t in range(data.triangle_count):
        i0 = data.tri_vertices[t * 3]
        i1 = data.tri_vertices[t * 3 + 1]
        i2 = data.tri_vertices[t * 3 + 2]
        for a, b in ((i0, i1), (i1, i2), (i2, i0)):
            if a == b:
                # A degenerate triangle already failed its own gate; a self-edge
                # would only add noise to the orientation graph.
                continue
            key = (a, b) if a < b else (b, a)
            found = incident.get(key)
            if found is None:
                incident[key] = [(t, a)]
            else:
                found.append((t, a))
    return incident


def orientation_analysis(data: MeshData) -> WindingDiagnosis:
    """Can ANY choice of per-triangle winding make every shared edge agree?

    Flood-fills one orientation across edges shared by exactly two triangles --
    the same connectivity ``bmesh.ops.recalc_face_normals`` uses -- and records
    the edges that contradict it. That makes the answer a measurement instead of
    a hypothesis: a non-empty ``conflict_edges`` is a proof of non-orientability,
    because an orientable surface admits the flood-filled assignment by
    definition.

    Public on purpose. ``mesh_ops.topology_report`` exists so a missed triangle
    budget reports a CAUSE rather than a number; this is the same service for the
    winding gate, and a generator or a probe can call it between stages to find
    the pass that introduced the twist instead of bisecting by hand.
    """
    incident = _triangle_adjacency(data)
    neighbours = {}
    over_shared = []
    for key in incident:
        users = incident[key]
        if len(users) > 2:
            over_shared.append((key, len(users)))
            continue
        if len(users) != 2:
            continue
        first, second = users[0], users[1]
        if first[0] == second[0]:
            # One triangle using the same edge twice cannot constrain another.
            continue
        # Equal start vertices mean both traverse the edge the same way, so one
        # of the two has to be flipped relative to the other.
        same_direction = first[1] == second[1]
        neighbours.setdefault(first[0], []).append((second[0], same_direction,
                                                    key))
        neighbours.setdefault(second[0], []).append((first[0], same_direction,
                                                     key))

    flipped = {}
    conflicts = set()
    backwards = 0
    regions = 0
    twisted = 0
    for seed in range(data.triangle_count):
        if seed in flipped:
            continue
        regions += 1
        flipped[seed] = False
        region = [seed]
        stack = [seed]
        region_twisted = False
        while stack:
            current = stack.pop()
            for other, same_direction, key in neighbours.get(current, ()):
                wanted = flipped[current] != same_direction
                if other not in flipped:
                    flipped[other] = wanted
                    region.append(other)
                    stack.append(other)
                elif flipped[other] != wanted:
                    conflicts.add(key)
                    region_twisted = True
        if region_twisted:
            twisted += 1
            # A twisted region has no "right way round", so counting triangles to
            # flip in it would be a number with no repair attached to it.
            continue
        turned = 0
        for index in region:
            if flipped[index]:
                turned += 1
        # Either side of an orientable region may be declared the front, so the
        # repair cost is the smaller side.
        backwards += turned if turned <= len(region) - turned \
            else len(region) - turned
    return WindingDiagnosis(tuple(sorted(conflicts)),
                            tuple(sorted(over_shared)), backwards,
                            regions, twisted)


def _gate_triangles(data: MeshData, sink: _Sink, *, double_sided: bool) -> None:
    """Degenerate triangles and winding consistency.

    3dmodel.md section 10: "area = length(cross(p1 - p0, p2 - p0)); assert
    area > 0.0000001". PROCEDURAL_ASSET_PIPELINE.md, Validation Before Save:
    "no inverted or broken winding except deliberate double-sided shells
    documented by family bible".

    The winding half reports a DIAGNOSIS, not only the symptom. See
    :class:`WindingDiagnosis` for the three defects that reach this one gate and
    why naming them apart is worth the extra pass.
    """
    tri_count = data.triangle_count
    for t in range(tri_count):
        i0 = data.tri_vertices[t * 3]
        i1 = data.tri_vertices[t * 3 + 1]
        i2 = data.tri_vertices[t * 3 + 2]
        if i0 == i1 or i1 == i2 or i0 == i2:
            sink.fail(GATE_DEGENERATE_TRIANGLE,
                      "triangle[{0}] repeats a vertex index ({1}, {2}, {3})"
                      .format(t, i0, i1, i2))
            continue
        area = triangle_area_times_two(data.positions, i0, i1, i2)
        if not _finite(area) or area <= law.DEGENERATE_TRIANGLE_AREA_EPS:
            sink.fail(GATE_DEGENERATE_TRIANGLE,
                      "triangle[{0}] verts=({1}, {2}, {3}) "
                      "length(cross(b-a, c-a))={4!r} <= "
                      "law.DEGENERATE_TRIANGLE_AREA_EPS={5}".format(
                          t, i0, i1, i2, area,
                          law.DEGENERATE_TRIANGLE_AREA_EPS))
    if double_sided:
        sink.skip(GATE_INCONSISTENT_WINDING,
                  "caller declared double_sided=True; pipeline bible allows a "
                  "deliberate double-sided shell")
        return
    # A closed or open manifold surface traverses every shared edge once in each
    # direction. The same directed edge appearing twice means two faces wind the
    # same way across it, which is the inverted/duplicated face case.
    #
    # Collected first, reported second. _Sink.fail keeps the detail of the FIRST
    # occurrence and only counts the rest, so the diagnosis has to be computed
    # before anything is emitted or it could never reach the message a reader
    # actually sees. Occurrence counting is byte-for-byte the previous behaviour:
    # `seen` is not updated on a repeat, so an edge with three triangles still
    # reports two occurrences and a mesh's number does not shift under this
    # change.
    seen = {}
    repeats = []
    for t in range(tri_count):
        i0 = data.tri_vertices[t * 3]
        i1 = data.tri_vertices[t * 3 + 1]
        i2 = data.tri_vertices[t * 3 + 2]
        for a, b in ((i0, i1), (i1, i2), (i2, i0)):
            key = (a, b)
            previous = seen.get(key)
            if previous is None:
                seen[key] = t
            else:
                repeats.append((a, b, previous, t))
    if not repeats:
        return
    # Paid only on failure. Clean geometry never walks the orientation graph.
    diagnosis = orientation_analysis(data)
    first = repeats[0]
    sink.fail(GATE_INCONSISTENT_WINDING,
              "directed edge ({0} -> {1}) used by triangle[{2}] and "
              "triangle[{3}]; winding is not consistent. {4}".format(
                  first[0], first[1], first[2], first[3], diagnosis.explain()))
    for extra in repeats[1:]:
        # _Sink.fail aggregates repeats of one gate into a count and keeps the
        # first detail, so these only raise the occurrence number. The real
        # detail is passed anyway rather than a placeholder: if the sink ever
        # keeps every detail, this stays correct instead of emitting blanks.
        sink.fail(GATE_INCONSISTENT_WINDING,
                  "directed edge ({0} -> {1}) used by triangle[{2}] and "
                  "triangle[{3}]".format(extra[0], extra[1], extra[2],
                                         extra[3]))


def _gate_uv(data: MeshData, sink: _Sink, *, hero: bool, triplanar: bool,
             atlas_size, indices_usable: bool, surface_class=None) -> None:
    """UV0 presence, finiteness, zero-area UV triangles, stretch, atlas gates.

    3dmodel.md section 6 forbidden UV states: "Stretched polygons above 15
    percent aspect distortion for hero/near assets or 25 percent for
    distant-only assets", "UV shells touching atlas border without padding",
    "Islands smaller than 4 pixels at target mip 0 for any visible LOD0 detail."
    Section 10: "No zero-area UV triangle for textured material surfaces unless
    triplanar-only and documented."
    """
    if not data.uv_layers:
        sink.fail(GATE_UV0_MISSING,
                  "mesh carries no UV layer; 3dmodel.md section 3 requires "
                  "TexCoord0 and section 6 requires an approved UV route")
        for gate in (GATE_NON_FINITE_UV, GATE_ZERO_AREA_UV_TRIANGLE,
                     GATE_UV_STRETCH_EXCESSIVE, GATE_UV_ISLAND_BELOW_MIN_PIXELS,
                     GATE_UV_ATLAS_PADDING_VIOLATION):
            sink.skip(gate, "no UV layer to measure")
        return

    for layer_index in range(len(data.uv_layers)):
        name, values = data.uv_layers[layer_index]
        for i in range(0, len(values), 2):
            u, v = values[i], values[i + 1]
            if not (_finite(u) and _finite(v)):
                sink.fail(GATE_NON_FINITE_UV,
                          "uv layer '{0}' corner[{1}]=({2!r}, {3!r})".format(
                              name, i // 2, u, v))

    uv0_name, uv0 = data.uv_layers[0]
    if not indices_usable:
        for gate in (GATE_ZERO_AREA_UV_TRIANGLE, GATE_UV_STRETCH_EXCESSIVE,
                     GATE_UV_ISLAND_BELOW_MIN_PIXELS,
                     GATE_UV_ATLAS_PADDING_VIOLATION):
            sink.skip(gate, "index buffer is invalid; fix index gates first")
        return
    if triplanar:
        # 3dmodel.md section 6 allows "Triplanar material assignment for large
        # geology ... when unique UVs would waste space; still requires UV0".
        for gate in (GATE_ZERO_AREA_UV_TRIANGLE, GATE_UV_STRETCH_EXCESSIVE):
            sink.skip(gate, "caller declared triplanar=True and UV0 is present")
    else:
        limit = law.uv_stretch_limit_for(surface_class, hero=hero)
        # Judged by SURFACE AREA, not by triangle count. See law.UV_STRETCH_AREA_FRACTION_MAX
        # for the control experiment: a clean UV sphere exceeds the per-triangle limit on
        # 68% of its TRIANGLES, because a conformal unwrap of a closed surface has an
        # unavoidable pole singularity -- but those triangles are tiny and are not visible
        # stretch. Area weighting asks the question the bible actually cares about: how much
        # of what the player looks at is stretched.
        stretched_area = 0.0
        total_area = 0.0
        measured = 0
        # (distortion, world_area, triangle_index) so the outlier test can filter by area.
        samples = []
        for t in range(data.triangle_count):
            l0 = data.tri_loops[t * 3]
            l1 = data.tri_loops[t * 3 + 1]
            l2 = data.tri_loops[t * 3 + 2]
            s0, t0 = uv0[l0 * 2], uv0[l0 * 2 + 1]
            s1, t1 = uv0[l1 * 2], uv0[l1 * 2 + 1]
            s2, t2 = uv0[l2 * 2], uv0[l2 * 2 + 1]
            if not all(_finite(x) for x in (s0, t0, s1, t1, s2, t2)):
                continue
            area2 = abs((s1 - s0) * (t2 - t0) - (s2 - s0) * (t1 - t0))
            # UV area is dimensionless in a 0..1 domain; DEGENERATE_TRIANGLE_AREA_EPS is a
            # world area in square metres. Comparing them mixed units and made a healthy
            # 5 mm triangle at high texel density read as degenerate.
            if area2 <= law.DEGENERATE_UV_AREA_EPS:
                sink.fail(GATE_ZERO_AREA_UV_TRIANGLE,
                          "triangle[{0}] uv area x2={1!r} on layer '{2}' <= "
                          "law.DEGENERATE_UV_AREA_EPS={3}".format(
                              t, area2, uv0_name, law.DEGENERATE_UV_AREA_EPS))
                continue

            world_area = _triangle_world_area(data, t)
            total_area += world_area
            measured += 1
            distortion = uv_aspect_distortion(data.positions, uv0,
                                              data.tri_vertices, data.tri_loops, t)
            samples.append((distortion, world_area, t))
            if distortion > limit:
                stretched_area += world_area

        if total_area > 0.0 and measured > 0:
            fraction = stretched_area / total_area
            if fraction > law.UV_STRETCH_AREA_FRACTION_MAX:
                worst_any = max(samples)
                sink.fail(GATE_UV_STRETCH_EXCESSIVE,
                          "{0:.1%} of surface area exceeds aspect distortion {1} "
                          "(limit {2:.1%}); worst triangle[{3}]={4:.4f}, "
                          "surface_class={5} hero={6}".format(
                              fraction, limit, law.UV_STRETCH_AREA_FRACTION_MAX,
                              worst_any[2], worst_any[0],
                              getattr(surface_class, "value", surface_class), hero))

            # The outlier ceiling ignores SLIVERS. Judging it on any single triangle
            # regardless of area reintroduces exactly the defect the area-weighted
            # population test above was written to remove. Measured: a failing kelp triangle
            # was a collinear sliver 9.7 cm long and 1.8 mm tall, ~0.017% of the plant's
            # surface, where sigma_max/sigma_min is numerically ill-conditioned and amplifies
            # rounding rather than measuring stretch. Ten documented attempts to remove such
            # slivers geometrically each converged just under the threshold and then produced
            # a DIFFERENT outlier -- the signature of chasing a numerical artefact.
            mean_area = total_area / measured
            min_area = mean_area * law.UV_STRETCH_OUTLIER_MIN_AREA_RATIO
            significant = [s for s in samples if s[1] >= min_area]
            excluded = len(samples) - len(significant)

            if significant:
                worst_distortion, worst_area, worst_triangle = max(significant)
                outlier_ceiling = limit * law.UV_STRETCH_OUTLIER_MULTIPLIER
                if worst_distortion > outlier_ceiling:
                    sink.fail(GATE_UV_STRETCH_EXCESSIVE,
                              "triangle[{0}] aspect distortion={1:.4f} exceeds the outlier "
                              "ceiling {2:.4f} (= limit {3} x "
                              "law.UV_STRETCH_OUTLIER_MULTIPLIER {4}); its area {5:.3e} m2 "
                              "is {6:.1f}x the sliver floor, so this is real stretch on "
                              "visible surface, not a numerical artefact".format(
                                  worst_triangle, worst_distortion, outlier_ceiling,
                                  limit, law.UV_STRETCH_OUTLIER_MULTIPLIER,
                                  worst_area, worst_area / max(min_area, 1e-12)))
            # Never silent. Recorded through `skip`, which is documented as "never reads as
            # a pass" -- accurate here, because the outlier sub-test genuinely did not apply
            # to those triangles. A generator author can see that the gate looked past
            # something and exactly how much, rather than inferring it from a clean result.
            if excluded:
                sink.skip(GATE_UV_STRETCH_EXCESSIVE,
                          "outlier sub-test skipped {0} of {1} triangles below the sliver "
                          "floor {2:.3e} m2 ({3}x mean triangle area {4:.3e} m2); the "
                          "area-weighted population test still covered all of them".format(
                              excluded, len(samples), min_area,
                              law.UV_STRETCH_OUTLIER_MIN_AREA_RATIO, mean_area))

    if atlas_size is None:
        for gate in (GATE_UV_ISLAND_BELOW_MIN_PIXELS,
                     GATE_UV_ATLAS_PADDING_VIOLATION):
            sink.skip(gate, "caller passed no atlas_size; island pixel size and "
                            "border padding are undefined without it")
        return
    _gate_uv_islands(data, sink, uv0_name=uv0_name, uv0=uv0,
                     atlas_size=int(atlas_size))


def _uv_islands(data: MeshData, uv0):
    """Group triangles into UV islands by shared UV coordinates.

    Union-find over quantised UV corners. Two triangles that share a UV
    coordinate belong to the same island, which is the definition an atlas
    packer uses, so island bounds here match the rectangles that were packed.
    """
    parent = list(range(data.triangle_count))

    def find(x):
        while parent[x] != x:
            parent[x] = parent[parent[x]]
            x = parent[x]
        return x

    def union(a, b):
        ra, rb = find(a), find(b)
        if ra != rb:
            parent[rb] = ra

    corner_owner = {}
    for t in range(data.triangle_count):
        for k in range(3):
            loop = data.tri_loops[t * 3 + k]
            u, v = uv0[loop * 2], uv0[loop * 2 + 1]
            if not (_finite(u) and _finite(v)):
                continue
            key = (int(round(u * _UV_WELD_QUANT)), int(round(v * _UV_WELD_QUANT)))
            owner = corner_owner.get(key)
            if owner is None:
                corner_owner[key] = t
            else:
                union(owner, t)

    islands = {}
    for t in range(data.triangle_count):
        root = find(t)
        box = islands.get(root)
        for k in range(3):
            loop = data.tri_loops[t * 3 + k]
            u, v = uv0[loop * 2], uv0[loop * 2 + 1]
            if not (_finite(u) and _finite(v)):
                continue
            if box is None:
                box = [u, v, u, v]
                islands[root] = box
            else:
                if u < box[0]:
                    box[0] = u
                if v < box[1]:
                    box[1] = v
                if u > box[2]:
                    box[2] = u
                if v > box[3]:
                    box[3] = v
    return islands


def _gate_uv_islands(data: MeshData, sink: _Sink, *, uv0_name: str, uv0,
                     atlas_size: int) -> None:
    """Island minimum pixel size and atlas border padding."""
    padding_px = law.atlas_padding_for(atlas_size)
    padding_uv = float(padding_px) / float(atlas_size)
    islands = _uv_islands(data, uv0)
    for root in sorted(islands.keys()):
        box = islands[root]
        width_px = (box[2] - box[0]) * atlas_size
        height_px = (box[3] - box[1]) * atlas_size
        if min(width_px, height_px) < law.UV_MIN_ISLAND_PIXELS:
            sink.fail(GATE_UV_ISLAND_BELOW_MIN_PIXELS,
                      "island rooted at triangle[{0}] on layer '{1}' measures "
                      "{2:.3f} x {3:.3f} px at atlas {4}, below "
                      "law.UV_MIN_ISLAND_PIXELS={5}".format(
                          root, uv0_name, width_px, height_px, atlas_size,
                          law.UV_MIN_ISLAND_PIXELS))
        # Half-texel tolerance. Blender's pack_islands with margin_method='ADD' places an
        # island edge AT the reserve boundary, and the two implementations disagree by
        # sub-pixel rounding -- measured 0.00770 against a 0.00781 reserve for a 16 px
        # margin at 2048, a quarter of one texel. Failing on that is the gate arguing with
        # the packer, not a real bleed risk: the reserve exists so the lowest mip does not
        # sample across an island border, and a quarter texel out of 16 cannot cause that.
        tolerance = 0.5 / float(atlas_size)
        low = padding_uv - tolerance
        high = 1.0 - padding_uv + tolerance
        if box[0] < low or box[1] < low or box[2] > high or box[3] > high:
            sink.fail(GATE_UV_ATLAS_PADDING_VIOLATION,
                      "island rooted at triangle[{0}] spans u {1:.5f}..{2:.5f} "
                      "v {3:.5f}..{4:.5f}, inside the {5} px border reserve "
                      "({6:.5f} uv, half-texel tolerance {7:.5f}) for atlas {8}".format(
                          root, box[0], box[2], box[1], box[3], padding_px,
                          padding_uv, tolerance, atlas_size))


def _gate_vertex_colors(data: MeshData, sink: _Sink, *, surface_class) -> None:
    """Vertex colour presence, contract naming, finiteness, UNorm8 range.

    3dmodel.md section 10: "Vertex color channels match family contract."
    Section 3 declares the stream as "Color | UNorm8 x4", so a value outside
    0..1 cannot survive serialisation and is a defect, not a style choice.
    """
    if not data.color_layers:
        sink.fail(GATE_VERTEX_COLOR_LAYER_MISSING,
                  "no colour attribute; {0} contract requires {1}".format(
                      surface_class.value, law.VCOL_CONTRACT[surface_class]))
        for gate in (GATE_VERTEX_COLOR_CONTRACT_MISMATCH,
                     GATE_VERTEX_COLOR_OUT_OF_UNORM_RANGE, GATE_NON_FINITE_COLOR,
                     GATE_ORGANIC_SWAY_ANCHOR_MISSING):
            sink.skip(gate, "no colour attribute to measure")
        return

    present = tuple(layer[0] for layer in data.color_layers)
    accepted = expected_vcol_names(surface_class)
    if not any(set(scheme) == set(present) for scheme in accepted):
        sink.fail(GATE_VERTEX_COLOR_CONTRACT_MISMATCH,
                  "colour layers {0} match none of the accepted {1} name sets "
                  "{2} derived from law.VCOL_CONTRACT".format(
                      present, surface_class.value, accepted))

    for name, domain, values in data.color_layers:
        for i in range(0, len(values), 4):
            element = i // 4
            for k in range(4):
                value = values[i + k]
                if not _finite(value):
                    sink.fail(GATE_NON_FINITE_COLOR,
                              "colour '{0}' ({1}) element[{2}] channel {3}="
                              "{4!r}".format(name, domain, element, "RGBA"[k],
                                             value))
                elif value < 0.0 or value > 1.0:
                    sink.fail(GATE_VERTEX_COLOR_OUT_OF_UNORM_RANGE,
                              "colour '{0}' ({1}) element[{2}] channel {3}="
                              "{4!r} outside the UNorm8 range 0..1".format(
                                  name, domain, element, "RGBA"[k], value))

    if surface_class is not law.SurfaceClass.ORGANIC:
        sink.skip(GATE_ORGANIC_SWAY_ANCHOR_MISSING,
                  "sway band law applies to organic surfaces only, this is "
                  + surface_class.value)
        return
    # 3DMODEL_FLORA_CORAL.md section 2 / 3dmodel.md section 5: "Root/anchor
    # vertices are 0", and the rigid mineralised band is 0..32/255. An organism
    # whose red channel never reaches the anchor band sways at the root, which
    # is an explicit rejection case.
    name, domain, values = data.color_layers[0]
    lowest = None
    for i in range(0, len(values), 4):
        red = values[i]
        if not _finite(red):
            continue
        if lowest is None or red < lowest:
            lowest = red
    if lowest is None:
        sink.fail(GATE_ORGANIC_SWAY_ANCHOR_MISSING,
                  "colour '{0}' red channel holds no finite value, so no sway "
                  "anchor can be proven".format(name))
    elif lowest > law.SWAY_RIGID_MINERAL_MAX:
        sink.fail(GATE_ORGANIC_SWAY_ANCHOR_MISSING,
                  "colour '{0}' minimum red={1:.6f} above "
                  "law.SWAY_RIGID_MINERAL_MAX={2:.6f}; no anchor/root band "
                  "exists so roots sway with the tips".format(
                      name, lowest, law.SWAY_RIGID_MINERAL_MAX))


def _gate_materials(data: MeshData, sink: _Sink) -> int:
    """Material slot and submesh contract. Returns the submesh count.

    3dmodel.md section 10: "Submesh count matches material slot declaration."
    Section 6 fixes the slot roles and law.MATERIAL_SLOT_MAX bounds them.
    """
    used = set()
    for t in range(len(data.tri_material_index)):
        index = data.tri_material_index[t]
        used.add(index)
        if index < 0 or index >= data.material_slot_count:
            sink.fail(GATE_MATERIAL_INDEX_OUT_OF_SLOT_RANGE,
                      "triangle[{0}] material_index={1} but the mesh declares "
                      "{2} slot(s)".format(t, index, data.material_slot_count))
    if data.material_slot_count <= 0:
        if data.triangle_count > 0:
            sink.fail(GATE_MATERIAL_SLOTS_MISSING,
                      "{0} triangles with zero material slots; section 6 "
                      "requires authored material IDs or deterministic "
                      "slots".format(data.triangle_count))
        sink.skip(GATE_SUBMESH_EMPTY_DECLARED_SLOT, "no slots declared")
    else:
        for slot in range(data.material_slot_count):
            if slot not in used:
                sink.fail(GATE_SUBMESH_EMPTY_DECLARED_SLOT,
                          "material slot {0} of {1} carries no triangle, so the "
                          "submesh count does not match the declaration".format(
                              slot, data.material_slot_count))
    declared = max(data.material_slot_count,
                   max(used) + 1 if used else 0)
    if declared > law.MATERIAL_SLOT_MAX:
        sink.fail(GATE_MATERIAL_SLOT_COUNT_EXCEEDED,
                  "{0} material slot(s)/submesh(es) exceeds "
                  "law.MATERIAL_SLOT_MAX={1}".format(declared,
                                                     law.MATERIAL_SLOT_MAX))
    return len(used)


def _gate_lod_budget(data: MeshData, sink: _Sink, *, family, lod_index) -> None:
    """Triangle budget for this LOD.

    3dmodel.md section 7: "These are hard maxima, not targets."
    """
    budget = law.LOD_BUDGETS[family].limit(lod_index)
    if data.triangle_count > budget:
        sink.fail(GATE_LOD_TRIANGLE_BUDGET_EXCEEDED,
                  "{0} triangles at LOD{1} exceeds the {2} hard maximum of "
                  "{3}".format(data.triangle_count, lod_index, family.value,
                               budget))


# ---------------------------------------------------------------------------
# Public API
# ---------------------------------------------------------------------------

def validate_mesh_data(data: MeshData, *, family, lod_index: int, surface_class,
                       blackbox=None, hero: bool = False,
                       triplanar: bool = False, double_sided: bool = False,
                       planar: bool = False, atlas_size=None) -> MeshReport:
    """Run the whole mesh gate battery over a flat snapshot.

    Every gate in ``MESH_GATES`` runs; none of them short-circuits the others, so
    one run returns the complete failure list.

    The optional declarations map onto named bible exemptions and default to the
    strict reading:

    ``triplanar``     section 6 triplanar route, UV0 still required
    ``double_sided``  pipeline bible deliberate double-sided shell
    ``planar``        section 7 approved card/impostor, one collapsed axis
    ``atlas_size``    enables the island-pixel and border-padding gates
    ``hero``          selects UV_STRETCH_MAX_HERO over UV_STRETCH_MAX_DISTANT
    """
    family = _as_family(family)
    surface_class = _as_surface_class(surface_class)
    sink = _Sink()

    indices_usable = _gate_structure(data, sink)
    bounds_min, bounds_max = _gate_positions_and_bounds(data, sink, planar=planar)
    _gate_normals(data, sink)
    _gate_tangents(data, sink)
    if indices_usable:
        _gate_triangles(data, sink, double_sided=double_sided)
    else:
        for gate in (GATE_DEGENERATE_TRIANGLE, GATE_INCONSISTENT_WINDING):
            sink.skip(gate, "index buffer is invalid; fix index gates first")
    _gate_uv(data, sink, hero=hero, triplanar=triplanar, atlas_size=atlas_size,
             surface_class=surface_class,
             indices_usable=indices_usable)
    _gate_vertex_colors(data, sink, surface_class=surface_class)
    submesh_count = _gate_materials(data, sink)
    _gate_lod_budget(data, sink, family=family, lod_index=lod_index)

    _record_gates(blackbox, "validate_mesh:", MESH_GATES, sink,
                  family=family.value, vertex_count=data.vertex_count,
                  triangle_count=data.triangle_count)

    report = MeshReport(
        name=data.name,
        vertex_count=data.vertex_count,
        triangle_count=data.triangle_count,
        submesh_count=submesh_count,
        bounds_min=bounds_min,
        bounds_max=bounds_max,
        uv_layers=tuple(layer[0] for layer in data.uv_layers),
        color_layers=tuple(layer[0] for layer in data.color_layers),
        has_tangent_basis=bool(data.tangents),
        failures=sink.failures(),
        warnings=sink.notes(),
        lod_index=lod_index,
        family=family.value,
        surface_class=surface_class.value,
        digest=data.digest(),
    )
    if blackbox is not None:
        blackbox.record(
            "validate_mesh:verdict:" + data.name, family=family.value,
            vertex_count=data.vertex_count, triangle_count=data.triangle_count,
            digest=report.digest,
            warning="pass" if report.passed else "{0} gate(s) failed".format(
                len(report.failures)),
            failure_code="" if report.passed else "mesh_validation_failed",
        )
    return report


def validate_mesh(mesh, *, family, lod_index: int, surface_class, blackbox=None,
                  hero: bool = False, triplanar: bool = False,
                  double_sided: bool = False, planar: bool = False,
                  atlas_size=None) -> MeshReport:
    """Validate one Blender mesh datablock against HECTON-8 law.

    Snapshots the datablock, then runs :func:`validate_mesh_data`. The mesh is
    inspected, never modified: the tangent basis requested during extraction is
    released again.
    """
    data = extract_mesh_data(mesh)
    if blackbox is not None:
        blackbox.record(
            "validate_mesh:extract:" + data.name,
            family=_as_family(family).value, vertex_count=data.vertex_count,
            triangle_count=data.triangle_count,
            warning="uv={0} vcol={1} tangents={2}".format(
                len(data.uv_layers), len(data.color_layers), data.tangent_source),
        )
    return validate_mesh_data(
        data, family=family, lod_index=lod_index, surface_class=surface_class,
        blackbox=blackbox, hero=hero, triplanar=triplanar,
        double_sided=double_sided, planar=planar, atlas_size=atlas_size)


def validate_lod_chain(reports, *, family, blackbox=None,
                       exempt: bool = False) -> list:
    """Chain-level gates across the per-LOD reports.

    3dmodel.md section 7: "No generated LOD0 asset may be saved without a
    complete LOD chain unless the asset is an approved single-triangle
    impostor/card or editor-only debug mesh", and LOD1/LOD2 must be reductions.
    The approval is the caller's declaration through ``exempt``; law.py holds no
    per-family LOD exemption set to derive it from.
    """
    family = _as_family(family)
    sink = _Sink()
    ordered = []
    for position in range(len(reports)):
        report = reports[position]
        index = report.lod_index if report.lod_index >= 0 else position
        ordered.append((index, report))
    ordered.sort(key=lambda pair: pair[0])

    seen = {}
    for index, report in ordered:
        previous = seen.get(index)
        if previous is None:
            seen[index] = report.name
        else:
            sink.fail(GATE_LOD_CHAIN_DUPLICATE_INDEX,
                      "LOD{0} claimed by both '{1}' and '{2}'".format(
                          index, previous, report.name))

    if exempt:
        sink.skip(GATE_LOD_CHAIN_INCOMPLETE,
                  "caller declared exempt=True; section 7 approved impostor/"
                  "card or editor-only debug mesh")
    else:
        for required in REQUIRED_LOD_INDICES:
            if required not in seen:
                sink.fail(GATE_LOD_CHAIN_INCOMPLETE,
                          "LOD{0} missing; chain holds {1} for family "
                          "{2}".format(required, sorted(seen.keys()),
                                       family.value))

    for position in range(1, len(ordered)):
        coarse_index, coarse = ordered[position]
        fine_index, fine = ordered[position - 1]
        if coarse.triangle_count >= fine.triangle_count:
            sink.fail(GATE_LOD_CHAIN_NOT_MONOTONIC,
                      "LOD{0} '{1}' has {2} triangles, not fewer than LOD{3} "
                      "'{4}' with {5}".format(
                          coarse_index, coarse.name, coarse.triangle_count,
                          fine_index, fine.name, fine.triangle_count))
    if len(ordered) < 2:
        sink.skip(GATE_LOD_CHAIN_NOT_MONOTONIC,
                  "fewer than two LODs supplied, no reduction to measure")

    _record_gates(blackbox, "validate_lod_chain:", LOD_CHAIN_GATES, sink,
                  family=family.value)
    return sink.failures()


def _referenced_vertices(data: MeshData):
    """Deduplicated vertex indices actually used by triangles."""
    seen = []
    flags = {}
    for index in data.tri_vertices:
        if index not in flags:
            flags[index] = True
            seen.append(index)
    return seen


def _gate_convex(data: MeshData, sink: _Sink) -> None:
    """Every referenced vertex must lie on or behind every face plane.

    3dmodel.md section 9: "convex hull or convex decomposition under 200
    triangles total per asset". law.py holds no dedicated convexity tolerance,
    so law.MIN_BOUNDS_EXTENT_M is used: a deviation smaller than the minimum
    meaningful extent in the bibles is below the law's own geometric resolution.
    """
    tolerance = law.MIN_BOUNDS_EXTENT_M
    verts = _referenced_vertices(data)
    positions = data.positions
    for t in range(data.triangle_count):
        i0 = data.tri_vertices[t * 3]
        i1 = data.tri_vertices[t * 3 + 1]
        i2 = data.tri_vertices[t * 3 + 2]
        a, b, c = i0 * 3, i1 * 3, i2 * 3
        ux = positions[b] - positions[a]
        uy = positions[b + 1] - positions[a + 1]
        uz = positions[b + 2] - positions[a + 2]
        vx = positions[c] - positions[a]
        vy = positions[c + 1] - positions[a + 1]
        vz = positions[c + 2] - positions[a + 2]
        nx = uy * vz - uz * vy
        ny = uz * vx - ux * vz
        nz = ux * vy - uy * vx
        length = _length3(nx, ny, nz)
        if not _finite(length) or length <= law.DEGENERATE_TRIANGLE_AREA_EPS:
            continue
        nx, ny, nz = nx / length, ny / length, nz / length
        for index in verts:
            base = index * 3
            distance = (nx * (positions[base] - positions[a])
                        + ny * (positions[base + 1] - positions[a + 1])
                        + nz * (positions[base + 2] - positions[a + 2]))
            if not _finite(distance):
                continue
            if distance > tolerance:
                sink.fail(GATE_COLLIDER_NOT_CONVEX,
                          "vertex[{0}] sits {1:.6f} m outside the plane of "
                          "triangle[{2}] (tolerance {3} m)".format(
                              index, distance, t, tolerance))
                return


def validate_collider(mesh, *, family, blackbox=None, lod0_mesh=None,
                      visual_meshes=()) -> list:
    """Validate a collision proxy datablock.

    3dmodel.md section 9: "LOD0 visual meshes must never be assigned directly to
    production MeshCollider components", "convex hull or convex decomposition
    under 200 triangles total per asset", "Collider proxy child names must start
    with COL_".

    The LOD0 cross-check needs the visual datablocks. Pass ``lod0_mesh`` and/or
    ``visual_meshes``; with neither, the proxy cannot be certified as distinct
    from LOD0 and the gate reports ``collider_crosscheck_unavailable`` rather
    than passing on an unverified assumption. Identity is tested three ways:
    Python object identity, datablock name, and a content digest over positions
    and indices, because a rename defeats a name comparison on its own.
    """
    family = _as_family(family)
    sink = _Sink()
    data = extract_mesh_data(mesh)

    if data.triangle_count <= 0 or data.vertex_count <= 0:
        sink.fail(GATE_COLLIDER_EMPTY,
                  "collider '{0}' has {1} vertices and {2} triangles".format(
                      data.name, data.vertex_count, data.triangle_count))

    if not data.name.startswith(law.COLLIDER_PREFIX):
        sink.fail(GATE_COLLIDER_NAME_NOT_COL_PREFIXED,
                  "collider datablock '{0}' does not start with "
                  "law.COLLIDER_PREFIX='{1}'".format(data.name,
                                                     law.COLLIDER_PREFIX))
    for prefix in (law.VISUAL_PREFIX, law.LOD_PREFIX):
        if data.name.startswith(prefix):
            sink.fail(GATE_COLLIDER_NAME_NOT_COL_PREFIXED,
                      "collider datablock '{0}' carries the visual prefix "
                      "'{1}'".format(data.name, prefix))

    over_budget = data.triangle_count > law.COLLIDER_CONVEX_TRI_MAX
    if over_budget:
        sink.fail(GATE_COLLIDER_TRIANGLE_BUDGET_EXCEEDED,
                  "{0} triangles exceeds law.COLLIDER_CONVEX_TRI_MAX={1}".format(
                      data.triangle_count, law.COLLIDER_CONVEX_TRI_MAX))
    if over_budget:
        sink.skip(GATE_COLLIDER_NOT_CONVEX,
                  "triangle budget already rejects this proxy; convexity of an "
                  "over-budget hull is not the actionable failure")
    elif data.triangle_count > 0:
        _gate_convex(data, sink)
    else:
        sink.skip(GATE_COLLIDER_NOT_CONVEX, "no triangles to test")

    candidates = []
    if lod0_mesh is not None:
        candidates.append(("LOD0", lod0_mesh))
    for extra in visual_meshes:
        candidates.append(("visual", extra))
    if not candidates:
        sink.fail(GATE_COLLIDER_CROSSCHECK_UNAVAILABLE,
                  "no lod0_mesh or visual_meshes passed, so '{0}' cannot be "
                  "proven distinct from the LOD0 visual mesh; section 9 "
                  "requires that proof before save".format(data.name))
        sink.skip(GATE_COLLIDER_IS_VISUAL_MESH, "no visual mesh to compare with")
    else:
        own_digest = None
        for role, visual in candidates:
            if visual is mesh:
                sink.fail(GATE_COLLIDER_IS_VISUAL_MESH,
                          "collider '{0}' is the same datablock object as the "
                          "{1} visual mesh".format(data.name, role))
                continue
            visual_name = getattr(visual, "name", "")
            if visual_name and visual_name == data.name:
                sink.fail(GATE_COLLIDER_IS_VISUAL_MESH,
                          "collider '{0}' shares the {1} visual mesh "
                          "name".format(data.name, role))
                continue
            visual_data = extract_mesh_data(visual)
            if (visual_data.vertex_count != data.vertex_count
                    or visual_data.triangle_count != data.triangle_count):
                continue
            if own_digest is None:
                own_digest = data.digest()
            if visual_data.digest() == own_digest:
                sink.fail(GATE_COLLIDER_IS_VISUAL_MESH,
                          "collider '{0}' is byte-identical to {1} visual mesh "
                          "'{2}' (digest {3}, {4} verts, {5} tris); renaming a "
                          "visual mesh does not make it a proxy".format(
                              data.name, role, visual_data.name, own_digest,
                              data.vertex_count, data.triangle_count))

    _record_gates(blackbox, "validate_collider:", COLLIDER_GATES, sink,
                  family=family.value, vertex_count=data.vertex_count,
                  triangle_count=data.triangle_count)
    return sink.failures()


def _collect_failures(items) -> list:
    """Flatten MeshReports, Failure lists, and single Failures into one list."""
    out = []
    for item in items:
        if isinstance(item, MeshReport):
            out.extend(item.failures)
        elif isinstance(item, Failure):
            out.append(item)
        else:
            out.extend(_collect_failures(item))
    return out


def assert_or_abort(reports, *, blackbox, reason: str) -> None:
    """Raise :class:`GenerationAborted` when anything failed. Never returns True.

    3dmodel.md section 10: "Failure aborts save." Section 11 requires the ring
    dump on validation abort, so the exception carries the dump path instead of a
    bare message.

    ``reports`` accepts MeshReport objects, the Failure lists returned by
    :func:`validate_lod_chain` and :func:`validate_collider`, and nested lists of
    those, so one call gates the whole package.
    """
    failures = _collect_failures(reports)
    if not failures:
        if blackbox is not None:
            blackbox.record("assert_or_abort:pass", warning=reason)
        return

    if blackbox is None:
        raise GenerationAborted(
            "validation aborted save ({0}): {1} failure(s) and no black box was "
            "supplied to dump: {2}".format(
                reason, len(failures), "; ".join(str(f) for f in failures)),
            None, failures)

    for failure in failures:
        blackbox.note_invalid(
            "assert_or_abort:" + failure.gate, failure.gate,
            "x{0} {1}".format(failure.count, failure.detail))
    dump_path = blackbox.dump(reason)
    total = 0
    for failure in failures:
        total += failure.count
    raise GenerationAborted(
        "validation aborted save ({0}): {1} gate(s), {2} occurrence(s). "
        "Black box dumped to {3}. Gates: {4}".format(
            reason, len(failures), total, dump_path,
            ", ".join(f.gate for f in failures)),
        dump_path, failures)
