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

