# CARTOGRAPHY_UX_LEAD Rationale

Status: `PENDING VERIFICATION`

## Intake Decision

Problem: PDA/cartography map stores discovery in heavyweight UI/runtime forms and the prompt reports banned `FindObjectOfType<Terrain>()` plus `List<Vector3>` use.
Solution: Inspect existing ownership first, then route discovery through AUP-indexed native bitmasks and decoupled signals rather than UI-side object hunting.
Rejected Alternatives: Standard Unity scene search, managed lists, mesh-per-cell storage, and direct terrain references are rejected because they allocate, couple UI to world terrain, and violate batch isolation.
Scalability potential: Low = 2D height-only cells; Middle = coarse point cloud; High = SDF-gated solids; Ultra = POI overlays and richer shader response.
Hardware Impact: Expected gain for i3/MX350 comes from replacing managed vector storage and map meshes with packed `ulong` masks and GPU append output; exact microsecond proof is pending profiling.
