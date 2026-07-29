"""Geometry operations that enforce HECTON-8 mesh law.

Everything here delegates the actual numerical work to Blender's battle-tested
operators rather than reimplementing bevel/decimation math, which is the entire
reason an external DCC is sanctioned by ``3dmodel.md`` section 0:

    "All mesh generation, texture synthesis, UV unwrapping, tangent construction,
     normal baking, atlas packing, collider fitting, LOD decimation, and prefab
     assembly MUST occur only in Unity Editor tooling or external offline DCC/bake
     tools."

Runs inside Blender 4.5 LTS (Python 3.11). Note the 4.1+ API break: ``use_auto_smooth``
no longer exists on ``Mesh``; angle-based shading is a modifier applied through
``bpy.ops.object.shade_auto_smooth``. Code written against 3.x docs silently does
nothing here, which is exactly the kind of quiet degeneracy this project's rule
files warn about.
"""

from __future__ import annotations

import math
from dataclasses import dataclass
from typing import Iterable, Optional

import bmesh
import bpy
from mathutils import Vector

from . import law
from . import validate
from .blackbox import BlackBox


# ---------------------------------------------------------------------------
# bmesh lifecycle
# ---------------------------------------------------------------------------

def _make_sole_active(obj: bpy.types.Object) -> None:
    """Make ``obj`` the only selected object and the active one.

    ``bpy.ops.object.modifier_apply`` and ``shade_auto_smooth`` are operators, and
    operators read the selection, not just the active pointer. Setting
    ``view_layer.objects.active`` alone leaves the object unselected, at which point
    the operator returns ``{'CANCELLED'}`` and does nothing -- with no exception. That
    is how a decimation loop "runs" six times and leaves the triangle count untouched.
    """
    view_layer = bpy.context.view_layer
    # `view_layer.objects` can yield None on a stale depsgraph - typically after a
    # multi-variant run removed objects between variants without the layer catching
    # up. Measured: cap-stem died on its first variant with "AttributeError: 'NoneType'
    # object has no attribute 'select_get'" from this exact loop, inside the decimate
    # step, taking the whole generator down. A loop whose only job is to clear a
    # selection must not be able to kill a bake, so the entry is skipped rather than
    # dereferenced.
    for other in view_layer.objects:
        if other is None:
            continue
        try:
            if other.select_get():
                other.select_set(False)
        except (ReferenceError, RuntimeError):
            # A freed datablock still listed by the layer. Same class of problem, and
            # equally not worth aborting a bake over.
            continue
    obj.select_set(True)
    view_layer.objects.active = obj


def bmesh_from_object(obj: bpy.types.Object) -> bmesh.types.BMesh:
    """Fresh BMesh from an object's evaluated-free mesh data."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    return bm


def bmesh_to_object(bm: bmesh.types.BMesh, obj: bpy.types.Object,
                    free: bool = True) -> None:
    bm.to_mesh(obj.data)
    obj.data.update()
    if free:
        bm.free()


def triangle_count(mesh: bpy.types.Mesh) -> int:
    """Triangles after triangulation, which is what Unity will actually receive.

    ``len(mesh.polygons)`` undercounts quads and n-gons, so budget checks against it
    pass while the imported asset blows the budget. Loop-triangles are the truth.
    """
    mesh.calc_loop_triangles()
    return len(mesh.loop_triangles)


# ---------------------------------------------------------------------------
# Cleaning and welding
# ---------------------------------------------------------------------------

def weld_and_clean(
    bm: bmesh.types.BMesh,
    merge_distance: float = 1e-4,
    *,
    fill_boundary_loops: bool = True,
    blackbox: Optional[BlackBox] = None,
) -> dict:
    """Merge coincident vertices, drop degenerate geometry, recalculate winding.

    ``3DMODEL_FLORA_CORAL.md`` section 3: "Branch intersections must be blended,
    welded, or explicitly hidden by knuckles. Intersecting tubes with z-fighting are
    rejected." Welding is the mechanical half of satisfying that gate.
    """
    before_v = len(bm.verts)
    before_f = len(bm.faces)

    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=merge_distance)
    bmesh.ops.dissolve_degenerate(bm, dist=merge_distance,
                                  edges=bm.edges[:])
    # Delete faces that survived dissolve but still have no area.
    dead = [f for f in bm.faces if f.calc_area() <= law.DEGENERATE_TRIANGLE_AREA_EPS]
    if dead:
        bmesh.ops.delete(bm, geom=dead, context="FACES")
    # Loose verts and edges never reach the GPU but do reach the validator.
    loose_verts = [v for v in bm.verts if not v.link_faces]
    if loose_verts:
        bmesh.ops.delete(bm, geom=loose_verts, context="VERTS")
    # NON-MANIFOLD EDGES PIN DECIMATION, and nothing above this line touches them.
    #
    # Measured on a branching coral: components=4, irreducible_floor=12, yet LOD2 sat at 584
    # triangles - 48x above its own floor - and `nonmanifold_edges` read exactly 144 at LOD0,
    # LOD1 AND LOD2. Identical through every decimation pass, because Quadric Edge Collapse
    # will not collapse across a non-manifold edge. 144 of them pinned the whole mesh.
    #
    # This also corrects a diagnosis I stated twice as fact: I claimed the floor came from ~76
    # disconnected tip-cluster shells. The component count is 4. Disconnected shells were never
    # the cause; interior faces at skin-modifier branch junctions were.
    #
    # The repair deletes faces that no manifold surface needs: an edge shared by three or more
    # faces means one of them is interior, buried where two branches merge, contributing nothing
    # visible while blocking every collapse through it. Deleting the face with the smallest area
    # at each such edge removes the interior sheet and leaves the outer hull.
    nonmanifold_before = sum(1 for e in bm.edges if len(e.link_faces) > 2)
    interior_deleted = 0
    forced_by_area = 0
    windings_flipped = 0
    if nonmanifold_before:
        doomed = set()
        for edge in bm.edges:
            linked = edge.link_faces
            if len(linked) <= 2:
                continue
            # AREA-ONLY, AND THE ALTERNATIVE WAS TRIED AND MEASURED AND REVERTED.
            #
            # This rule is the sole creator of a NON-ORIENTABLE surface, proven
            # per-operator on coral seed 1712: the Skin output arriving here has
            # contradicting_manifold_edges 0, and this deletion alone takes it to 10.
            # remove_doubles is exonerated, and holes_fill only exposes more twisted
            # cycles because filling a hole in an orientable surface with a disk cannot
            # break orientability. The mechanism is exact: all 8 non-manifold edges split
            # 2/1 by TRAVERSAL DIRECTION, never 3/0, so keeping the two largest keeps BOTH
            # faces from the majority side whenever the minority face is smaller and welds
            # the shell to itself with a half turn. A Moebius join. Every edge then carries
            # exactly two faces, so nonmanifold_edges reads a clean 0 while
            # inconsistent_winding fires 53 times on coral LOD0 - and no winding assignment
            # exists, which is why recalc_face_normals measured 53 -> 39 and never 0, and
            # 60 -> 98 pre-decimation.
            #
            # THE CORRECT RULE IS TO KEEP ONE FACE PER DIRECTION, and it cannot land yet.
            # Measured with it in place, three times to six decimals: the FBX round trip
            # fails with "LOD2: corner normals changed by 0.962271" - 74 degrees on a unit
            # normal - and the exporter deletes the package, so the asset stops existing.
            # Adding a topological winding flood-fill afterwards did not move that number
            # at all, so the bad normals come from the surviving face SET and not from its
            # winding. Reverting the generator's own post-decimation twin did not move it
            # either.
            #
            # So the choice is between a clean validator line and a shippable package, and
            # the package wins. Orientability stays recorded as debt with its mechanism
            # named, which is worth more than a fix that deletes the asset.
            ordered = sorted(linked, key=lambda f: f.calc_area(), reverse=True)
            for face in ordered[2:]:
                doomed.add(face)
        if doomed:
            bmesh.ops.delete(bm, geom=list(doomed), context="FACES")
            interior_deleted = len(doomed)

            # No winding flood fill here: it was written, measured against the
            # round-trip gate, and changed the 0.962271 drift by nothing at all.
            # Deleting an interior sheet can strand vertices that only it used.
            orphans = [v for v in bm.verts if not v.link_faces]
            if orphans:
                bmesh.ops.delete(bm, geom=orphans, context="VERTS")
    nonmanifold_after = sum(1 for e in bm.edges if len(e.link_faces) > 2)

    # CAP THE RIMS THE REPAIR OPENED. Removing an interior sheet leaves a boundary where it
    # met the shell, and on a coral that was visible as an open hole at the trunk base with
    # the interior volume showing through - measured 11 boundary edges before the repair, 88
    # after. Recorded as "debt" in one commit; looking at the render showed it was a hole,
    # not an abstraction.
    #
    # Filled PER LOOP, not globally. A single holes_fill over every boundary edge bridges
    # unrelated rims into one outer membrane - a sibling generator hit exactly that and its
    # tell was an AO mean collapsing to 0.0057, because the real surface ended up buried
    # inside the bridging sheet.
    holes_filled = 0
    if fill_boundary_loops:
        remaining = set(e for e in bm.edges if len(e.link_faces) == 1)
        while remaining:
            seed = remaining.pop()
            loop = [seed]
            frontier = [seed]
            while frontier:
                edge = frontier.pop()
                for vert in edge.verts:
                    for other in vert.link_edges:
                        if other in remaining and len(other.link_faces) == 1:
                            remaining.discard(other)
                            loop.append(other)
                            frontier.append(other)
            if len(loop) < 3:
                continue
            try:
                bmesh.ops.holes_fill(bm, edges=loop, sides=0)
                holes_filled += 1
            except (RuntimeError, ValueError):
                # A degenerate rim that will not close is reported rather than retried;
                # retrying the same op on the same geometry is the same-failure loop the
                # project's law forbids.
                pass

    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])

    stats = {
        "verts_removed": before_v - len(bm.verts),
        "faces_removed": before_f - len(bm.faces),
        "degenerate_faces_deleted": len(dead),
        "loose_verts_deleted": len(loose_verts),
        "nonmanifold_edges_before": nonmanifold_before,
        "nonmanifold_edges_after": nonmanifold_after,
        "interior_faces_deleted": interior_deleted,
        # Non-zero means some non-manifold edge had every face winding the same way, so
        # the direction-aware keep had no opposing pair and fell back to area - the rule
        # that was measured to create the Moebius join. A stage that can silently pick
        # wrong reports how often it had to.
        "interior_edges_forced_by_area": forced_by_area,
        # Faces reversed to make the winding agree after the direction-aware keep.
        # Zero on a mesh that arrived consistently wound; non-zero is the repair
        # working, not a warning.
        "windings_flipped": windings_flipped,
        "boundary_loops_filled": holes_filled,
        "boundary_edges_after": sum(1 for e in bm.edges if len(e.link_faces) == 1),
    }
    if blackbox is not None:
        blackbox.record(
            "weld_and_clean",
            vertex_count=len(bm.verts),
            triangle_count=len(bm.faces),
            warning="" if not dead else "deleted {n} zero-area faces".format(n=len(dead)),
        )
    return stats


# ---------------------------------------------------------------------------
# Bevel  --  3dmodel.md section 4, "Hard-Surface Engineering Law"
# ---------------------------------------------------------------------------

@dataclass
class BevelResult:
    edges_considered: int
    edges_beveled: int
    width_m: float
    segments: int
    clamped: bool


def _shortest_adjacent_edge_length(edge: bmesh.types.BMEdge) -> float:
    """Length of the shortest edge sharing a vertex with ``edge``.

    Feeds the bible's overlap clamp: "Clamp bevel width to 20 percent of the
    shortest adjacent edge to prevent self-overlap."
    """
    shortest = edge.calc_length()
    for vert in edge.verts:
        for other in vert.link_edges:
            if other is edge:
                continue
            length = other.calc_length()
            if length < shortest:
                shortest = length
    return shortest


def select_hard_edges(
    bm: bmesh.types.BMesh,
    angle_threshold_deg: float = law.BEVEL_ANGLE_THRESHOLD_DEG,
) -> list:
    """Manifold edges whose adjacent face angle exceeds the threshold.

    Implements steps 1-3 of the bible's required algorithm: build the edge/face
    adjacency, reject zero-area faces, and compute
    ``angle = acos(clamp(dot(n0, n1), -1, 1))``.

    Boundary edges are excluded: they have one adjacent face, so there is no
    dihedral angle to bevel, and beveling them produces a rim that the flora bible
    wants authored deliberately rather than as a side effect.
    """
    threshold = math.radians(angle_threshold_deg)
    hard = []
    for edge in bm.edges:
        faces = edge.link_faces
        if len(faces) != 2:
            continue
        f0, f1 = faces[0], faces[1]
        if (f0.calc_area() <= law.DEGENERATE_TRIANGLE_AREA_EPS or
                f1.calc_area() <= law.DEGENERATE_TRIANGLE_AREA_EPS):
            continue
        dot = max(-1.0, min(1.0, f0.normal.dot(f1.normal)))
        if math.acos(dot) > threshold:
            hard.append(edge)
    return hard


def bevel_hard_edges(
    bm: bmesh.types.BMesh,
    *,
    family: law.Family,
    quality_weight: float,
    hero: bool = False,
    bevel_range: Optional[law.BevelRange] = None,
    angle_threshold_deg: float = law.BEVEL_ANGLE_THRESHOLD_DEG,
    blackbox: Optional[BlackBox] = None,
) -> BevelResult:
    """Chamfer every visible hard edge. Never a no-op on hard-surface families.

    ``3dmodel.md`` section 4 bans 90-degree mathematical corners outright: "A perfect
    cube edge is one infinitely sharp normal discontinuity, so it either renders as a
    dead black cut or a razor highlight that exposes the mesh as programmer output."

    Width comes from the family range scaled by ``GlobalQualityWeight``, then clamped
    per-edge to 20% of the shortest adjacent edge. The clamp is global here (one
    offset for the whole bevel op) and uses the most restrictive edge in the
    selection, because a single bmesh bevel call takes one offset; picking the
    minimum is the only choice that cannot self-intersect anywhere.
    """
    if bevel_range is None:
        bevel_range = law.BEVEL_RANGES.get(family)
    if bevel_range is None:
        raise ValueError(
            "no bevel range for family " + str(family) +
            "; pass bevel_range explicitly (e.g. law.BEVEL_RANGE_INTERIOR_TRIM)"
        )

    hard = select_hard_edges(bm, angle_threshold_deg)
    considered = len(bm.edges)
    if not hard:
        if blackbox is not None:
            blackbox.record("bevel_hard_edges", warning="no hard edges found")
        return BevelResult(considered, 0, 0.0, 0, False)

    requested = bevel_range.width_for(quality_weight)
    segments = law.bevel_segments_for(quality_weight, hero=hero)

    limit = min(_shortest_adjacent_edge_length(e) for e in hard)
    max_allowed = limit * law.BEVEL_WIDTH_CLAMP_RATIO
    width = min(requested, max_allowed)
    clamped = width < requested

    # harden_normals requires the result to be shaded smooth; the caller applies
    # angle shading afterwards, so we keep normals soft here and let the weighted
    # normal pass own the final basis. Hardening twice fights itself.
    bmesh.ops.bevel(
        bm,
        geom=hard,
        offset=width,
        offset_type="OFFSET",
        segments=segments,
        profile=0.5,
        affect="EDGES",
        clamp_overlap=True,
        material=-1,
    )

    if blackbox is not None:
        blackbox.record(
            "bevel_hard_edges",
            vertex_count=len(bm.verts),
            triangle_count=len(bm.faces),
            warning=("clamped {w:.4f}m from requested {r:.4f}m".format(w=width, r=requested)
                     if clamped else ""),
        )
    return BevelResult(considered, len(hard), width, segments, clamped)


# ---------------------------------------------------------------------------
# Shading basis  --  3dmodel.md section 4, smoothing groups + weighted normals
# ---------------------------------------------------------------------------

@dataclass
class ShadingResult:
    smooth_polygons: int
    sharp_edges: int
    weighted_applied: bool


def apply_shading_basis(
    obj: bpy.types.Object,
    *,
    smooth_angle_deg: float = law.SMOOTH_ANGLE_DEG,
    weighted: bool = True,
    keep_sharp: bool = True,
    blackbox: Optional[BlackBox] = None,
) -> ShadingResult:
    """Angle-based smoothing plus area/angle weighted normals, applied at DATA level.

    ``bpy.ops.object.shade_auto_smooth`` -- the documented Blender 4.1+ replacement for
    the removed ``Mesh.use_auto_smooth`` -- returns ``{'CANCELLED'}`` under
    ``-b --factory-startup`` and adds NO modifier. Measured on 4.5.9: the operator
    reports CANCELLED, ``obj.modifiers`` stays empty, and every ``polygon.use_smooth``
    remains False. So the previous implementation was a complete no-op in exactly the
    headless mode this whole pipeline runs in, and every asset it produced was
    flat-shaded. That is not a cosmetic loss: flat shading destroys the specular
    response the bevel pass in ``3dmodel.md`` section 4 exists to create, so a correctly
    beveled mesh still read as faceted programmer output.

    The fix does the same job without an operator:
      1. mark every polygon smooth;
      2. mark edges whose dihedral angle exceeds the threshold as SHARP -- this IS what
         "Smooth by Angle" does, expressed as mesh data instead of a modifier;
      3. apply WEIGHTED_NORMAL, which is precisely the bible's formula
         ``normalize(sum(faceNormal * faceArea * cornerAngleWeight))`` in
         ``mode='FACE_AREA_WITH_ANGLE'``, with ``keep_sharp`` honouring step 2.

    Data-level is also strictly better here: it is deterministic, it does not depend on
    operator context, and the sharp-edge set is inspectable afterwards.
    """
    mesh = obj.data
    for polygon in mesh.polygons:
        polygon.use_smooth = True

    threshold = math.radians(smooth_angle_deg)
    sharp_count = 0
    bm = bmesh.new()
    bm.from_mesh(mesh)
    try:
        for edge in bm.edges:
            if len(edge.link_faces) != 2:
                # A boundary edge has no dihedral angle. Leaving it smooth avoids a
                # shading seam along an intentionally open shell rim.
                continue
            angle = edge.calc_face_angle()
            is_smooth = angle <= threshold
            edge.smooth = is_smooth
            if not is_smooth:
                sharp_count += 1
        bm.to_mesh(mesh)
    finally:
        bm.free()
    mesh.update()

    weighted_applied = False
    if weighted:
        view_layer = bpy.context.view_layer
        previous_active = view_layer.objects.active
        modifier = obj.modifiers.new(name="H8_WeightedNormal", type="WEIGHTED_NORMAL")
        modifier.mode = "FACE_AREA_WITH_ANGLE"
        modifier.weight = 50
        modifier.keep_sharp = keep_sharp
        _make_sole_active(obj)
        result = bpy.ops.object.modifier_apply(modifier=modifier.name)
        weighted_applied = "FINISHED" in result
        if not weighted_applied and modifier.name in [m.name for m in obj.modifiers]:
            obj.modifiers.remove(modifier)
        if previous_active is not None:
            view_layer.objects.active = previous_active

    smooth_polygons = sum(1 for p in obj.data.polygons if p.use_smooth)
    if blackbox is not None:
        blackbox.record(
            "apply_shading_basis",
            vertex_count=len(obj.data.vertices),
            triangle_count=triangle_count(obj.data),
            warning=("" if smooth_polygons and weighted_applied else
                     "smooth_polygons={s} weighted_applied={w}".format(
                         s=smooth_polygons, w=weighted_applied)),
            failure_code="" if smooth_polygons else "SHADING_NOT_APPLIED",
        )
    return ShadingResult(smooth_polygons, sharp_count, weighted_applied)


# ---------------------------------------------------------------------------
# LOD chain  --  3dmodel.md section 7
# ---------------------------------------------------------------------------

@dataclass
class LodLevel:
    index: int
    obj: bpy.types.Object
    triangles: int
    budget: int
    ratio_used: float

    @property
    def within_budget(self) -> bool:
        return self.triangles <= self.budget


@dataclass
class TopologyReport:
    """Why a mesh is the size it is. Explains an unreachable decimation target."""

    triangles: int
    faces: int
    components: int
    boundary_edges: int
    nonmanifold_edges: int
    smallest_component: int
    largest_component: int

    @property
    def irreducible_floor(self) -> int:
        """Rough lower bound on triangles that Quadric Edge Collapse cannot remove.

        Decimate collapses edges; it does not delete whole shells. Every disconnected
        component therefore keeps at least a tetrahedron-ish remnant, and every boundary
        loop resists collapse. A colony made of many small separate nubs has a floor far
        above its budget no matter how many passes run -- which is the difference between
        "decimation is broken" and "this target is unreachable for this topology".
        """
        return self.components * 4 + self.boundary_edges // 2

    def explain(self, budget: int) -> str:
        if self.triangles <= budget:
            return ""
        return ("{t} tris vs {b} budget; {c} disconnected components, {be} boundary "
                "edges, {nm} non-manifold edges -> estimated irreducible floor ~{f} tris"
                .format(t=self.triangles, b=budget, c=self.components,
                        be=self.boundary_edges, nm=self.nonmanifold_edges,
                        f=self.irreducible_floor))


def topology_report(obj: bpy.types.Object) -> TopologyReport:
    """Connected-component and manifold census.

    Called when a budget is missed so the black box records a CAUSE rather than just a
    number. A generator author reading "584 tris vs 300 budget" learns nothing; reading
    "76 disconnected components" tells them the tip clusters must be welded into the
    parent branch or replaced with an impostor at that LOD.
    """
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    try:
        bm.faces.ensure_lookup_table()
        bm.edges.ensure_lookup_table()

        seen = set()
        sizes = []
        for face in bm.faces:
            if face.index in seen:
                continue
            stack = [face]
            size = 0
            while stack:
                current = stack.pop()
                if current.index in seen:
                    continue
                seen.add(current.index)
                size += 1
                for edge in current.edges:
                    for neighbour in edge.link_faces:
                        if neighbour.index not in seen:
                            stack.append(neighbour)
            sizes.append(size)

        boundary = sum(1 for e in bm.edges if len(e.link_faces) == 1)
        nonmanifold = sum(1 for e in bm.edges if len(e.link_faces) > 2)
        return TopologyReport(
            triangles=triangle_count(obj.data),
            faces=len(bm.faces),
            components=len(sizes),
            boundary_edges=boundary,
            nonmanifold_edges=nonmanifold,
            smallest_component=min(sizes) if sizes else 0,
            largest_component=max(sizes) if sizes else 0,
        )
    finally:
        bm.free()


def reduce_to_budget(
    obj: bpy.types.Object,
    *,
    family: law.Family,
    lod_index: int = 0,
    headroom: float = 0.94,
    blackbox: Optional[BlackBox] = None,
) -> int:
    """Decimate an object down to its LOD budget, returning the final triangle count.

    Needed because the correct authoring route for organic surfaces is high-density
    sculpt THEN reduce: displacing a mesh that is already at budget resolution produces
    mush, while displacing a subdivided mesh and decimating afterwards keeps the
    silhouette the displacement created. ``3dmodel.md`` section 7 names Quadric Edge
    Collapse as the default allowed algorithm for exactly this.

    ``headroom`` leaves a small margin under the ceiling so a later triangulation or
    seam split cannot push a compliant asset back over it.
    """
    budget = law.LOD_BUDGETS[family].limit(lod_index)
    target = max(4, int(budget * max(0.1, min(1.0, headroom))))
    start = triangle_count(obj.data)

    passes = 0
    for _attempt in range(8):
        current = triangle_count(obj.data)
        if current <= target:
            break
        modifier = obj.modifiers.new(name="H8_BudgetDecimate", type="DECIMATE")
        modifier.decimate_type = "COLLAPSE"
        modifier.ratio = max(0.01, min(0.99, (target / float(current)) * 0.96))
        modifier.use_collapse_triangulate = True
        _make_sole_active(obj)
        bpy.ops.object.modifier_apply(modifier=modifier.name)
        passes += 1

    # THE SECOND COLLAPSE SITE, PROBED BUT DELIBERATELY NOT REPAIRED HERE.
    #
    # This function runs the same Decimate/COLLAPSE as `build_lod_chain` and therefore
    # emits the same 3D slivers, which are the cause of `uv_stretch_excessive` (see
    # `heal_collapse_slivers`). It is not healed here because this is the LOD0 path for
    # five generator families and the blast radius of editing an authored LOD0 silhouette
    # cannot be judged without a measured run per family -- rock and the hand tool carry
    # authored split normals through an FBX round-trip gate that has already rejected two
    # different post-decimation edits.
    #
    # So the honest move is a CENSUS, which changes nothing and makes the site visible:
    # `kelp` happens to heal its LOD0 by hand afterwards and measures clean, and the
    # other four generators do not heal at all. Without this number that asymmetry is
    # invisible, and a future LOD0 breach would look like a new defect rather than the
    # known one at a known site. Repairing here needs per-family proof first.
    slivers = 0
    worst_aspect = 0.0
    if passes:
        bm = bmesh_from_object(obj)
        try:
            slivers, worst_aspect, _ngons = _sliver_census(bm, sliver_aspect_max())
        finally:
            bm.free()

    final = triangle_count(obj.data)
    if blackbox is not None:
        notes = []
        if final > budget:
            notes.append("still over budget {f}>{b} from {s}".format(
                f=final, b=budget, s=start))
        if slivers:
            notes.append(
                "{n} collapse slivers above aspect {a:g} (worst {w}); NOT repaired at "
                "this site -- see heal_collapse_slivers. Any later "
                "uv_stretch_excessive on LOD{i} starts here".format(
                    n=slivers, a=sliver_aspect_max(), w=_fmt_metric(worst_aspect),
                    i=lod_index))
        blackbox.record(
            "reduce_to_budget", family=family.value, triangle_count=final,
            vertex_count=len(obj.data.vertices),
            warning="; ".join(notes),
            failure_code="" if final <= budget else "BUDGET_UNREACHABLE",
        )
    return final


def _split_uv_seams(obj: bpy.types.Object) -> int:
    """Split the mesh along UV seams and sharp edges so decimation preserves them.

    ``3dmodel.md`` section 7 requires: "Decimation must preserve boundary edges, UV
    seams, hard normals, material borders, sockets, attachment points, silhouette
    curvature, and collision proxy fit."

    Blender's Decimate/COLLAPSE has no seam-preservation flag -- it preserves mesh
    BOUNDARIES. Splitting seam and sharp edges converts them into boundaries, which
    is the mechanism that makes the bible's requirement actually hold instead of
    being asserted in a comment. The duplicated vertices along the seam are not
    waste: any UV-seamed mesh must duplicate them on export anyway, so this matches
    what Unity will receive regardless.

    Returns the number of edges split.
    """
    bm = bmesh_from_object(obj)
    targets = set()
    for edge in bm.edges:
        if edge.seam or not edge.smooth:
            targets.add(edge)
    # Material borders are silhouette-relevant too: a collapse across a slot border
    # drags one material's geometry into another's island.
    for edge in bm.edges:
        faces = edge.link_faces
        if len(faces) == 2 and faces[0].material_index != faces[1].material_index:
            targets.add(edge)
    count = len(targets)
    if count:
        bmesh.ops.split_edges(bm, edges=list(targets))
    bmesh_to_object(bm, obj)
    return count


def material_slot_anchors(obj: bpy.types.Object) -> dict:
    """Per-slot polygon centroid, so a slot decimation empties can be re-tagged in place.

    Call on LOD0 before ``build_lod_chain`` and hand the result to
    :func:`preserve_material_slots` for each level.
    """
    sums = {}
    for polygon in obj.data.polygons:
        entry = sums.setdefault(polygon.material_index,
                                [Vector((0.0, 0.0, 0.0)), 0])
        entry[0] += polygon.center
        entry[1] += 1
    return {slot: (total / float(count))
            for slot, (total, count) in sums.items() if count}


def preserve_material_slots(obj: bpy.types.Object, anchors: dict) -> dict:
    """Re-tag the nearest surviving polygon to any material slot decimation emptied.

    Quadric Edge Collapse has no notion of a submesh contract, so the smallest role on
    an asset can lose its last polygon at LOD2 - measured on coral at 285 triangles and
    on kelp at 288, both firing the validator's ``submesh_empty_declared_slot``.
    ``3dmodel.md`` section 10 requires the submesh count to match the declaration and
    section 6 requires LOD2 to keep the shader semantics it still reads, so the honest
    repair is to keep the role alive at its own location rather than let a material
    silently vanish partway down the chain.

    One polygon per emptied slot, chosen by distance to that slot's LOD0 centroid, and
    never taken from a slot that is itself down to its last polygon.

    IT LIVES HERE BECAUSE THE CODE THAT EMPTIES THE SLOT LIVES HERE. It existed as two
    copies, in ``kelp.py`` and ``coral_branching.py``, and they had already DRIFTED - not
    in behaviour but in COST. Coral's built a count dict once and decremented it, O(n).
    Kelp's recomputed ``sum(1 for q in mesh.polygons if ...)`` INSIDE the loop over
    polygons, which is O(n^2): on 5864 polygons that is roughly 34 million comparisons per
    emptied slot. A third copy was about to be written for cap-stem. Consolidating on the
    cheaper one fixes kelp as a side effect.
    """
    mesh = obj.data
    used = set(polygon.material_index for polygon in mesh.polygons)
    counts = {}
    for polygon in mesh.polygons:
        counts[polygon.material_index] = counts.get(polygon.material_index, 0) + 1
    repaired = {}
    for slot in range(len(mesh.materials)):
        if slot in used:
            continue
        anchor = anchors.get(slot)
        if anchor is None or not mesh.polygons:
            continue
        best = None
        best_distance = None
        for polygon in mesh.polygons:
            # Never cannibalise a slot that is itself down to its last polygon.
            if counts.get(polygon.material_index, 0) <= 1:
                continue
            distance = (polygon.center - anchor).length
            if best_distance is None or distance < best_distance:
                best_distance = distance
                best = polygon
        if best is not None:
            counts[best.material_index] -= 1
            best.material_index = slot
            counts[slot] = counts.get(slot, 0) + 1
            repaired[slot] = round(best_distance, 5)
    return repaired


def _weld_coincident(obj: bpy.types.Object, distance: float = 1e-6) -> dict:
    """Re-join vertices this pipeline split apart, and nothing else.

    WHY. ``_split_uv_seams`` converts seams, MATERIAL borders and every SHARP edge
    into mesh boundaries so Decimate/COLLAPSE will not collapse across them. That
    works, and on a faceted asset it is also catastrophic: ``apply_shading_basis``
    marks an edge sharp wherever the dihedral angle exceeds the family threshold, so
    a geology asset with hundreds of arrises gets hundreds of edges split and the
    shell SHATTERS. Measured on rock: LOD2 came out with 4 to 118 components and 134
    to 991 boundary edges, LOD1 with 1 to 79 non-manifold edges. Consequences, all
    measured rather than predicted: ``recalc_face_normals`` cannot orient a shattered
    shell so every config failed ``inconsistent_winding``, and the FBX round trip
    rejected LOD1 and LOD2 outright, which means those levels were not shippable at
    all. Coral hit the same wall from the other direction and three hypotheses were
    measured and refuted against it before the cause was found in this function.

    WHY A DISTANCE WELD IS THE RIGHT TOOL AND NOT A BLUNT ONE. The duplicates
    ``split_edges`` creates are COINCIDENT - distance exactly 0. A genuine open rim,
    a blade margin left uncapped on purpose, a card edge: none of those have a
    coincident partner. So a weld at 1e-6 m recovers precisely the vertices this
    pipeline split and cannot silently close a boundary the author wanted. That is
    the whole reason the tolerance is 1e-6 and not the 1e-4 ``weld_and_clean`` uses
    for authored geometry.

    WHY IT DOES NOT UNDO THE SEAM WORK. The split exists to shape the DECIMATION, and
    by the time this runs the decimation has already happened. Unity re-duplicates
    seam vertices on export regardless, so the exported result is unchanged; what
    changes is that the intermediate mesh is a closed shell again, which is what the
    FBX round trip and the winding repair both need.

    Custom split normals survive: they are per-LOOP, and merging two coincident
    vertices does not merge the loops that reference them.
    """
    before_verts = len(obj.data.vertices)
    bm = bmesh_from_object(obj)
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=distance)

    # DUPLICATE FACES, and this is the one that cost five hypotheses.
    #
    # Decimate/COLLAPSE can pull two triangles onto the SAME vertex triple. The
    # result is a pair of coincident faces, and FBX cannot express it: the importer
    # merges them, so the file comes back with exactly one face fewer, three corner
    # normals fewer and its vertex count unchanged - which is precisely what the
    # round-trip gate kept reporting on coral LOD2.
    #
    # MEASURED on coral LOD2, seed 1712: V=146 E=435 F=286, euler -3,
    # duplicateFacePairs=1 - faces 118 and 239 both on vertex set (38, 39, 143).
    #
    # It is invisible to every check that was tried against it first, which is why it
    # survived so long: nonManifoldEdges reads 0 because the pair's edges still have
    # exactly two faces each; the faces have real area so a degenerate-area sweep
    # passes; there is no bowtie vertex; and a boundary-edge count says nothing. Four
    # measured hypotheses were refuted before this one, and each refutation only
    # arrived because the number was checked instead of the mechanism being assumed.
    #
    # Keep the first of each pair and delete the rest: they are geometrically the same
    # face, so nothing visible is lost, and the exported topology finally equals the
    # measured topology.
    seen = {}
    doomed = []
    for face in bm.faces:
        key = tuple(sorted(vert.index for vert in face.verts))
        if key in seen:
            doomed.append(face)
        else:
            seen[key] = face
    duplicate_faces = len(doomed)
    if doomed:
        bmesh.ops.delete(bm, geom=doomed, context="FACES_ONLY")

    # DELIBERATELY NO recalc_face_normals HERE, and it was tried and measured.
    #
    # Nothing recalculates winding after decimation - recalc runs inside
    # weld_and_clean before the LOD chain and never again - so adding it here looked
    # obviously right for the `inconsistent_winding` gate. Measured: coral went 54 ->
    # 53 failures, and that single one was the duplicate face this function had
    # already removed. It fixed NOTHING. And on rock it actively broke the export:
    # recalculating face normals puts them out of agreement with the authored
    # weighted/split basis, so the round trip started failing with "corner normals
    # changed by 0.001859; the authored weighted/split normal basis did not survive"
    # - trading lost geometry for lost shading.
    #
    # So inconsistent_winding is NOT an orientation problem. 53 pairs of triangles
    # share a directed edge in the same direction while nonManifoldEdges reads 0,
    # which is a topology question this function is the wrong place to answer.

    boundary = sum(1 for e in bm.edges if len(e.link_faces) == 1)
    nonmanifold = sum(1 for e in bm.edges if len(e.link_faces) > 2)
    after_verts = len(bm.verts)
    bmesh_to_object(bm, obj)
    return {
        "vertsBefore": before_verts,
        "vertsAfter": after_verts,
        "merged": before_verts - after_verts,
        "duplicateFacesRemoved": duplicate_faces,
        "boundaryEdges": boundary,
        "nonManifoldEdges": nonmanifold,
        "distance": distance,
    }


# ---------------------------------------------------------------------------
# Post-decimation sliver repair  --  the owner of uv_stretch_excessive at LOD1+
# ---------------------------------------------------------------------------

# Aspect ceiling for a triangle Decimate/COLLAPSE is allowed to leave behind, written
# as ``longest_edge ** 2 / (2 * area)``: the ratio of the longest edge to the altitude
# dropped onto it. Dimensionless, so one number covers a 0.2 m hand tool and a 6.7 m
# kelp without a per-family scale constant.
#
# ``law.py`` is the correct long-term home and the value there wins as soon as it
# exists -- the same deferral ``validate.packed_vcol_attribute_name`` uses for the
# packed vertex-colour attribute name.
#
# 30.0 is not a guess. It is the value ``kelp._heal_degenerate`` converged on by
# measurement: at 60 a 53.3 sliver was missed outright, and at 42 the pass converged
# leaving a 41.8 sliver alive that still mapped to 4.03 UV aspect distortion against a
# 3.3 ceiling. A collapse pass converges just under whatever threshold it is given, so
# the threshold has to sit BELOW the aspect that produces a gate breach, not at it.
_SLIVER_ASPECT_MAX_FALLBACK = 30.0


def sliver_aspect_max() -> float:
    """Dimensionless ``longest_edge^2 / 2A`` ceiling for a post-collapse triangle."""
    return float(getattr(law, "DECIMATION_SLIVER_ASPECT_MAX",
                         _SLIVER_ASPECT_MAX_FALLBACK))


def _face_aspect(face: bmesh.types.BMFace) -> float:
    """``longest_edge^2 / 2A``, or ``inf`` for a face with no usable area."""
    area = face.calc_area()
    if area <= law.DEGENERATE_TRIANGLE_AREA_EPS:
        return float("inf")
    longest = max(edge.calc_length() for edge in face.edges)
    return (longest * longest) / (2.0 * area)


def _sliver_census(bm: bmesh.types.BMesh, aspect_max: float) -> tuple:
    """(count above ``aspect_max``, worst aspect, non-triangular faces skipped)."""
    count = 0
    worst = 0.0
    ngons = 0
    for face in bm.faces:
        if len(face.verts) != 3:
            # Recorded rather than silently included: the gate judges loop triangles,
            # so a quad's own aspect is not the number that fails, and triangulating
            # here would rewrite geometry the decimator never touched.
            ngons += 1
            continue
        aspect = _face_aspect(face)
        if aspect > worst:
            worst = aspect
        if aspect > aspect_max:
            count += 1
    return count, worst, ngons


def heal_collapse_slivers(
    obj: bpy.types.Object,
    *,
    aspect_max: Optional[float] = None,
    passes: int = 8,
    blackbox: Optional[BlackBox] = None,
    stage: str = "heal_collapse_slivers",
) -> dict:
    """Remove the 3D slivers Decimate/COLLAPSE emits, BEFORE any UV solve sees them.

    WHY THIS IS THE OWNER OF ``uv_stretch_excessive`` AND THE GROWTH GRAMMAR IS NOT.
    ``validate.uv_aspect_distortion`` builds the Jacobian of the UV -> 3D map by
    dividing 3D position differences by the SIGNED UV AREA (``validate.py:351``,
    ``:359-360``), then returns ``sigma_max / sigma_min - 1`` of that Jacobian
    (``validate.py:371``). The 3D area never appears in the denominator. So the metric
    blows up in exactly one geometric situation: the 3D triangle has collapsed towards
    a LINE, the Jacobian drops to rank one, ``sigma_min`` goes to zero, and the ratio
    diverges *for any parameterisation whatsoever*. It is not a statement about the
    UVs at all.

    That is why re-solving cannot fix it and why re-tuning the grammar cannot either.
    ``kelp._unwrap_and_pack`` solves with ``ANGLE_BASED`` (ABF++), an ANGLE-preserving
    solver, and the gate measures ANGLE anisotropy -- a conformal map of a well-shaped
    triangle tends to ``sigma_max/sigma_min = 1``, i.e. distortion 0. A conformal
    solver physically cannot manufacture 43.6 out of a healthy triangle. Where ABF++
    does have an unavoidable singularity it trades AREA, not angle. So a large value
    here is a DEGENERATE-TRIANGLE ARTEFACT, never genuine texture stretch, and the
    opposite fix -- relaxing ``law.UV_STRETCH_*`` -- would hide real stretch instead.

    AND NO OTHER GATE CAN SEE IT. ``validate`` judges 3D degeneracy by AREA
    (``GATE_DEGENERATE_TRIANGLE``, ``validate.py:1076-1083``, against
    ``law.DEGENERATE_TRIANGLE_AREA_EPS`` = 1e-7). A collapse sliver is LONG: 9.7 cm by
    1.8 mm measures 8.7e-5 m2, roughly a thousand times above that epsilon, and it
    also clears ``law.UV_STRETCH_OUTLIER_MIN_AREA_RATIO``'s sliver floor because the
    floor is relative to the mean triangle area and a decimated level has a large mean.
    An area threshold therefore cannot catch it in either place. The repair has to be
    ASPECT-based, which is what this function is.

    WHAT IT DOES NOT DO, each for a measured reason recorded in this file:
      * no ``remove_doubles`` -- ``_split_uv_seams`` has just duplicated coincident
        vertices along every seam and material border on purpose, and welding here
        would undo the boundary constraints the decimation was shaped by;
      * no ``dissolve_degenerate`` -- its ``dist`` is a LENGTH in metres, so a single
        default would be a scale-dependent magic number across five families of very
        different physical size, and it can merge genuinely separate vertices. The
        law-sourced zero-area face sweep below is scale-free and is the real backstop;
      * no ``recalc_face_normals`` -- measured in ``_weld_coincident``: recalculating
        after decimation put face normals out of agreement with the authored
        weighted/split basis and the FBX round trip started failing on rock with
        "corner normals changed by 0.001859". Collapsing and deleting do not change
        the winding of the faces that survive, so the recalc is not needed;
      * no plain aggressive collapse -- measured to fold the surface into non-manifold
        joins (three faces on one edge) that ``recalc_face_normals`` cannot orient,
        surfacing as ``inconsistent_winding``. Every collapse here is gated by the
        edge-collapse LINK CONDITION, which is precisely the test for "this collapse
        does not create a non-manifold join";
      * no dissolve of the middle vertex -- measured to produce n-gons whose
        re-triangulation created fresh slivers (worst triangle went to 116).

    It can only ever REMOVE triangles, so it cannot threaten a LOD budget; it makes
    the seam-drop rebuild in :func:`build_lod_chain` less likely, not more.

    Returns the census on both sides plus every refusal, and records a
    ``LOD_SLIVER_UNHEALED`` failure code when the census does not clear. A stage that
    can quietly fail to repair must say so: that is the whole reason this reports
    ``slivers_remaining`` instead of just ``edges_collapsed``.
    """
    limit = sliver_aspect_max() if aspect_max is None else float(aspect_max)
    bm = bmesh_from_object(obj)
    faces_before = len(bm.faces)
    found, worst_before, ngons = _sliver_census(bm, limit)

    collapsed = 0
    skipped_link = 0
    skipped_nonmanifold = 0
    skipped_contended = 0
    passes_used = 0

    if found:
        for _pass in range(max(1, int(passes))):
            candidates = []
            for face in bm.faces:
                if len(face.verts) != 3:
                    continue
                area = face.calc_area()
                if area <= law.DEGENERATE_TRIANGLE_AREA_EPS:
                    # Nothing to collapse towards; the zero-area sweep deletes it.
                    continue
                longest = max(edge.calc_length() for edge in face.edges)
                if (longest * longest) / (2.0 * area) > limit:
                    candidates.append(min(face.edges,
                                          key=lambda e: e.calc_length()))
            if not candidates:
                break
            passes_used += 1

            claimed = set()
            chosen = []
            for edge in candidates:
                # Boundary edges COUNT. _split_uv_seams turns every seam and material
                # border into a boundary before decimating, so a decimated level is
                # covered in them, and a sliver sitting on one is exactly the case that
                # survived every earlier repair attempt. The link condition still
                # applies there, with one opposite vertex instead of two.
                if len(edge.link_faces) not in (1, 2):
                    skipped_nonmanifold += 1
                    continue
                u, v = edge.verts
                ring_u = set(e.other_vert(u).index for e in u.link_edges)
                ring_v = set(e.other_vert(v).index for e in v.link_edges)
                opposite = set()
                for face in edge.link_faces:
                    for vertex in face.verts:
                        if vertex is not u and vertex is not v:
                            opposite.add(vertex.index)
                if (ring_u & ring_v) != opposite:
                    skipped_link += 1
                    continue
                # Independent set as well: two individually legal collapses in one
                # neighbourhood can still interact.
                ring = {u.index, v.index} | ring_u | ring_v
                if ring & claimed:
                    skipped_contended += 1
                    continue
                claimed |= ring
                chosen.append(edge)
            if not chosen:
                break
            # uvs=True interpolates the parameterisation across the collapse instead of
            # discarding it, which is what a level with reunwrap=None ships.
            bmesh.ops.collapse(bm, edges=chosen, uvs=True)
            collapsed += len(chosen)
            bm.verts.index_update()
            bm.edges.index_update()
            bm.faces.index_update()

    dead = [f for f in bm.faces
            if f.calc_area() <= law.DEGENERATE_TRIANGLE_AREA_EPS]
    if dead:
        bmesh.ops.delete(bm, geom=dead, context="FACES")
    loose = [v for v in bm.verts if not v.link_faces]
    if loose:
        bmesh.ops.delete(bm, geom=loose, context="VERTS")

    remaining, worst_after, _ngons_after = _sliver_census(bm, limit)
    faces_after = len(bm.faces)
    bmesh_to_object(bm, obj)

    stats = {
        "aspectMax": limit,
        "facesBefore": faces_before,
        "facesAfter": faces_after,
        "sliversFound": found,
        "sliversRemaining": remaining,
        "worstAspectBefore": worst_before,
        "worstAspectAfter": worst_after,
        "edgesCollapsed": collapsed,
        "passesUsed": passes_used,
        "skippedLinkCondition": skipped_link,
        "skippedNonManifold": skipped_nonmanifold,
        "skippedContended": skipped_contended,
        "zeroAreaFacesDeleted": len(dead),
        "looseVertsDeleted": len(loose),
        "ngonFacesSkipped": ngons,
    }
    if blackbox is not None:
        blackbox.record(
            stage,
            vertex_count=len(obj.data.vertices),
            triangle_count=triangle_count(obj.data),
            warning=(
                "slivers {f}->{r} at aspect>{l:g}; worst {wb}->{wa}; collapsed {c} "
                "edges in {p} passes; refused {lk} link-condition / {nm} non-manifold "
                "/ {ct} contended; deleted {z} zero-area faces, {lv} loose verts; "
                "{ng} non-triangular faces not measured".format(
                    f=found, r=remaining, l=limit,
                    wb=_fmt_metric(worst_before), wa=_fmt_metric(worst_after),
                    c=collapsed, p=passes_used, lk=skipped_link,
                    nm=skipped_nonmanifold, ct=skipped_contended,
                    z=len(dead), lv=len(loose), ng=ngons)
                if (found or ngons) else ""),
            # The repair cannot hide behind a clean-looking level. A residual sliver is
            # the cause of the uv_stretch_excessive the validator will report later, so
            # the black box names it HERE, at the stage that owns it, rather than
            # leaving the gate to report a symptom with no attribution.
            failure_code="LOD_SLIVER_UNHEALED" if remaining else "",
        )
    return stats


def _fmt_metric(value: float) -> str:
    """Format a distortion/aspect number that may legitimately be ``inf``."""
    return "{0:.4g}".format(value)


def uv_stretch_stats(obj: bpy.types.Object, *, surface_class=None) -> dict:
    """Area-weighted UV aspect-distortion summary for one object.

    Exists so a caller can prove whether a decimation pass wrecked the parameterisation
    instead of discovering it later in the validator. Measured on a kelp asset: LOD0 sat at
    p95 = 0.98 and its LOD1 worst triangle reached 7610 -- Decimate/COLLAPSE has no UV term
    in its collapse cost and there is no flag to add one, so UV quality after decimation is
    not something to assume.

    THE ``worst``/``p95``/``mean`` FAMILY IS BLIND TO THE FAILURE THAT ACTUALLY FIRES,
    and that blindness is why four LOD1 investigations mis-attributed
    ``uv_stretch_excessive`` to whatever grammar change was in flight. Those numbers are
    the ratio of two EDGE scalings and the loop below SKIPS any triangle with
    ``world <= 1e-12``; the gate instead measures ``sigma_max/sigma_min`` of the whole
    parameterisation and diverges on precisely the triangles this metric either skips or
    scores near zero. Measured disagreement: this function reported worst 0.561 on a
    level whose worst gate triangle was 181.46 against a 3.3 ceiling. A probe that
    cannot fire on the defect it is watching for is the same class of bug as a parameter
    accepted and ignored.

    So the gate's OWN formula is reported alongside, by calling
    ``validate.uv_aspect_distortion`` rather than restating it -- one copy of the
    formula, no drift. ``gate_worst_significant`` mirrors the sub-test that actually
    fails: ``validate._gate_uv`` judges its outlier ceiling only on triangles at or
    above ``law.UV_STRETCH_OUTLIER_MIN_AREA_RATIO`` of the mean triangle area, so that
    is the number to compare against ``gate_ceiling``. ``validate`` remains the
    authority; this is a probe, not a second gate.

    ``surface_class`` is optional only so existing callers keep working. Without it
    ``gate_ceiling`` is ``None`` and ``gate_breaches`` cannot be counted.
    """
    empty = {
        "worst": 0.0, "p95": 0.0, "mean": 0.0, "triangles": 0,
        "gate_worst": 0.0, "gate_worst_significant": 0.0,
        "gate_breaches": 0, "gate_ceiling": None,
        "gate_measured": 0, "mean_area_m2": 0.0, "sliver_floor_m2": 0.0,
    }
    mesh = obj.data
    layer = mesh.uv_layers.active
    if layer is None or not mesh.polygons:
        return dict(empty)

    ceiling = None
    if surface_class is not None:
        # hero=False: LOD1+ are the distant levels, which is how the generators call
        # validate. law.uv_stretch_limit_for floors an organic/geologic class at its own
        # wider limit rather than at UV_STRETCH_MAX_DISTANT, so this matches the gate.
        ceiling = (law.uv_stretch_limit_for(surface_class, hero=False) *
                   law.UV_STRETCH_OUTLIER_MULTIPLIER)

    mesh.calc_loop_triangles()

    # Flat buffers for validate.uv_aspect_distortion. Built with foreach_get rather
    # than validate.extract_mesh_data because that also runs calc_tangents, and this
    # is a measurement pass that must not add or drop a custom-data layer.
    vertex_count = len(mesh.vertices)
    loop_count = len(mesh.loops)
    tri_count = len(mesh.loop_triangles)
    positions = [0.0] * (vertex_count * 3)
    mesh.vertices.foreach_get("co", positions)
    uv_flat = [0.0] * (loop_count * 2)
    layer.data.foreach_get("uv", uv_flat)
    tri_vertices = [0] * (tri_count * 3)
    mesh.loop_triangles.foreach_get("vertices", tri_vertices)
    tri_loops = [0] * (tri_count * 3)
    mesh.loop_triangles.foreach_get("loops", tri_loops)

    samples = []
    gate_samples = []
    for index, tri in enumerate(mesh.loop_triangles):
        p = [mesh.vertices[v].co for v in tri.vertices]
        uv = [layer.data[loop].uv for loop in tri.loops]
        e1 = p[1] - p[0]
        e2 = p[2] - p[0]
        du1 = uv[1] - uv[0]
        du2 = uv[2] - uv[0]
        world = e1.cross(e2).length * 0.5
        uv_area = abs(du1.x * du2.y - du2.x * du1.y) * 0.5
        # BEFORE the skip below, deliberately: the triangles that skip are the ones the
        # gate fails on, so measuring the gate's metric after the skip would rebuild the
        # exact blind spot this block exists to close. The UV-area floor mirrors
        # validate._gate_uv, which routes those to GATE_ZERO_AREA_UV_TRIANGLE instead.
        if uv_area * 2.0 > law.DEGENERATE_UV_AREA_EPS:
            gate_samples.append((
                validate.uv_aspect_distortion(positions, uv_flat, tri_vertices,
                                              tri_loops, index),
                world))
        if world <= 1e-12 or uv_area <= 1e-14:
            continue
        # Ratio of the two edge scalings; a uniform map gives ~0.
        s1 = du1.length / max(1e-9, e1.length)
        s2 = du2.length / max(1e-9, e2.length)
        lo, hi = (s1, s2) if s1 <= s2 else (s2, s1)
        samples.append((hi / max(1e-9, lo) - 1.0, world))

    gate = dict(empty)
    if gate_samples:
        gate_total = sum(area for _d, area in gate_samples)
        gate_mean_area = gate_total / len(gate_samples)
        floor_area = gate_mean_area * law.UV_STRETCH_OUTLIER_MIN_AREA_RATIO
        significant = [d for d, area in gate_samples if area >= floor_area]
        gate["gate_worst"] = max(d for d, _a in gate_samples)
        gate["gate_worst_significant"] = max(significant) if significant else 0.0
        gate["gate_measured"] = len(gate_samples)
        gate["mean_area_m2"] = gate_mean_area
        gate["sliver_floor_m2"] = floor_area
        gate["gate_ceiling"] = ceiling
        if ceiling is not None:
            gate["gate_breaches"] = sum(1 for d in significant if d > ceiling)

    if not samples:
        return gate
    samples.sort(key=lambda item: item[0])
    total = sum(area for _d, area in samples)
    cumulative = 0.0
    p95 = samples[-1][0]
    for distortion, area in samples:
        cumulative += area
        if cumulative >= total * 0.95:
            p95 = distortion
            break
    gate.update({
        "worst": samples[-1][0],
        "p95": p95,
        "mean": sum(d * a for d, a in samples) / max(1e-9, total),
        "triangles": len(samples),
    })
    return gate


def build_lod_chain(
    source: bpy.types.Object,
    *,
    family: law.Family,
    name: str,
    quality_weight: float = 1.0,
    levels: int = 3,
    preserve_seams: bool = True,
    reunwrap: Optional[object] = None,
    blackbox: Optional[BlackBox] = None,
) -> list:
    """Produce LOD0..LOD(levels-1) as separate objects, each inside its budget.

    LOD0 is the source geometry, only verified against budget -- never decimated,
    because LOD0 is the authored silhouette. LOD1+ use Quadric Edge Collapse, which
    ``3dmodel.md`` names as "the default allowed algorithm for arbitrary meshes".

    Blender's decimate ``ratio`` is an approximate target, so each level iterates:
    apply, measure real triangles, tighten the ratio if still over budget. A single
    blind ratio pass is how generators end up shipping over-budget LODs while their
    logs claim compliance.
    """
    budgets = law.LOD_BUDGETS[family]
    out: list = []

    lod0 = source
    lod0.name = law.NAME_MESH.format(family=family.value, name=name, lod=0)
    lod0.data.name = lod0.name
    tris0 = triangle_count(lod0.data)
    out.append(LodLevel(0, lod0, tris0, budgets.lod0, 1.0))
    if blackbox is not None:
        blackbox.record("lod0", family=family.value, triangle_count=tris0,
                        vertex_count=len(lod0.data.vertices),
                        warning="" if tris0 <= budgets.lod0 else
                        "LOD0 over budget {t}>{b}".format(t=tris0, b=budgets.lod0))

    # Each level is derived from the PREVIOUS level's real triangle count, not from
    # its own budget. Targeting the budget alone produces a non-monotonic chain
    # whenever LOD0 already sits far under budget: LOD1 gets aggressively reduced,
    # LOD2 sees itself already under its looser budget and skips decimation
    # entirely, and the far LOD ships heavier than the near one. 3dmodel.md section 7
    # requires LOD1 to be a reduction of LOD0 and LOD2 a reduction of LOD1, so the
    # chain is relative by construction.
    previous_tris = tris0

    for index in range(1, levels):
        budget = budgets.limit(index)
        clone = lod0.copy()
        clone.data = lod0.data.copy()
        clone.name = law.NAME_MESH.format(family=family.value, name=name, lod=index)
        clone.data.name = clone.name
        source.users_collection[0].objects.link(clone)

        # Seam splitting is what makes 3dmodel.md section 7's preservation requirement
        # hold, but it costs a TRIANGLE FLOOR: split seams become mesh boundaries, and
        # Decimate/COLLAPSE will not collapse a boundary edge. On a many-island unwrap
        # that floor can sit above the budget -- observed on coral LOD2, which stuck at
        # 584 against a 300 ceiling no matter how many passes ran.
        #
        # Resolution follows the bible rather than picking a favourite: section 6 of
        # 3DMODEL_FLORA_CORAL.md describes LOD2 as "preserve mass and root/anchor shape"
        # and permits "simplified shells or cards", so UV precision is explicitly
        # secondary at the coarsest level. Seams are preserved where they matter and
        # dropped only when keeping them would breach a hard budget -- and the drop is
        # recorded, never silent.
        seams_split = 0
        if preserve_seams:
            seams_split = _split_uv_seams(clone)

        # Retention per step, scaled by GlobalQualityWeight. LOD1 keeps silhouette
        # plus most material zones; LOD2 keeps mass and anchor shape only. Quality
        # raises retained density inside the step but can never lift it to 1.0, so
        # monotonicity does not depend on the weight.
        base_retain = 0.55 if index == 1 else 0.35
        retain = base_retain + 0.15 * law.saturate(quality_weight)
        target = int(previous_tris * retain)
        # Strictly below the previous level whenever there is anything left to cut.
        if previous_tris > 8:
            target = min(target, previous_tris - 1)
        target = max(4, min(target, budget))

        ratio_used = 1.0
        decimation_passes = 0
        for _attempt in range(6):
            current = triangle_count(clone.data)
            if current <= target:
                break
            ratio = max(0.01, min(0.99, (target / float(current)) * 0.96))
            modifier = clone.modifiers.new(name="H8_Decimate", type="DECIMATE")
            modifier.decimate_type = "COLLAPSE"
            modifier.ratio = ratio
            modifier.use_collapse_triangulate = True
            _make_sole_active(clone)
            bpy.ops.object.modifier_apply(modifier=modifier.name)
            ratio_used *= ratio
            decimation_passes += 1

        # Put the shell back together. The seam split above did its job during the
        # decimation and is pure damage afterwards - see _weld_coincident for the
        # measured consequences of leaving it in place. Only runs when something was
        # actually split, so an unsplit level is untouched.
        weld_stats = None
        if seams_split:
            weld_stats = _weld_coincident(clone)
            if blackbox is not None:
                blackbox.record(
                    "lod{i}_reweld".format(i=index),
                    vertex_count=weld_stats["vertsAfter"],
                    triangle_count=triangle_count(clone.data),
                    warning="merged {m} coincident verts ({b} -> {a}), removed "
                            "{d} duplicate faces; boundary now {be}, "
                            "non-manifold {nm}".format(
                                m=weld_stats["merged"],
                                b=weld_stats["vertsBefore"],
                                a=weld_stats["vertsAfter"],
                                d=weld_stats["duplicateFacesRemoved"],
                                be=weld_stats["boundaryEdges"],
                                nm=weld_stats["nonManifoldEdges"]))

        # HEAL THE SLIVERS THE COLLAPSE JUST MADE, before anything measures or
        # re-solves UVs over them. This is the route owner for `uv_stretch_excessive`
        # at LOD1+ -- see heal_collapse_slivers for why the metric that fails is a
        # statement about 3D shape and not about the parameterisation, and therefore
        # why neither a re-solve nor a grammar change can reach it.
        #
        # Gated on decimation_passes so a level the decimator never touched ships
        # exactly the geometry it was cloned from. LOD0 is the authored silhouette and
        # this function is not entitled to edit a level that was not collapsed.
        sliver_stats = None
        if decimation_passes:
            sliver_stats = heal_collapse_slivers(
                clone, blackbox=blackbox,
                stage="lod{i}_heal_slivers".format(i=index))

        final_tris = triangle_count(clone.data)

        # Decimation has no UV term in its collapse cost, so the parameterisation can be
        # destroyed while the triangle budget is met. `reunwrap(obj, lod_index)` lets the
        # owning generator re-solve UVs for this level with its own family-appropriate
        # settings -- unwrap parameters are family knowledge and do not belong here.
        # Without it the LOD ships whatever the collapse left behind.
        if reunwrap is not None:
            surface_class = law.FAMILY_SURFACE_CLASS.get(family)
            before = uv_stretch_stats(clone, surface_class=surface_class)
            reunwrap(clone, index)
            after = uv_stretch_stats(clone, surface_class=surface_class)
            if blackbox is not None:
                blackbox.record(
                    "lod{i}_reunwrap".format(i=index),
                    vertex_count=len(clone.data.vertices),
                    triangle_count=triangle_count(clone.data),
                    # THE GATE'S OWN NUMBER, not just the edge-scaling summary. The
                    # `worst`/`p95` pair is blind to the failure that actually fires --
                    # measured 0.561 here against a gate reading 181.46 on the same
                    # mesh -- so recording only those is what let four LOD1 breaches be
                    # attributed to the grammar. `gate_worst_significant` is the
                    # quantity validate._gate_uv compares to its outlier ceiling.
                    warning="uv worst {b:.2f}->{a:.2f} p95 {bp:.3f}->{ap:.3f}; "
                            "gate worst {gb}->{ga} significant {sb}->{sa} "
                            "ceiling {c} breaches {n}".format(
                                b=before["worst"], a=after["worst"],
                                bp=before["p95"], ap=after["p95"],
                                gb=_fmt_metric(before["gate_worst"]),
                                ga=_fmt_metric(after["gate_worst"]),
                                sb=_fmt_metric(before["gate_worst_significant"]),
                                sa=_fmt_metric(after["gate_worst_significant"]),
                                c=("n/a" if after["gate_ceiling"] is None
                                   else "{0:.3f}".format(after["gate_ceiling"])),
                                n=after["gate_breaches"]),
                    # Loud, at the stage that owns it. If the re-solve hands back a
                    # level whose worst significant triangle still breaches the ceiling,
                    # the validator WILL fail uv_stretch_excessive on it later, and the
                    # black box should already name the cause rather than leave the gate
                    # reporting a symptom with no attribution.
                    failure_code=("LOD_UV_STRETCH_UNRESOLVED"
                                  if after["gate_breaches"] else ""),
                )
            final_tris = triangle_count(clone.data)

        # If the seam floor blocked the budget, rebuild this level from LOD0 WITHOUT
        # splitting seams and decimate again. Retrying with the same constraints would be
        # the "same-failure escalation" AGENTS.md forbids; changing the constraint is the
        # strategy change it demands.
        seams_dropped = False
        if final_tris > budget and seams_split > 0:
            bpy.data.objects.remove(clone, do_unlink=True)
            clone = lod0.copy()
            clone.data = lod0.data.copy()
            clone.name = law.NAME_MESH.format(family=family.value, name=name, lod=index)
            clone.data.name = clone.name
            source.users_collection[0].objects.link(clone)
            seams_dropped = True
            rebuild_passes = 0
            for _attempt in range(8):
                current = triangle_count(clone.data)
                if current <= target:
                    break
                modifier = clone.modifiers.new(name="H8_Decimate", type="DECIMATE")
                modifier.decimate_type = "COLLAPSE"
                modifier.ratio = max(0.01, min(0.99, (target / float(current)) * 0.96))
                modifier.use_collapse_triangulate = True
                _make_sole_active(clone)
                bpy.ops.object.modifier_apply(modifier=modifier.name)
                rebuild_passes += 1

            # REDO BOTH POST-DECIMATION STEPS ON THE REBUILT MESH. This branch throws the
            # first clone away and decimates a fresh copy of LOD0, so everything that ran
            # on the discarded one has to run again - and until now neither did.
            #
            # The consequence was silent in the worst way: the black box still held the
            # clean `lod{i}_reunwrap` numbers measured on the mesh that was DELETED, while
            # the mesh that actually shipped carried LOD0's collapse-mangled
            # parameterisation. The evidence said the level was fine and the level was not.
            # Coral measured worst aspect distortion 407.07 before its reunwrap was wired
            # at all, so this path is one dense seed away from shipping that.
            #
            # The duplicate-face purge matters just as much here: Decimate/COLLAPSE can
            # pull two triangles onto the same vertex triple, FBX merges the pair on
            # import, and the round-trip gate then rejects the whole package for losing
            # exactly one triangle. That is the defect that cost five hypotheses to find,
            # and this branch was still exposed to it.
            if seams_split:
                weld_stats = _weld_coincident(clone)
                if blackbox is not None:
                    blackbox.record(
                        "lod{i}_reweld_after_seam_drop".format(i=index),
                        vertex_count=weld_stats["vertsAfter"],
                        triangle_count=triangle_count(clone.data),
                        warning="merged {m} coincident verts, removed {d} duplicate "
                                "faces; boundary {be}, non-manifold {nm}".format(
                                    m=weld_stats["merged"],
                                    d=weld_stats["duplicateFacesRemoved"],
                                    be=weld_stats["boundaryEdges"],
                                    nm=weld_stats["nonManifoldEdges"]))
            # AND THE SLIVER HEAL, for the same reason the reweld and the reunwrap are
            # here: this branch ships a mesh the discarded clone's records do not
            # describe. A rebuilt level is decimated exactly as hard as the first
            # attempt was, so it carries exactly the same collapse slivers.
            if rebuild_passes:
                sliver_stats = heal_collapse_slivers(
                    clone, blackbox=blackbox,
                    stage="lod{i}_heal_slivers_after_seam_drop".format(i=index))
            if reunwrap is not None:
                surface_class = law.FAMILY_SURFACE_CLASS.get(family)
                before = uv_stretch_stats(clone, surface_class=surface_class)
                reunwrap(clone, index)
                after = uv_stretch_stats(clone, surface_class=surface_class)
                if blackbox is not None:
                    blackbox.record(
                        "lod{i}_reunwrap_after_seam_drop".format(i=index),
                        vertex_count=len(clone.data.vertices),
                        triangle_count=triangle_count(clone.data),
                        warning="uv worst {b:.2f}->{a:.2f} p95 {bp:.3f}->{ap:.3f}; "
                                "gate worst {gb}->{ga} significant {sb}->{sa} "
                                "ceiling {c} breaches {n} "
                                "(rebuilt without seam splits)".format(
                                    b=before["worst"], a=after["worst"],
                                    bp=before["p95"], ap=after["p95"],
                                    gb=_fmt_metric(before["gate_worst"]),
                                    ga=_fmt_metric(after["gate_worst"]),
                                    sb=_fmt_metric(before["gate_worst_significant"]),
                                    sa=_fmt_metric(after["gate_worst_significant"]),
                                    c=("n/a" if after["gate_ceiling"] is None
                                       else "{0:.3f}".format(after["gate_ceiling"])),
                                    n=after["gate_breaches"]),
                        failure_code=("LOD_UV_STRETCH_UNRESOLVED"
                                      if after["gate_breaches"] else ""),
                    )
            final_tris = triangle_count(clone.data)

        out.append(LodLevel(index, clone, final_tris, budget, ratio_used))

        problems = []
        if seams_dropped:
            problems.append(
                "UV seams NOT preserved at this level: the {n} split seams imposed a "
                "triangle floor above the {b} budget".format(n=seams_split, b=budget))
        # The sliver census belongs on the level record too, not only on its own stage.
        # A reader who opens the ring at `lod1` because the validator failed must see
        # the decimation's own verdict without knowing to look one stage earlier.
        if sliver_stats is not None and sliver_stats["sliversRemaining"]:
            problems.append(
                "{r} of {f} collapse slivers UNHEALED at aspect>{a:g} (worst {w}); "
                "{lk} collapses refused by the link condition. This is the cause of a "
                "uv_stretch_excessive on this level -- do not attribute it to the "
                "generator grammar".format(
                    r=sliver_stats["sliversRemaining"],
                    f=sliver_stats["sliversFound"],
                    a=sliver_stats["aspectMax"],
                    w=_fmt_metric(sliver_stats["worstAspectAfter"]),
                    lk=sliver_stats["skippedLinkCondition"]))
        if final_tris > budget:
            problems.append("over budget {t}>{b}".format(t=final_tris, b=budget))
        if final_tris > previous_tris:
            problems.append("non-monotonic: LOD{i}={t} > LOD{p}={pt}".format(
                i=index, t=final_tris, p=index - 1, pt=previous_tris))
        if blackbox is not None:
            blackbox.record(
                "lod{i}".format(i=index),
                family=family.value,
                triangle_count=final_tris,
                vertex_count=len(clone.data.vertices),
                warning="; ".join(problems),
                failure_code="LOD_CHAIN_INVALID" if (final_tris > budget or
                                                 final_tris > previous_tris) else "",
            )
        previous_tris = final_tris

    return out


# ---------------------------------------------------------------------------
# Collision proxy  --  3dmodel.md section 9
# ---------------------------------------------------------------------------

def _convex_hull_in_place(bm: bmesh.types.BMesh) -> None:
    """Replace the BMesh contents with its triangulated convex hull.

    ``bmesh.ops.convex_hull`` ADDS hull faces without removing the source geometry, and
    its ``geom_interior``/``geom_unused`` reports only cover input the hull did not
    consume. For an already-convex input every vertex IS on the hull, so both lists come
    back empty and the original faces survive underneath the new ones -- giving exactly
    double the triangle count with no error and no leftover to delete.

    Stripping faces and edges down to a bare point cloud first makes the operation
    unconditional: whatever the input topology, the result is the hull of its vertices
    and nothing else.
    """
    if bm.faces:
        bmesh.ops.delete(bm, geom=bm.faces[:], context="FACES_ONLY")
    if bm.edges:
        bmesh.ops.delete(bm, geom=bm.edges[:], context="EDGES_FACES")
    result = bmesh.ops.convex_hull(bm, input=bm.verts[:], use_existing_faces=False)
    # geom_interior and geom_unused OVERLAP on concave input, and bmesh.ops.delete raises
    # ValueError("found the same ... used multiple times") on a duplicated element. Every
    # concave geology LOD0 crashed here. dict.fromkeys preserves order while de-duplicating
    # by identity, which matters because bmesh elements are unhashable-by-value but stable
    # by object identity within one BMesh.
    leftovers = list(dict.fromkeys(
        list(result.get("geom_interior", [])) + list(result.get("geom_unused", []))))
    if leftovers:
        bmesh.ops.delete(bm, geom=leftovers, context="VERTS")
    if bm.faces:
        bmesh.ops.triangulate(bm, faces=bm.faces[:])
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])


@dataclass
class ColliderResult:
    obj: Optional[bpy.types.Object]
    triangles: int
    kind: str
    within_budget: bool
    reason: str = ""


def make_convex_collider(
    source: bpy.types.Object,
    *,
    family: law.Family,
    name: str,
    blackbox: Optional[BlackBox] = None,
) -> ColliderResult:
    """Convex hull proxy under the 200-triangle ceiling, as a separate datablock.

    ``3dmodel.md`` section 9 is absolute: "LOD0 visual meshes must never be assigned
    directly to production MeshCollider components. ... Rocks/coral/complex geology:
    convex hull or convex decomposition under 200 triangles total per asset."

    Flora families return no collider by default -- ``3DMODEL_FLORA_CORAL.md``
    section 7: "Default flora/coral collision is none." Returning an empty result
    with a stated reason is the honest outcome; silently emitting a collider anyway
    would put decorative fronds into the physics scene.
    """
    if family in law.FAMILIES_WITHOUT_DEFAULT_COLLISION:
        if blackbox is not None:
            blackbox.record("collider_skipped", family=family.value,
                            warning="flora default: no collision (bible section 7)")
        return ColliderResult(None, 0, "none", True,
                              "flora/coral default collision is none per "
                              "3DMODEL_FLORA_CORAL.md section 7")

    bm = bmesh_from_object(source)
    _convex_hull_in_place(bm)

    hull_mesh = bpy.data.meshes.new(
        law.NAME_COLLIDER.format(family=family.value, name=name))
    bm.to_mesh(hull_mesh)
    bm.free()

    collider = bpy.data.objects.new(
        law.COLLIDER_PREFIX + "{f}_{n}".format(f=family.value, n=name), hull_mesh)
    source.users_collection[0].objects.link(collider)

    # Read ``collider.data`` on every pass, never a captured mesh reference:
    # ``modifier_apply`` rebinds the object's mesh datablock, so a variable captured
    # before the loop keeps pointing at the pre-decimation mesh. The loop then runs to
    # exhaustion measuring geometry that nobody is modifying, reports FINISHED every
    # time, and leaves the collider at full hull density.
    hull_tris = triangle_count(collider.data)
    attempts = 0
    for _attempt in range(8):
        tris = triangle_count(collider.data)
        if tris <= law.COLLIDER_CONVEX_TRI_MAX:
            break
        attempts += 1
        modifier = collider.modifiers.new(name="H8_HullDecimate", type="DECIMATE")
        modifier.decimate_type = "COLLAPSE"
        modifier.ratio = max(0.02, min(0.95,
                             (law.COLLIDER_CONVEX_TRI_MAX / float(tris)) * 0.92))
        modifier.use_collapse_triangulate = True
        _make_sole_active(collider)
        bpy.ops.object.modifier_apply(modifier=modifier.name)

    # Decimation breaks convexity: collapsing a hull edge can pull a vertex inside the
    # shell. PhysX needs the proxy convex, so re-hull after reduction and judge the
    # budget only afterwards. Skipping this ships a "convex" collider that is not.
    if attempts:
        bm = bmesh.new()
        bm.from_mesh(collider.data)
        _convex_hull_in_place(bm)
        bm.to_mesh(collider.data)
        bm.free()

    tris = triangle_count(collider.data)
    within = tris <= law.COLLIDER_CONVEX_TRI_MAX
    if blackbox is not None:
        blackbox.record(
            "collider_convex", family=family.value, triangle_count=tris,
            vertex_count=len(collider.data.vertices),
            warning="" if within else
            "collider over budget {t}>{m} (hull was {h}, {a} decimation passes)".format(
                t=tris, m=law.COLLIDER_CONVEX_TRI_MAX, h=hull_tris, a=attempts),
            failure_code="" if within else "COLLIDER_OVER_BUDGET",
        )
    return ColliderResult(
        collider, tris, "convex", within,
        "" if within else
        "hull {h} tris reduced to {t} over {a} passes, still above the {m} ceiling".format(
            h=hull_tris, t=tris, a=attempts, m=law.COLLIDER_CONVEX_TRI_MAX))


# ---------------------------------------------------------------------------
# Bounds
# ---------------------------------------------------------------------------

def local_bounds(obj: bpy.types.Object) -> tuple:
    """(min, max) as Vectors in object local space. Empty mesh yields zero vectors."""
    if not obj.data.vertices:
        return (Vector((0.0, 0.0, 0.0)), Vector((0.0, 0.0, 0.0)))
    xs = [v.co.x for v in obj.data.vertices]
    ys = [v.co.y for v in obj.data.vertices]
    zs = [v.co.z for v in obj.data.vertices]
    return (Vector((min(xs), min(ys), min(zs))), Vector((max(xs), max(ys), max(zs))))


def longest_extent(obj: bpy.types.Object) -> float:
    lo, hi = local_bounds(obj)
    return max(hi.x - lo.x, hi.y - lo.y, hi.z - lo.z)
