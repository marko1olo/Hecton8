# Rationale 1870

Decisions:

- Kept CopperOre data route mapped to `Assets/_Project/Data/Items/Resources/Raw/Data_Copper.asset`; no `Data_CopperOre.asset` invented.
- Classified BioForge kelp and WorldProceduralGeology meshes as source candidates only because 1858/1866 already mark them missing manifests, named proof, and captures.
- Recommended quarantine for `Assets/_Project/Prefabs/Item_Titanium.prefab` because it duplicates `Data_TitaniumScrap.asset`, uses a primitive cube, and carries unresolved material guid `31321ba15b8f8eb4c954353edc038b1d`.
- Required unique silhouettes/material identities per resource. Recoloring one rock mesh for all resources is rejected.
- Kept proof class static only. No visual acceptance, Unity acceptance, runtime proof, profiler proof, or build health claimed.

Evidence limits:

- `rg`, `Select-String`, `Get-Content`, and `Get-ChildItem` prove text/path presence only.
- Material paths prove GUID resolution only, not texture roles or import correctness.
- Candidate mesh paths prove source availability only, not visual quality or source-package acceptance.
