# Rationale 1875

Decision: implement an editor-only authoring tool, not a runtime generator.
Reason: `3dmodel.md` requires offline mesh generation; the task forbids Unity execution and prefab/asset edits now.

Decision: generate only eight canonical pickup mesh source specs.
Reason: task requires CopperOre, FiberKelp, HydrocarbonResin, MembraneTissue, SilicaShards, SilverOre, SulfurClumps, TitaniumScrap. `Item_Titanium` remains quarantine/canonical-route only.

Decision: map CopperOre metadata to `Data_Copper`.
Reason: prior 1870 packet states `Data_Copper.asset` is canonical. Creating `Data_CopperOre.asset` would split item truth.

Decision: use deterministic manual vertex/index helpers.
Reason: task forbids primitive generation and requires non-generic silhouettes. Continuous `GlobalQualityWeight` scales counts/segments/fronds/shards without changing item/data/collider truth.
