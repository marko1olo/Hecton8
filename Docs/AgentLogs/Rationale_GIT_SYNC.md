# Rationale: GIT_SYNC

Date: 2026-05-15
Evidence class: GIT_CLI / STATIC_DOC

## Decision 1

Problem: Local `main` has a large dirty tree while `origin/main` is 77 commits ahead.
Solution: Commit a local snapshot before pull/rebase so every existing local change has a recoverable Git object.
Rejected Alternatives: Pulling with unstaged changes risks checkout conflicts and partial conflict state. Stashing a multi-agent Unity tree is less transparent than a named commit and can obscure asset/meta pairing.
Scalability potential: Low/Middle/High/Ultra runtime tiers are not affected by this repository-only operation.
Hardware Impact: 0 us runtime gain on i3/MX350; no gameplay frame path modified by GIT_SYNC.

## Decision 2

Problem: Untracked files include generated data, docs, tools, Unity `.meta` pairs, and one 33.5 MB binary LUT.
Solution: Preserve all non-ignored files in the local snapshot because the user requested commit/pull/push and there is no reliable authorship signal to split concurrent agent work.
Rejected Alternatives: Selective omission could drop legitimate generated assets or break Unity GUID pairing. Deleting/cleaning is outside the requested operation.
Scalability potential: Runtime scalability claims are not made. The content may include tier data authored by other agents, but this sync operation does not validate it.
Hardware Impact: 0 us runtime gain; repository transfer size increases by approximately 41.5 MB for untracked payload.

## Decision 3

Problem: Any final status claim could be overstated without Unity Editor or player logs.
Solution: Report Git/CLI verification only. Mark Unity/runtime health as pending if no Unity log is generated in this operation.
Rejected Alternatives: Claiming compile/runtime correctness from Git state violates the evidence mandate.
Scalability potential: Not applicable to repository operation.
Hardware Impact: 0 us runtime gain.

## Decision 4

Problem: Rebase conflict in `Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs` between upstream relocation-record cleanup and local Macro DB payload LRU/dirty eviction support.
Solution: Keep upstream removal of `RelocationRecordCapacity`/`_lastRelocationRecords`, keep local `MacroDatabasePayloadDirtyFlag` because merged code references it in `TryEvictLeastRecentlyUsedMacroDatabasePayload`.
Rejected Alternatives: Blind `--ours` would drop Macro DB LRU support and leave local cache behavior weaker. Blind `--theirs` would reintroduce a relocation record array that upstream had removed and that the merged file no longer uses.
Scalability potential: Preserves cheap LRU payload eviction for low-end devices while avoiding restored unused NativeArray allocation. High/Ultra runtime claims remain pending Unity/profiler evidence.
Hardware Impact: Static estimate: avoids reintroducing 64 relocation records of persistent NativeArray storage; exact runtime gain is 0 us measured / pending profiler.

## Decision 5

Problem: After the push, `python -m unittest discover -s Tools -p "test*.py"` exposed two stale generated artifacts: `Tools/AiBattleSim_Report.json` had `knownBufferCount=34` while current `H8Memory.cs` parses 66 buffers, and `Data/Lore/Encyclopedia.h8bin` no longer matched `Docs/Lore/Lore_Bible.md`.
Solution: Regenerate the artifacts using their owning tools: `python Tools/VerifyLore.py --bake --check` and `python Tools/AiBattleSim.py --encounters 10000`, then verify with artifact checkers and full unittest discovery.
Rejected Alternatives: Editing tests, hand-editing binary blobs, or leaving the failure documented only. Tests were reporting real stale artifact drift, not flaky behavior.
Scalability potential: Generated data remains cheap on low hardware; high-tier visual/AI behavior is not changed by this repository hygiene repair.
Hardware Impact: 0 us gameplay runtime gain measured; CLI report generation time was 41.955 seconds for 10,000 encounters on this machine, but Unity/player runtime remains pending.

## Decision 6

Problem: Continued repository scans found Unity/dotnet compile proof unavailable: Unity executable is absent, Visual Studio-local `dotnet.exe` has no SDK, and MSBuild can parse `Hecton8.slnx` but fails because referenced `.csproj` files are absent while only `.csproj.lscache` files exist.
Solution: Record the exact tooling failure as environment evidence and continue with static/CLI repository guards that can run locally.
Rejected Alternatives: Generating fake `.csproj` files or claiming compile success from `.lscache` metadata. Both would create false evidence and risk Unity project drift.
Scalability potential: None for runtime tiers; this is verification infrastructure debt.
Hardware Impact: 0 us runtime gain; compile/runtime proof remains blocked until Unity regenerates project files or a proper .NET SDK/project surface exists.

## Decision 7

Problem: Refined Unity meta scan found two first-party orphan folder metas: `Assets/_Project/Art/TEXTURES/VFX.meta` and `Assets/_Project/Scripts/Core/Memory/Layout.meta`; their base folders do not exist.
Solution: Delete only those two orphan `.meta` files. Leave third-party package/import debris untouched because it is outside this task and AGENTS forbids casual third-party asset surgery.
Rejected Alternatives: Broad cleanup of third-party metas, native bundle internals, Addressables metas, or RealtimeCSG internals. That would be high-risk and outside repository hygiene for this agent.
Scalability potential: No runtime tier impact; reduces Unity asset database noise and avoids unreachable GUID residue.
Hardware Impact: 0 us runtime gain measured.

