# ProceduralOreSpawner DataVault Lock Flattening

Status: PENDING VERIFICATION
Domain: Echelon 2 / Geological Node Spawner
Prompt task count: 1

- [x] Extracted scoped prompt from chat block | DOD: strict domain boundary; work limited to ProceduralOreSpawner.cs | Rejected: neighboring agent task inference | Estimate: 200 us
- [x] Read AGENTS.md, domain map, and relevant mandates | DOD: DataVault/job/zero-GC/resource RNG mandates identified before coding | Rejected: patching from memory | Estimate: 900 us
- [x] Inspected existing dirty diff | DOD: preserve another agent's pending hot/cold native-state edits | Rejected: reverting file or assuming clean baseline | Estimate: 350 us
- [ ] Patch spawn job to stage output without holding multiple DataVault write locks | DOD: no ScheduleSpawnJob multi-write-lock chain | Rejected: leaving job writes directly against Vault buffers | Estimate: 2500 us
- [ ] Validate by source scans only | DOD: no dotnet build per prompt; grep lock path and local compile hazards | Rejected: unauthorized build | Estimate: 600 us
- [ ] Append final LOG entry | DOD: file report, not chat-only | Rejected: verbose bureaucracy | Estimate: 300 us
