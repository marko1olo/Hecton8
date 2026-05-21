# Economy Integrity Audit

Agent: BACKEND_ENGINEER / ITEM_RECIPE_GRAPH_AUDITOR
Date: 2026-05-16

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-18 R13 Report Snapshot Boundary

This report file is a snapshot/provenance document. It is active only where it agrees with:

- `Docs/README.md`
- `Docs/Reports/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

Historical `PASS`, `VERIFIED`, `current`, `latest`, counter, compile, runtime, 0-GC, frame-time, cost, and performance statements inside this report are not current proof unless the exact claim links a fresh artifact path, command/tool, timestamp, evidence class, and unresolved-error list. No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied by this file alone.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->
Evidence class: STATIC_SOURCE plus CLI_PYTHON. Unity compile, Play Mode, profiler, and player build remain PENDING VERIFICATION.

## Commands

- `python Tools\EconomyItemsCsvBake.py --root .`
- `python -B Tools\EconomyValidator.py --root . --negative-tests`
- `python -B Tools\EconomyRecipeGraphAudit.py --root . --report Docs\Reports\Economy_Integrity_Audit.md`
- `python -B -m unittest Tools.test_economy_integrity`
- `python -B -c "ast.parse(...)"`
- `git diff --check -- ...owned files...`

## Regression Coverage
- `Tools.test_economy_integrity`: 13 tests covering deterministic `Items.csv` bake, manifest validation, hash corruption detection, recipe identity uniqueness, effective physical metadata, full graph-audit CLI binding drift failure, missing binding-plan failure, resource matrix coverage, DAG/progression band, and inventory capacity fail-closed source gates.
- `EconomyValidator.py --negative-tests`: 6 malformed economy cases must fail as expected.
- Bytecode hygiene: final verification uses `python -B`; economy pycache artifacts must remain absent.

## Sources
- Recipes: `Data\Economy\Recipes.json`
- Items.csv present: `True`
- Resource matrix: `Data\Economy\Resource_Distribution_Matrix.csv`
- NetworkX: `3.6.1`

## Items.csv Manifest
- Header matches contract: True
- Rows: 55
- Raw/Crafted/External: 15 / 40 / 0
- Hash checks: 150
- Duplicate item IDs: []
- Missing item value rows: []
- Hash mismatches: []
- Source recipe mismatches: []
- Baseline mismatches: []
- Classification mismatches: []
- Resource mismatches: []

## Item Physical Metadata
- ItemData assets scanned: 72
- Items.csv rows with ItemData assets: 33
- Missing raw ItemData assets: []
- Missing crafted ItemData assets: 22
- Missing crafted assets runtime-blocked: True
- Runtime binding blocked count: 22
- Unblocked missing crafted assets: []
- Invalid effective mass: []
- Invalid effective volume: []
- Serialized zero but auto-resolved: ['Data_AbyssalCrystal']

## DAG
- Nodes: 55
- Edges: 122
- Is DAG: True
- Cycle count: 0

## Recipe Identity
- Recipe count: 40
- Missing recipe IDs: []
- Missing result item IDs: []
- Duplicate recipe IDs: []
- Duplicate result item IDs: []

## Progression
- Goal: economy.goal.first_submarine_handoff
- Unique recipe steps: 17
- Total recipe batches: 46
- Max dependency depth: 3
- Threshold verdict: 46 total recipe batches is inside the prompt's 5-50 band.

## Exploit Scan
- Zero ingredient recipes: []
- Nonpositive quantities: []
- Nonpositive costs/time/energy: []
- Unknown ingredients: []
- Value mismatches: []

## Scarcity
- Recipe biome constraints present: False
- Missing resource rows: []
- Zero-spawn resources: []
- Matrix coverage: 10 biomes x 15 resources. Recipe-specific biome gates are not authored, so this proves global availability, not per-biome unlock placement.

## Hash Contract
- Hash pairs checked: 329
- Missing hash fields: []
- Mismatched hashes: []
- Full validator checked 1041 generated hash pairs after `Items.csv` was added.

## Bulk Transfer
- Bulk job checks weight: True
- Bulk job checks volume: True
- Normal TryAdd checks weight: True
- Normal TryAdd checks volume: True
- Normal TryAdd rejects nonpositive unit mass: True
- Normal TryAdd rejects nonpositive unit volume: True
- Capacity resolver rejects nonpositive unit value: True
- Fix applied: normal inventory add now resolves capacity-limited quantity using current mass/volume plus runtime item mass/volume before mutating inventory.

## Save DTO Hash Width
- Same bit length: True
- Hashes above Int32.MaxValue: 27
- Signed representation note: True. Runtime `ItemData.PersistentHashId` uses `int`, so DTO bit width matches; generated CSV/JSON display hashes as unsigned decimal.

## Regression Model

- CPU: normal add path gains O(N) current mass/volume scan on command-path insertion only. No Tick/FixedTick code changed.
- GC: no managed allocations added to gameplay paths; helpers use existing NativeArray buffers and scalar math.
- Memory: `Items.csv`, audit tools, and regression tests are offline assets/tools only.
- Correctness: recipe graph acyclic; `Items.csv` manifest validated; effective raw item physical metadata validated; capacity bypass closed statically.
- Verification gap: local `dotnet` and Unity executables were unavailable in this shell, so Unity compile remains pending.

STATUS: HISTORICAL STATIC ECONOMY AUDIT SNAPSHOT / UNITY RUNTIME PENDING VERIFICATION
