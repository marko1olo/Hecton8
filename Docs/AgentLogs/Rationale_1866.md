# Rationale 1866

Agent ID: 1866

Evidence boundary:

- This task is requirements/report work only.
- Static text/filesystem reads prove path presence, source references, and prior report claims only.
- No Unity/import/render/profiler proof was produced or claimed.

Decisions:

- Kept `1861` source gates intact. No recommendation to unblock menus without real source packages.
- Treated existing `PFB_Resource_*` pickup prefabs as blocked primitive outputs, not reusable source.
- Treated `Assets/_Project/Prefabs/WorldProceduralProxy/*` as invalid final source even where names match power/resource families.
- Treated generated kelp/geology and ScifiFacility models as candidate source inputs only. Prior reports still show missing manifests, named proof, and visual proof.
- Recorded missing root `resources.md` as static evidence instead of inventing resource bible authority.
- Required continuous `GlobalQualityWeight` scaling for fidelity, density, texture size, and LOD residency only; gameplay truth and data identity stay fixed.

Main risk:

- Future agents may try to use existing primitive prefabs or proxy families to satisfy menu unblock. This packet explicitly rejects that path.
