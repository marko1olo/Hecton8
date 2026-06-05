# Rationale 1887

Evidence boundary: static text/YAML only. No Unity/runtime claims.

1. `Item_Titanium.prefab` is not deletion-safe from static report because active editor/bootstrap/validator source references and a possible scene object expectation exist. It is a quarantine candidate only after Unity-owner reference proof.
2. `STRUCTURES.prefab` has no scoped GUID reference beyond its meta, but it contains an active primitive `Item_Titanium` child with package/default material GUID. It is a quarantine candidate only after aggregate reference proof.
3. `Buildings/Cube.prefab` has a live GUID reference in `Assets/MapMagic/Map_Graph/Old tries/Terrain.asset`. Static text cannot prove that graph is inactive. Deletion/quarantine is forbidden without MapMagic/construction owner proof.
4. GUID `31321ba15b8f8eb4c954353edc038b1d` resolves to package-cache URP `Lit.mat` under `.codexbuild`. Any retained product-face route using it needs project-owned material replacement.
5. Reports and archives are historical evidence only. They cannot prove current runtime references or absence of references.
