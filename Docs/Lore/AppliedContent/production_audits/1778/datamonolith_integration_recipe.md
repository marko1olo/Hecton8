# DataMonolith Integration Recipe - AppliedLore 1778

Evidence class: STATIC_SOURCE / CLI_AUDIT / H8BIN_OFFLINE_PARSE

## Current Route

1. VERIFIED: Authoring packets live under `Docs/Lore/AppliedContent/packets/*.packets.json` and are referenced by `Docs/Lore/AppliedContent/release_sets/*_manifest.json`.
2. VERIFIED: `Tools/AppliedLoreImporter.py --root .` collects packet JSON, enforces the 15-locale roster, strips draft-review prose markers from player-visible text, writes `applied_lore_packets.csv`, and writes `H8AppliedLoreHashes.cs`.
3. VERIFIED: `Tools/AppliedLoreRouteCardExporter.py --root .` reads `Docs/Lore/AppliedContent/route_cards/*_route_cards.csv`, validates packet IDs against `applied_lore_packets.csv`, and writes `Assets/_SourceData/DataMonolith/Narrative/applied_lore_route_cards.csv`.
4. VERIFIED: `Assets/_Project/Scripts/Editor/DataMonolith/H8DataMonolithCompiler.cs` runs the AppliedLore importer and route-card exporter before bake, then parses `applied_lore_packets.csv` and `applied_lore_route_cards.csv` into `H8AppliedLorePacketRecord` and `H8AppliedLoreRouteRecord` sections.
5. VERIFIED: AppliedLore runtime sections are `H8DataSectionId.AppliedLorePackets = 27` and `AppliedLoreRoutes = 28`; both use fixed 128-byte records.
6. VERIFIED PRE-GENERATION: Offline full audit initially passed against `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` with `applied_records=6900` and `applied_routes=454`.
7. VERIFIED SOURCE: After importer, route-card exporter, and page exporter, source-only audit passed with `publication_frontmatter_pages=13800`, `publication_surface_rows=13800`, and `publication_cluster_rows=150`.
8. BLOCKED: Current `static_data.h8bin` is stale after source generation. Post-page-export full audit failed at `P288_WORKER_LOCKER_NAMEPLATE_SAMPLE/ja_JP` scanner length (`csv=88`, `blob=71`).
9. BLOCKED: Runtime readiness still needs Unity import, Play Mode load, player-build package inclusion, profiler/GC proof, and actual UI/scanner/terminal interaction proof.

## Current Counts

- Packet IDs: 460.
- Localized CSV rows: 6900.
- Locales per packet: 15.
- Draft localization rows: 5180.
- Route-card source rows: 454.
- Baked route export rows: 454.
- H8BIN AppliedLore packet records: 6900 in stale binary.
- H8BIN AppliedLore route records: 454 in stale binary.

## Runtime Boundary

- Runtime reads `H8AppliedLorePacketRecord`, `H8AppliedLoreRouteRecord`, UTF-8 byte slices, hashes, masks, flags, and SignalBus payloads.
- Runtime must not parse Markdown, packet JSON, publication indexes, authoring CSV, or localization dictionaries for AppliedLore.
- Publication Markdown is website/wiki output only; frontmatter explicitly states `runtime_reads_markdown: false`.

## Scalability Consequences

- Low: static binary lookup and low terminal-preview signal budget (`AppliedLoreTerminalPreviewSignal.LowTierFrameSignals = 8`) preserve zero-GC UI route.
- Middle: same binary route, more frame headroom for PDA metadata seeding and route prerequisite checks outside hot parser paths.
- High: richer terminal/PDA presentation can consume the same records without changing gameplay truth ownership.
- Ultra: visual-overkill terminal/codex presentation must remain a read-only observer of baked records and signals; no runtime Markdown/JSON parser is allowed.
