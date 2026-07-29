"""Probe: WHY does ``inconsistent_winding`` fire while ``topology_report`` reads clean?

Not a generator and not a gate. A measurement harness for one question, written
because four hypotheses on the adjacent round-trip defect in this pipeline were
each refuted only after somebody read a number instead of assuming a mechanism.

It hooks ``validate.validate_mesh`` so the analysis runs on the EXACT datablock,
at the EXACT stage, that the gate runs on -- which is the only way to settle
"the gate reads a different mesh than the one that was repaired" by construction
rather than by argument.

Reported per LOD:

*   polygon size histogram, so "the gate triangulates n-gons the topology report
    never sees" is a number rather than a theory;
*   duplicate entries in ``mesh.edges`` for one vertex pair, which no
    non-manifold count can see because each copy carries one face;
*   the full directed-edge duplicate list the gate builds, classified: same
    polygon or different, real mesh edge or triangulation diagonal only, and how
    many polygons traverse the pair each way;
*   an ORIENTABILITY test -- flood-fill a consistent orientation across
    manifold edges and count the edges that contradict it. That is precisely
    what ``bmesh.ops.recalc_face_normals`` can do, so a non-zero count is the
    only shape of defect recalc is powerless against, and a zero count REFUTES
    the "winding genuinely disagrees" hypothesis outright;
*   ``mesh_ops.topology_report`` beside it, from the same datablock.

Run:
    blender.exe -b --factory-startup -P Tools/Blender/generators/_probe_winding.py \
        -- --seed 1712 --quality 1.0 --no-preview
Any argument the coral generator accepts is forwarded.
"""

from __future__ import annotations

import os
import sys
from collections import Counter

import bmesh  # noqa: F401  (imported for its side effect of being available)

_HERE = os.path.dirname(os.path.abspath(__file__))
_TOOLS = os.path.dirname(_HERE)
if _TOOLS not in sys.path:
    sys.path.insert(0, _TOOLS)
if _HERE not in sys.path:
    sys.path.insert(0, _HERE)

from h8forge import mesh_ops, validate  # noqa: E402

DETAIL_PAIRS = 4


def _polygon_directed_pairs(mesh):
    """Map directed corner pair -> list of polygon indices that traverse it.

    Built from ``mesh.polygons`` / ``mesh.loops``, i.e. the topology bmesh sees,
    NOT from ``loop_triangles``. Comparing the two maps is the whole point.
    """
    out = {}
    loops = mesh.loops
    for polygon in mesh.polygons:
        start = polygon.loop_start
        total = polygon.loop_total
        for k in range(total):
            a = loops[start + k].vertex_index
            b = loops[start + (k + 1) % total].vertex_index
            out.setdefault((a, b), []).append(polygon.index)
    return out


def _edge_pair_multiplicity(mesh):
    counter = Counter()
    for edge in mesh.edges:
        a, b = edge.vertices[0], edge.vertices[1]
        counter[(min(a, b), max(a, b))] += 1
    return counter


def _loop_direction_in_face(face, edge):
    """(start, end) vertex indices for ``edge`` as ``face`` traverses it."""
    for loop in face.loops:
        if loop.edge is edge:
            return (loop.vert.index, loop.link_loop_next.vert.index)
    return None


def _orientability(mesh):
    """Flood-fill one consistent orientation; return the edges that contradict it.

    This is what ``recalc_face_normals`` is able to do: group faces across
    manifold edges, orient each group consistently, then point it outward. If a
    connected group is non-orientable no assignment exists and some edge must
    stay inconsistent -- recalc cannot fix that, and every edge still carries
    exactly two faces so ``nonmanifold_edges`` reads 0.

    Returns (contradicting_edge_keys, groups, faces_visited).
    """
    bm = bmesh.new()
    bm.from_mesh(mesh)
    try:
        bm.faces.ensure_lookup_table()
        bm.edges.ensure_lookup_table()
        flip = {}
        contradictions = set()
        groups = 0
        for seed in bm.faces:
            if seed.index in flip:
                continue
            groups += 1
            flip[seed.index] = False
            stack = [seed]
            while stack:
                face = stack.pop()
                for edge in face.edges:
                    linked = edge.link_faces
                    if len(linked) != 2:
                        continue
                    other = linked[0] if linked[1] is face else linked[1]
                    here = _loop_direction_in_face(face, edge)
                    there = _loop_direction_in_face(other, edge)
                    if here is None or there is None:
                        continue
                    # Same start vertex => both faces traverse the edge the same
                    # way => one of them must be flipped relative to the other.
                    same_direction = here[0] == there[0]
                    want = flip[face.index] ^ same_direction
                    if other.index not in flip:
                        flip[other.index] = want
                        stack.append(other)
                    elif flip[other.index] != want:
                        contradictions.add(
                            (min(edge.verts[0].index, edge.verts[1].index),
                             max(edge.verts[0].index, edge.verts[1].index)))
        return contradictions, groups, len(flip)
    finally:
        bm.free()


def _inconsistent_adjacencies(mesh):
    """Edges whose two faces traverse them the SAME way, with geometry context.

    The gate reports this set as occurrences. Here each one carries the dot
    product of the two face normals and both areas, because "inverted face" and
    "zero-thickness fold welded onto itself" are different defects with different
    owners and the count alone cannot tell them apart.
    """
    bm = bmesh.new()
    bm.from_mesh(mesh)
    try:
        out = []
        self_linked = 0
        for edge in bm.edges:
            linked = edge.link_faces
            if len(linked) != 2:
                continue
            if linked[0] is linked[1]:
                self_linked += 1
                continue
            here = _loop_direction_in_face(linked[0], edge)
            there = _loop_direction_in_face(linked[1], edge)
            if here is None or there is None:
                continue
            if here[0] == there[0]:
                out.append((
                    min(edge.verts[0].index, edge.verts[1].index),
                    max(edge.verts[0].index, edge.verts[1].index),
                    linked[0].index, linked[1].index,
                    linked[0].normal.dot(linked[1].normal),
                    edge.calc_length(),
                    linked[0].calc_area(), linked[1].calc_area()))
        return out, self_linked
    finally:
        bm.free()


def _cluster_sites(pairs):
    """Group inconsistent edges that touch, so 53 edges resolve to N sites."""
    parent = {}

    def find(x):
        parent.setdefault(x, x)
        while parent[x] != x:
            parent[x] = parent[parent[x]]
            x = parent[x]
        return x

    def union(a, b):
        ra, rb = find(a), find(b)
        if ra != rb:
            parent[rb] = ra

    for entry in pairs:
        union(("v", entry[0]), ("v", entry[1]))
        union(("e", entry[0], entry[1]), ("v", entry[0]))
    roots = set()
    for entry in pairs:
        roots.add(find(("e", entry[0], entry[1])))
    return len(roots)


def _flip_census(mesh):
    """How many faces the best flood-filled orientation would have to flip."""
    bm = bmesh.new()
    bm.from_mesh(mesh)
    try:
        bm.faces.ensure_lookup_table()
        flip = {}
        per_group = []
        for seed in bm.faces:
            if seed.index in flip:
                continue
            flip[seed.index] = False
            group = [seed.index]
            stack = [seed]
            while stack:
                face = stack.pop()
                for edge in face.edges:
                    linked = edge.link_faces
                    if len(linked) != 2 or linked[0] is linked[1]:
                        continue
                    other = linked[0] if linked[1] is face else linked[1]
                    here = _loop_direction_in_face(face, edge)
                    there = _loop_direction_in_face(other, edge)
                    if here is None or there is None:
                        continue
                    want = flip[face.index] ^ (here[0] == there[0])
                    if other.index not in flip:
                        flip[other.index] = want
                        group.append(other.index)
                        stack.append(other)
            flipped = sum(1 for index in group if flip[index])
            per_group.append((len(group), min(flipped, len(group) - flipped)))
        return per_group
    finally:
        bm.free()


def _gate_duplicate_count(mesh) -> int:
    mesh.calc_loop_triangles()
    seen = set()
    duplicates = 0
    for triangle in mesh.loop_triangles:
        i0, i1, i2 = (triangle.vertices[0], triangle.vertices[1],
                      triangle.vertices[2])
        for a, b in ((i0, i1), (i1, i2), (i2, i0)):
            if (a, b) in seen:
                duplicates += 1
            else:
                seen.add((a, b))
    return duplicates


def _recalc_test(mesh, tag: str) -> None:
    """Run recalc_face_normals on a COPY and re-measure. The decisive test.

    If the surface is orientable, recalc drives the gate count to 0. If it is
    non-orientable, no assignment of per-face orientations exists that makes
    every shared edge consistent, so the count cannot reach 0 and the previously
    measured "54 -> 53, recalc fixed nothing" is a property of the mesh rather
    than a bug in how recalc was called.
    """
    import bpy
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    scratch = bpy.data.meshes.new("_probe_recalc_scratch")
    bm.to_mesh(scratch)
    bm.free()
    try:
        after_gate = _gate_duplicate_count(scratch)
        after_contradictions, _groups, _visited = _orientability(scratch)
        print("PROBE %s AFTER recalc_face_normals: gate_duplicate_directed_"
              "edges=%d contradicting_manifold_edges=%d" % (
                  tag, after_gate, len(after_contradictions)))
    finally:
        bpy.data.meshes.remove(scratch)


def _weld_steps(source_bm, index: int) -> None:
    """Replay mesh_ops.weld_and_clean one operator at a time on a COPY.

    Orientability is the property under test, and only three of the seven steps
    can move it: ``remove_doubles`` glues faces along newly shared edges, the
    interior-sheet deletion converts an UNTRAVERSED non-manifold edge into a
    traversed 2-face one, and ``holes_fill`` adds faces that bridge rims. This
    names which one instead of leaving it to elimination.
    """
    import bpy
    from h8forge import law as forge_law

    scratch = bpy.data.meshes.new("_probe_step_scratch")
    source_bm.to_mesh(scratch)
    bm = bmesh.new()
    bm.from_mesh(scratch)

    def report(label):
        probe = bpy.data.meshes.new("_probe_step_measure")
        bm.to_mesh(probe)
        try:
            contradictions, groups, _visited = _orientability(probe)
            adjacencies, _self_linked = _inconsistent_adjacencies(probe)
            nonmanifold = sum(1 for e in bm.edges if len(e.link_faces) > 2)
            boundary = sum(1 for e in bm.edges if len(e.link_faces) == 1)
            print("PROBE WELDSTEP%d %-26s faces=%d contradicting_manifold_"
                  "edges=%d inconsistent_adjacencies=%d groups=%d boundary=%d "
                  "nonmanifold=%d" % (
                      index, label, len(bm.faces), len(contradictions),
                      len(adjacencies), groups, boundary, nonmanifold))
        finally:
            bpy.data.meshes.remove(probe)

    try:
        report("0_input")
        bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-4)
        report("1_remove_doubles")
        bmesh.ops.dissolve_degenerate(bm, dist=1e-4, edges=bm.edges[:])
        report("2_dissolve_degenerate")
        dead = [f for f in bm.faces
                if f.calc_area() <= forge_law.DEGENERATE_TRIANGLE_AREA_EPS]
        if dead:
            bmesh.ops.delete(bm, geom=dead, context="FACES")
        report("3_delete_zero_area")
        loose = [v for v in bm.verts if not v.link_faces]
        if loose:
            bmesh.ops.delete(bm, geom=loose, context="VERTS")
        report("4_delete_loose_verts")
        doomed = set()
        for edge in bm.edges:
            linked = edge.link_faces
            if len(linked) <= 2:
                continue
            ordered = sorted(linked, key=lambda f: f.calc_area(), reverse=True)
            for face in ordered[2:]:
                doomed.add(face)
        if doomed:
            bmesh.ops.delete(bm, geom=list(doomed), context="FACES")
            orphans = [v for v in bm.verts if not v.link_faces]
            if orphans:
                bmesh.ops.delete(bm, geom=orphans, context="VERTS")
        report("5_delete_interior_sheets")
        remaining = set(e for e in bm.edges if len(e.link_faces) == 1)
        filled = 0
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
                filled += 1
                report("6_holes_fill_loop%d(%d_edges)" % (filled, len(loop)))
            except (RuntimeError, ValueError):
                pass
        report("6_holes_fill_done")
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
        report("7_recalc_face_normals")
    finally:
        bm.free()
        bpy.data.meshes.remove(scratch)


def _weld_steps_candidate(source_bm, index: int) -> None:
    """Same sequence, but the interior-sheet deletion is ORIENTATION AWARE.

    ``weld_and_clean`` keeps "the two largest faces" at an edge shared by three
    or more. Area is the wrong tie-break for this: whether the surviving pair can
    be consistently oriented depends on which DIRECTION each of them traverses
    the shared edge, and the area sort has no orientation term. Keeping the
    largest face plus the largest face that traverses the edge the OTHER way
    leaves a pair that a later ``recalc_face_normals`` can reconcile.

    Filling a hole in an orientable surface with a disk cannot make it
    non-orientable, so if this step is the sole creator, the whole sequence must
    come out at zero contradictions.
    """
    import bpy
    from h8forge import law as forge_law

    scratch = bpy.data.meshes.new("_probe_cand_scratch")
    source_bm.to_mesh(scratch)
    bm = bmesh.new()
    bm.from_mesh(scratch)

    def report(label):
        probe = bpy.data.meshes.new("_probe_cand_measure")
        bm.to_mesh(probe)
        try:
            contradictions, groups, _visited = _orientability(probe)
            adjacencies, _self_linked = _inconsistent_adjacencies(probe)
            print("PROBE CANDSTEP%d %-26s faces=%d contradicting_manifold_"
                  "edges=%d inconsistent_adjacencies=%d groups=%d boundary=%d "
                  "nonmanifold=%d" % (
                      index, label, len(bm.faces), len(contradictions),
                      len(adjacencies), groups,
                      sum(1 for e in bm.edges if len(e.link_faces) == 1),
                      sum(1 for e in bm.edges if len(e.link_faces) > 2)))
        finally:
            bpy.data.meshes.remove(probe)

    try:
        bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-4)
        bmesh.ops.dissolve_degenerate(bm, dist=1e-4, edges=bm.edges[:])
        dead = [f for f in bm.faces
                if f.calc_area() <= forge_law.DEGENERATE_TRIANGLE_AREA_EPS]
        if dead:
            bmesh.ops.delete(bm, geom=dead, context="FACES")
        loose = [v for v in bm.verts if not v.link_faces]
        if loose:
            bmesh.ops.delete(bm, geom=loose, context="VERTS")
        report("4_delete_loose_verts")

        doomed = set()
        for edge in bm.edges:
            linked = edge.link_faces
            if len(linked) <= 2:
                continue
            start = edge.verts[0].index
            forward, backward = [], []
            for face in linked:
                direction = _loop_direction_in_face(face, edge)
                (forward if direction is not None and direction[0] == start
                 else backward).append(face)
            forward.sort(key=lambda f: f.calc_area(), reverse=True)
            backward.sort(key=lambda f: f.calc_area(), reverse=True)
            print("PROBE CANDSTEP%d nonmanifold edge(%d,%d) faces=%d "
                  "direction_split=%d/%d" % (
                      index, edge.verts[0].index, edge.verts[1].index,
                      len(linked), len(forward), len(backward)))
            if forward and backward:
                keep = {forward[0], backward[0]}
            else:
                ordered = sorted(linked, key=lambda f: f.calc_area(),
                                 reverse=True)
                keep = set(ordered[:2])
            for face in linked:
                if face not in keep:
                    doomed.add(face)
        if doomed:
            bmesh.ops.delete(bm, geom=list(doomed), context="FACES")
            orphans = [v for v in bm.verts if not v.link_faces]
            if orphans:
                bmesh.ops.delete(bm, geom=orphans, context="VERTS")
        report("5_delete_interior_ORIENTED")

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
            except (RuntimeError, ValueError):
                pass
        report("6_holes_fill_done")
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
        report("7_recalc_face_normals")
    finally:
        bm.free()
        bpy.data.meshes.remove(scratch)


def analyse(mesh, tag: str) -> None:
    mesh.calc_loop_triangles()
    tris = mesh.loop_triangles
    print("")
    print("PROBE %s  ===============================================" % tag)
    print("PROBE %s datablock='%s' verts=%d edges=%d polygons=%d loops=%d "
          "loop_triangles=%d" % (tag, mesh.name, len(mesh.vertices),
                                 len(mesh.edges), len(mesh.polygons),
                                 len(mesh.loops), len(tris)))

    histogram = Counter(polygon.loop_total for polygon in mesh.polygons)
    print("PROBE %s polygon_size_histogram=%s  (ngons=%d)" % (
        tag, dict(sorted(histogram.items())),
        sum(count for size, count in histogram.items() if size > 3)))

    multiplicity = _edge_pair_multiplicity(mesh)
    duplicated_edges = {key: count for key, count in multiplicity.items()
                        if count > 1}
    print("PROBE %s mesh.edges duplicate_vertex_pairs=%d %s" % (
        tag, len(duplicated_edges),
        sorted(duplicated_edges.items())[:6] if duplicated_edges else ""))

    # Duplicate faces by vertex set, and by ORDERED cycle, separately: an
    # identical cycle is a same-winding double, a reversed cycle is not.
    by_set = Counter()
    for polygon in mesh.polygons:
        by_set[tuple(sorted(polygon.vertices))] += 1
    duplicate_face_sets = sum(count - 1 for count in by_set.values()
                              if count > 1)
    print("PROBE %s duplicate_faces_by_vertex_set=%d" % (tag,
                                                         duplicate_face_sets))

    polygon_pairs = _polygon_directed_pairs(mesh)

    # The gate's own map, rebuilt verbatim from validate._gate_triangles.
    seen = {}
    duplicates = []
    for index in range(len(tris)):
        triangle = tris[index]
        i0, i1, i2 = triangle.vertices[0], triangle.vertices[1], triangle.vertices[2]
        for a, b in ((i0, i1), (i1, i2), (i2, i0)):
            previous = seen.get((a, b))
            if previous is None:
                seen[(a, b)] = index
            else:
                duplicates.append((a, b, previous, index))
    print("PROBE %s gate_duplicate_directed_edges=%d" % (tag, len(duplicates)))

    same_polygon = 0
    real_edge = 0
    diagonal_only = 0
    involves_ngon = 0
    for a, b, first, second in duplicates:
        poly_first = tris[first].polygon_index
        poly_second = tris[second].polygon_index
        if poly_first == poly_second:
            same_polygon += 1
        key = (min(a, b), max(a, b))
        if key in multiplicity:
            real_edge += 1
        else:
            diagonal_only += 1
        if (mesh.polygons[poly_first].loop_total > 3
                or mesh.polygons[poly_second].loop_total > 3):
            involves_ngon += 1
    print("PROBE %s classify: same_polygon=%d different_polygon=%d "
          "pair_is_real_mesh_edge=%d pair_is_triangulation_diagonal_only=%d "
          "at_least_one_ngon=%d" % (tag, same_polygon,
                                    len(duplicates) - same_polygon, real_edge,
                                    diagonal_only, involves_ngon))

    contradictions, groups, visited = _orientability(mesh)
    print("PROBE %s orientability: face_groups=%d faces_visited=%d "
          "contradicting_manifold_edges=%d %s" % (
              tag, groups, visited, len(contradictions),
              sorted(contradictions)[:6] if contradictions else ""))

    adjacencies, self_linked = _inconsistent_adjacencies(mesh)
    negative = sum(1 for entry in adjacencies if entry[4] < 0.0)
    near_fold = sum(1 for entry in adjacencies if entry[4] < -0.9)
    faces_involved = set()
    for entry in adjacencies:
        faces_involved.add(entry[2])
        faces_involved.add(entry[3])
    print("PROBE %s inconsistent_adjacencies=%d sites=%d faces_involved=%d "
          "normal_dot_negative=%d normal_dot_below_-0.9=%d "
          "edges_with_one_face_twice=%d" % (
              tag, len(adjacencies), _cluster_sites(adjacencies),
              len(faces_involved), negative, near_fold, self_linked))
    for entry in adjacencies[:DETAIL_PAIRS]:
        print("PROBE %s   adjacency edge(%d,%d) faces=(%d,%d) normal_dot=%+.6f "
              "edge_len=%.6g areas=(%.6g, %.6g)" % (
                  tag, entry[0], entry[1], entry[2], entry[3], entry[4],
                  entry[5], entry[6], entry[7]))
    census = _flip_census(mesh)
    print("PROBE %s flip_census (group_faces, faces_needing_flip)=%s" % (
        tag, census[:8]))
    _recalc_test(mesh, tag)

    census = mesh_ops.topology_report_for_mesh(mesh) \
        if hasattr(mesh_ops, "topology_report_for_mesh") else None
    if census is not None:
        print("PROBE %s topology_report tris=%d faces=%d components=%d "
              "boundary=%d nonmanifold=%d" % (
                  tag, census.triangles, census.faces, census.components,
                  census.boundary_edges, census.nonmanifold_edges))
    else:
        # topology_report takes an object; recreate its two numbers here from the
        # datablock so the comparison is against the same bytes.
        bm = bmesh.new()
        bm.from_mesh(mesh)
        try:
            boundary = sum(1 for e in bm.edges if len(e.link_faces) == 1)
            nonmanifold = sum(1 for e in bm.edges if len(e.link_faces) > 2)
            face_link = Counter()
            for edge in bm.edges:
                face_link[len(edge.link_faces)] += 1
            print("PROBE %s edge_face_count_histogram=%s boundary=%d "
                  "nonmanifold=%d" % (tag, dict(sorted(face_link.items())),
                                      boundary, nonmanifold))
        finally:
            bm.free()

    for a, b, first, second in duplicates[:DETAIL_PAIRS]:
        poly_first = tris[first].polygon_index
        poly_second = tris[second].polygon_index
        key = (min(a, b), max(a, b))
        print("PROBE %s DETAIL directed edge (%d -> %d)" % (tag, a, b))
        for label, index in (("first", first), ("second", second)):
            triangle = tris[index]
            print("PROBE %s   %s triangle[%d] verts=%s loops=%s "
                  "polygon_index=%d area=%.9g" % (
                      tag, label, index, tuple(triangle.vertices),
                      tuple(triangle.loops), triangle.polygon_index,
                      triangle.area))
        for label, poly_index in (("first", poly_first), ("second", poly_second)):
            polygon = mesh.polygons[poly_index]
            corner_verts = tuple(
                mesh.loops[polygon.loop_start + k].vertex_index
                for k in range(polygon.loop_total))
            corner_loops = tuple(polygon.loop_start + k
                                 for k in range(polygon.loop_total))
            print("PROBE %s   %s polygon[%d] loop_total=%d verts=%s loops=%s" % (
                tag, label, poly_index, polygon.loop_total, corner_verts,
                corner_loops))
        print("PROBE %s   mesh.edges entries for pair %s = %d" % (
            tag, key, multiplicity.get(key, 0)))
        forward = polygon_pairs.get((a, b), [])
        backward = polygon_pairs.get((b, a), [])
        print("PROBE %s   polygons traversing %d->%d: %s ; %d->%d: %s" % (
            tag, a, b, forward, b, a, backward))
        for vertex_index in (a, b):
            co = mesh.vertices[vertex_index].co
            print("PROBE %s   vertex[%d] co=(%.9g, %.9g, %.9g)" % (
                tag, vertex_index, co.x, co.y, co.z))


def install_hook() -> None:
    original = validate.validate_mesh

    def hooked(mesh, **kwargs):
        tag = "LOD%s/%s" % (kwargs.get("lod_index", "?"), mesh.name)
        try:
            analyse(mesh, tag)
        except Exception as error:  # a probe must never mask the real run
            print("PROBE FAILED on %s: %r" % (tag, error))
        return original(mesh, **kwargs)

    validate.validate_mesh = hooked

    # STAGE COVERAGE. topology_report already has five call sites bracketing the
    # budget decimation and every LOD, so hooking it walks the pipeline for free
    # and localises the stage that introduces the defect instead of inferring it.
    original_topology = mesh_ops.topology_report
    counter = {"n": 0}

    def hooked_topology(obj):
        report = original_topology(obj)
        counter["n"] += 1
        try:
            analyse(obj.data, "STAGE%d/%s/tris%d" % (
                counter["n"], obj.name, report.triangles))
        except Exception as error:
            print("PROBE FAILED on stage %d: %r" % (counter["n"], error))
        return report

    mesh_ops.topology_report = hooked_topology

    # BRACKET THE WELD. weld_and_clean is the one stage that ends in
    # recalc_face_normals, so measuring its input and its output separates "the
    # generator handed it a twisted surface" from "this function twisted it".
    import bpy
    original_weld = mesh_ops.weld_and_clean
    weld_counter = {"n": 0}

    def _snapshot(bm, label):
        scratch = bpy.data.meshes.new("_probe_weld_scratch")
        bm.to_mesh(scratch)
        try:
            analyse(scratch, label)
        finally:
            bpy.data.meshes.remove(scratch)

    def hooked_weld(bm, *args, **kwargs):
        weld_counter["n"] += 1
        index = weld_counter["n"]
        try:
            _snapshot(bm, "WELD%d-IN" % index)
        except Exception as error:
            print("PROBE FAILED on weld %d input: %r" % (index, error))
        try:
            _weld_steps(bm, index)
        except Exception as error:
            print("PROBE FAILED on weld %d steps: %r" % (index, error))
        try:
            _weld_steps_candidate(bm, index)
        except Exception as error:
            print("PROBE FAILED on weld %d candidate: %r" % (index, error))
        stats = original_weld(bm, *args, **kwargs)
        try:
            _snapshot(bm, "WELD%d-OUT" % index)
        except Exception as error:
            print("PROBE FAILED on weld %d output: %r" % (index, error))
        return stats

    mesh_ops.weld_and_clean = hooked_weld


def weld_and_clean_oriented(bm, merge_distance=1e-4, *,
                            fill_boundary_loops=True, blackbox=None) -> dict:
    """``mesh_ops.weld_and_clean`` with ONE change: the survivors are chosen by
    traversal direction, not by area alone.

    Exists so the proposed ``mesh_ops`` diff can be measured end to end on the
    shipped asset without editing a read-only module. Everything else -- op
    order, distances, per-loop hole filling, the trailing recalc, the returned
    stats keys the coral generator reads -- is a mirror of the live function as
    of 2026-07-29.

    Selected with ``H8_PROBE_MODE=fix``.
    """
    from h8forge import law as forge_law

    before_v = len(bm.verts)
    before_f = len(bm.faces)

    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=merge_distance)
    bmesh.ops.dissolve_degenerate(bm, dist=merge_distance, edges=bm.edges[:])
    dead = [f for f in bm.faces
            if f.calc_area() <= forge_law.DEGENERATE_TRIANGLE_AREA_EPS]
    if dead:
        bmesh.ops.delete(bm, geom=dead, context="FACES")
    loose_verts = [v for v in bm.verts if not v.link_faces]
    if loose_verts:
        bmesh.ops.delete(bm, geom=loose_verts, context="VERTS")

    nonmanifold_before = sum(1 for e in bm.edges if len(e.link_faces) > 2)
    interior_deleted = 0
    forced_by_area = 0
    if nonmanifold_before:
        doomed = set()
        for edge in bm.edges:
            linked = edge.link_faces
            if len(linked) <= 2:
                continue
            start = edge.verts[0]
            forward, backward = [], []
            for face in linked:
                first = None
                for loop in face.loops:
                    if loop.edge is edge:
                        first = loop.vert
                        break
                (forward if first is start else backward).append(face)
            forward.sort(key=lambda f: f.calc_area(), reverse=True)
            backward.sort(key=lambda f: f.calc_area(), reverse=True)
            if forward and backward:
                keep = {forward[0], backward[0]}
            else:
                forced_by_area += 1
                keep = set(sorted(linked, key=lambda f: f.calc_area(),
                                  reverse=True)[:2])
            for face in linked:
                if face not in keep:
                    doomed.add(face)
        if doomed:
            bmesh.ops.delete(bm, geom=list(doomed), context="FACES")
            interior_deleted = len(doomed)
            orphans = [v for v in bm.verts if not v.link_faces]
            if orphans:
                bmesh.ops.delete(bm, geom=orphans, context="VERTS")
    nonmanifold_after = sum(1 for e in bm.edges if len(e.link_faces) > 2)

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
                pass

    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    print("PROBE FIXWELD interior_deleted=%d forced_by_area=%d "
          "nonmanifold %d->%d holes_filled=%d" % (
              interior_deleted, forced_by_area, nonmanifold_before,
              nonmanifold_after, holes_filled))
    return {
        "verts_removed": before_v - len(bm.verts),
        "faces_removed": before_f - len(bm.faces),
        "degenerate_faces_deleted": len(dead),
        "loose_verts_deleted": len(loose_verts),
        "nonmanifold_edges_before": nonmanifold_before,
        "nonmanifold_edges_after": nonmanifold_after,
        "interior_faces_deleted": interior_deleted,
        "interior_edges_forced_by_area": forced_by_area,
        "boundary_loops_filled": holes_filled,
        "boundary_edges_after": sum(1 for e in bm.edges
                                   if len(e.link_faces) == 1),
    }


def main() -> None:
    if os.environ.get("H8_PROBE_MODE") == "fix":
        mesh_ops.weld_and_clean = weld_and_clean_oriented
        print("PROBE MODE=fix: weld_and_clean replaced by the orientation-aware "
              "candidate; validate.py is unmodified, so the VALIDATE lines below "
              "are the proposed mesh_ops diff measured end to end.")
    else:
        install_hook()
    import coral_branching
    coral_branching.validate = validate
    coral_branching.main()


if __name__ == "__main__":
    main()
