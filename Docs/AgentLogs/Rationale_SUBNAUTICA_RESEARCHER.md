# Rationale_SUBNAUTICA_RESEARCHER

Status: COMPLETE - RESEARCH ONLY, NO RUNTIME CODE CHANGED

Problem: User asked what can be tactically borrowed from Subnautica files/source/mods.
Solution: Treat installed Subnautica as proprietary reference only. Inspect local folder/file taxonomy and public documentation/open-source mods. Extract patterns, not assets/code.
Rejected Alternatives: Copying game files, decompiling assemblies, or reusing meshes/textures/audio is legal and production risk. Standard "clone the reference" approach is too brittle and creates licensing debt.
Scalability potential: Low = cheap approximations and authored data tables; Middle = deterministic managers; High = richer shader/VFX/audio presentation; Ultra = visual overkill behind tier gates.
Hardware Impact: Research-only pass has 0us runtime impact. Any future implementation must target MX350/i3 budget and spend saved CPU on visible underwater mood, not hidden simulation.

Problem: No active batch prompt or agent XML ID was provided.
Solution: Use local ID SUBNAUTICA_RESEARCHER and keep status/rationale/log files separate from existing agents.
Rejected Alternatives: Reading archived batch prompts as current authority would mix stale directives into this task.
Scalability potential: Keeps cross-agent hygiene intact.
Hardware Impact: 0us runtime impact.

Problem: User clarified the current focus is codebase foundation, not content harvesting.
Solution: Reframe Subnautica as evidence for foundation contracts: Addressables catalog scale, two-level terrain cache, compiled octree/visibility proxy, save-slot paging, terrain patch/mod handler topology, and runtime validation gates. Hecton8 comparison is against code/data wiring, not art direction.
Rejected Alternatives: Chasing models, textures, language strings, FMOD banks, or decompiled Assembly-CSharp would create legal debt and not strengthen the Hecton8 foundation. Copying Nautilus/TerrainPatcher/Nitrox code is blocked by GPL/AGPL for a non-GPL codebase unless licensing strategy changes.
Scalability potential: Low = generated chunk manifests, 64m/192m cells, compressed deltas, cheap proxy layers; Middle = deterministic Addressables residency and chunk-paged save; High = compiled PVS/octree/material proxies; Ultra = richer visual overkill loaded from tiered content groups without changing gameplay contracts.
Hardware Impact: Research-only: exact measured 0us. Future foundation work targets reduced hitch risk on i3/MX350 by moving sync asset/world work into prebuilt bundles and paged fixed-size payloads; exact gains require Unity profiler proof.

Problem: Hecton8 contains major systems but has weak proof of production payload packaging.
Solution: Mark Addressables as P0 foundation gap: package exists and code references Addressables, but Assets/AddressableAssetsData contains zero files. Subnautica local install proves large underwater worlds need real catalog/bundle topology, not only runtime classes.
Rejected Alternatives: Treating an empty folder as Addressables readiness, or accepting editor-only validators as runtime proof.
Scalability potential: Low = core labels only; Middle = world/item/audio groups; High = biome/route/cell groups; Ultra = tiered texture/proxy/detail groups by hardware class.
Hardware Impact: Exact measured 0us now. Expected effect is hitch prevention, not free frame time; must be profiled after groups are generated.

Problem: Hecton8 world/save systems are architecturally rich but not yet aligned to a stable external world-cache contract.
Solution: Use Subnautica's public/local topology as pattern: base terrain cells, batch objects, compiled octree/PVS proxy data, global scene state, per-cell save deltas. Map this to H8WorldPagePayloadTypes and add explicit base-world payload families before adding more gameplay systems.
Rejected Alternatives: Runtime-only procedural generation with no baked cache, monolithic save blobs, or hard dependencies between agents' systems.
Scalability potential: Low = procedural fallback + small deltas; Middle = baked macro payloads; High = precomputed visibility/material/physics proxies; Ultra = dense visual payloads streamed by tier.
Hardware Impact: Exact measured 0us now. Foundation target is predictable IO and lower main-thread activation spikes; exact gains pending player build profiling.

Problem: User asked whether Hecton8 has something instead of Addressables.
Solution: Classify the existing layers precisely. Addressables package 2.7.6 is installed and UNITY_ADDRESSABLES_EXIST is active through asmdef versionDefines, but Assets/AddressableAssetsData contains 0 files and Assets/StreamingAssets is absent. Runtime Resources loading is explicitly disabled by AsyncLoadHelper. The current project has no full Addressables replacement. It has partial layers: scene-owned references/ObjectPoolManager, ScriptableObject data lake, ItemCatalog direct prefab fallback plus optional Addressables references, PrefabRegistry for stable IDs of already available prefabs, AssetLoadDispatcher for throttling, AssetLifecycleGovernor for residency/release policy, WorldChunkResidencyManager for intended chunk streaming, PersistentWorldRegistry for save/world-item hydration, H8StaticDataArena for a not-yet-built binary data monolith, and a separate mod AssetBundle lane.
Rejected Alternatives: Calling AssetLoadDispatcher a loader, calling PrefabRegistry a content pipeline, calling ModAssetManager the core game packaging layer, or claiming Addressables readiness because the Unity package and directory exist.
Scalability potential: Low = direct scene references plus small pools and direct prefab fallback; Middle = real Addressables/custom bundle manifest with core labels; High = chunk/biome/item/audio groups with deterministic residency; Ultra = tiered proxy/detail/texture groups that spend saved IO and CPU on visible underwater overkill.
Hardware Impact: Research-only 0us. Future benefit is fewer activation stalls and fewer accidental sync loads on i3/MX350 after the packaging contract is made real; exact microseconds require Unity player profiling.

Problem: Hecton8 modding looks broad on API surface but has contradictions in runtime loading.
Solution: Treat content-only mod bundles, raw PNG/localization fallbacks, namespaced save payloads, item/recipe/buildable overlays, resource hashes, and command queues as the real current foundation. Mark external managed DLL mods as not proven: ModBuilderWindow copies DLLs and writes EntryAssembly, but ModLoader requires explicit factory registration and says runtime assembly reflection loading is disabled for IL2CPP compliance. ModBuilderWindow also omits RequiredAPIVersion while ModLoader disables manifests where RequiredAPIVersion <= 0. RegisterKernel has no callers, so several command opcodes reject as MissingKernel unless future systems register executors.
Rejected Alternatives: Treating this as a BepInEx/Nautilus-equivalent loader, exposing Unity GameObject/Transform/prefab references directly to mods, or enabling reflection loading as a cheap fix against IL2CPP/security constraints.
Scalability potential: Low = content-only bundles and data overlays; Middle = validated manifest v2 packages and explicit command kernels; High = curated first-party MOD_COMPATIBLE ledger entries and chunk-safe mod resources; Ultra = tiered mod visual resources behind the same residency gates as first-party content.
Hardware Impact: Research-only 0us. Correcting the manifest/kernel story should reduce support failures and runtime rejection churn, not directly save frame time. Bundle residency and command quotas already point in the right direction.

Problem: Hecton8 has many validators, but several content-foundation checks are diagnostic rather than build-blocking.
Solution: Classify validators by enforcement. Existing IPreprocessBuildWithReport gates cover several platform/compliance risks, but Addressables/world streaming readiness is not yet a hard gate: PlatformCompatibilityAudit marks Addressables project data PASS when the empty directory exists; HectonProjectAuditor warns through a menu path; WorldStreamingWiringValidator and MapMagicWorldValidator are menu validators; HeadlessSimulationValidator requires Play Mode or separate batch runner; no source call to AddressableAssetSettings.BuildPlayerContent or AddressablesPlayerBuildResult was found.
Rejected Alternatives: Counting menu items as CI/build acceptance, treating Addressables package presence as content build proof, or adding more runtime systems before payload packaging has a blocking verifier.
Scalability potential: Low = fail build when Addressables settings/catalog/groups are missing; Middle = require labels for core/world/item/audio; High = require per-zone bundle size and residency budgets; Ultra = enforce high-tier overkill groups separately from low-tier mandatory groups.
Hardware Impact: Research-only 0us. Future build gates save debugging time and prevent hitch-prone content from shipping; runtime microseconds are indirect and require player-build profiling.

Problem: Hecton8 save/pager layer is strong, but Subnautica's file topology shows a separate base-world cache contract that Hecton8 has not explicitly named.
Solution: Keep H8BinaryWorldPager and SaveBinaryStorage as the save/delta backbone, but add or validate explicit payload families for base terrain cells, object batches, visibility/proxy data, physics proxy data, audio biome banks, and authored route/cache metadata. Existing H8WorldPagePayloadTypes only name VoxelDeltaRle, InventoryState, ChunkDehydratedMetadata, and WfcOutpostState.
Rejected Alternatives: Treating persistent deltas, WFC outpost state, or ItemCatalog hydration as enough for a Subnautica-scale world cache. Also rejected copying Subnautica optoctrees or TerrainPatcher code due to proprietary/GPL/AGPL licensing constraints.
Scalability potential: Low = procedural fallback and sparse deltas; Middle = baked terrain/object cells; High = PVS/octree/material/audio proxies; Ultra = tiered visual payloads and richer near-field detail streams.
Hardware Impact: Research-only 0us. Future target is reduced main-thread activation spikes and predictable IO on i3/MX350; exact gains need Unity Profiler and asset-build telemetry.

Problem: Audio runtime architecture exists, but source import policy still contains large preload/decompress risks.
Solution: Use Subnautica's category-bank split as a taxonomy pattern only: music, player, creatures, environment, vehicles, interface, tools, loot. Hecton8 currently has 45 WAV, 89 OGG, 3 MP3 under Assets/_Project/Audio; 52 audio metas with loadType 0, 45 with preloadAudioData 1, and large ambience WAVs such as Atmos 1.wav import with loadType 0, quality 1, preloadAudioData 1, loadInBackground 0. This needs an AudioImportPolicy build gate and residency categories, not another DSP layer.
Rejected Alternatives: Copying FMOD banks, assuming DSP code fixes bad importer settings, or forcing all stereo ambience to mono without authoring review. 3D SFX mono remains required; music/ambience must be categorized explicitly.
Scalability potential: Low = Vorbis Q70 ambience/music, ADPCM short SFX, no preload for long clips; Middle = per-biome audio groups; High = streamed/queued biome beds; Ultra = richer high-tier layers behind residency gates.
Hardware Impact: Research-only 0us. Future expected impact is memory pressure and hitch reduction on MX350, not guaranteed frame-time gain until measured.

Problem: PDA/scanner/lore systems exist, but authored progression density is not proven.
Solution: Treat Subnautica/Nautilus handler taxonomy as useful pattern: prefab identity, crafting/known-tech unlocks, loot distribution, PDA entries, story goals, save-data handlers. Hecton8 has PDADataLogTab, AudioLogSystem, LoreMmfEncyclopedia, scanner targets/fragments, quests/depth zones/registries, but current data count is modest and route coverage is not build-blocked. Add a first-hour progression coverage validator after content contracts are fixed.
Rejected Alternatives: Copying Subnautica text/data or claiming first-hour readiness because scanner/PDA code compiles.
Scalability potential: Low = minimum route graph with required scan/log/quest unlock counts; Middle = biome-specific PDA density; High = MMF encyclopedia payloads by route/zone; Ultra = richer hologram/VO/audio layers loaded by tier.
Hardware Impact: Research-only 0us. Future benefit is production reliability and player guidance, not a direct microsecond gain.

Problem: The third-pass report contained volatile external release/archive-date claims that could become wrong or be unsupported by the checked source.
Solution: Re-check GitHub API on 2026-05-16 for TerrainPatcher, Nitrox, QModManager, Nautilus, and BepInEx.Subnautica. Keep the confirmed TerrainPatcher and Nitrox latest release data, and downgrade QModManager from an exact archive-date claim to the API-supported repository state: archived true, last push 2023-05-09.
Rejected Alternatives: Trusting search snippets, preserving an exact QModManager archive date not exposed by GitHub API, or deleting useful clean-room source comparison because one timestamp needed correction.
Scalability potential: Documentation/evidence hygiene only. Low/Middle/High/Ultra runtime behavior is unchanged; research stays useful because legal/source freshness boundaries are clearer.
Hardware Impact: Research-only 0us. No runtime path changed.
