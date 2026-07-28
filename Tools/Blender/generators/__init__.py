"""Per-family asset generators built on the ``h8forge`` core.

Each module here owns exactly one asset family and is invoked headless::

    blender.exe -b --factory-startup -P Tools/Blender/generators/<family>.py -- \
        --seed 1712 --quality 1.0 --out <dir>

A generator must NOT reimplement a threshold, budget, bevel width, texel density, or
vertex-colour contract. Those live once in ``h8forge.law`` with their bible citation.
A local copy is drift waiting to happen and makes the next agent guess which number is
authoritative.

Required stage order comes from ``PROCEDURAL_ASSET_PIPELINE.md`` "Generation Order" and
is not optional: shape grammar, high-detail geometry, family topology rules, UVs and
material IDs, bakes and vertex colours, shared materials, LOD chain, collision proxies,
prefab/package assembly, VALIDATION, then save. "Small asset" is explicitly not an
exemption from UVs, normals, tangents, material IDs, LOD policy, or validation.
"""

from __future__ import annotations

__all__ = []
