"""HECTON-8 offline asset forge -- Blender-side generation core.

Sanctioned by ``3dmodel.md`` section 0, which permits offline authoring in "Unity
Editor tooling **or external offline DCC/bake tools**". This package is the external
DCC half. Unity remains the only route that writes ``.prefab``/``.unity`` assets, per
``AGENTS.md`` ``Evidence Law`` -- nothing here touches Unity asset files.

Module map:
  ``law``          every threshold from the 3D bibles, with citations. No bpy import,
                   so it is testable outside Blender.
  ``blackbox``     fixed 300-step bake ring + failure dump (3dmodel.md section 11).
  ``mesh_ops``     bevel policy, shading basis, LOD chain, convex collider proxy.
  ``vertexcolor``  channel semantics + real Cycles AO bake into channel B.
  ``validate``     pre-save quality gates; aborts the save on failure.
  ``preview``      headless contact-sheet renders for visual judgement without Unity.
  ``export_unity`` FBX export with correct Unity axis conversion.

Import style inside Blender, where this package is not on ``sys.path`` by default::

    import sys, os
    sys.path.insert(0, os.path.join(<repo>, "Tools", "Blender"))
    from h8forge import law, mesh_ops, vertexcolor

``law`` is imported eagerly because it is dependency-free. The bpy-dependent modules
are left to the caller so that ``law`` can be imported by plain CPython tooling
(validators, CI checks) without pulling Blender in.
"""

from __future__ import annotations

from . import law  # noqa: F401  -- dependency-free, safe outside Blender

__all__ = ["law"]
__version__ = law.FORGE_VERSION
