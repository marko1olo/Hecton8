# Rationale_X_014

Status: COMPLETE / PENDING VERIFICATION

## R0 - Scope Lock

Problem: X_014 is a read-only netcode/save scout, but project workflow demands status/rationale/report artifacts.

Solution: Write only documentation/report artifacts under `Docs/Tasks`, `Docs/AgentLogs`, and `Docs/Reports`. Do not modify `.cs`, `.asmdef`, `.prefab`, `.unity`, `.asset`, package, or project settings files.

Rejected Alternatives: Editing C# to add instrumentation is outside prompt. Running broad refactors or source fixes would violate READ-ONLY ARCHAEOLOGY.

Scalability potential: Low/Middle/High/Ultra audit output must separate gameplay truth from presentation lanes so future implementations can scale visuals through `GlobalQualityWeight` without changing hash authority.

Hardware Impact: Static audit has no runtime cost. Expected runtime savings are report-only until implementation: eliminating cosmetic hash inputs prevents false rollback/reconcile work on i3/MX350.

## R1 - Evidence Policy

Problem: Existing docs can be stale, and the current Docs index already labels netcode as placeholder-grade pending proof.

Solution: Treat current source under `Assets/_Project` as primary evidence. Use docs only as contract context. Every report claim must include file path and line numbers.

Rejected Alternatives: Trusting `Docs/ARCHITECTURE/COOP_MERKLE_STATE_DELTA_PROTOCOL.md` as implementation proof. Searching archives as active source.

Scalability potential: Low tier needs compact hash/state packets; Ultra tier may carry richer telemetry, but not in gameplay truth hash unless source proves it.

Hardware Impact: Prevents future agents from adding broad full-state hashing or managed serialization in hot paths, a likely >0.1 ms risk on MX350 if implemented badly.

## R2 - Merkle Boundary Separation

Problem: The prompt asks for Merkle proof across network rollback and save/persistence, but source contains two different Merkle systems.

Solution: Treat `RollbackNetcodeContracts.cs` as the runtime network Merkle authority and `SaveStateMerkleTree.cs` / `SaveMasterHashV10.cs` / `SaveBinaryStorage.cs` as the persistence hash authority. Report them separately.

Rejected Alternatives: Merging save Merkle facts into rollback root proof would create a false design. `HectonNetworkManager.cs` does not call save Merkle code.

Scalability potential: Low tier can use compact network leaves without touching persistent save identity. Middle/High/Ultra can add richer save telemetry or WAL behavior, but not by changing network hash authority.

Hardware Impact: Separation prevents future agents from hashing 4096 save leaves in a per-frame network lane. On i3/MX350 that would spend frame budget on persistence proof instead of visible state.

## R3 - Quality-Dependent Hash Risk

Problem: Network Merkle root currently depends on `QualityLeafBudget`, and `QualityLeafBudget` depends on `GlobalQualityWeight`.

Solution: Mark this as a correctness risk in the report. `GlobalQualityWeight` may control cadence/capacity, but it must not change authority route or the set of gameplay truth leaves used for a cross-peer frame hash.

Rejected Alternatives: Calling optional quality leaves harmless. They include inventory, durability, quest, and predator chosen-state buffers, which are gameplay state names, not presentation buffers.

Scalability potential: Low/Middle/High/Ultra should keep identical authoritative root inputs. Quality can scale branch probe frequency, packet redundancy, telemetry, and presentation smoothing after hash proof.

Hardware Impact: Avoids false desync/rollback work across heterogeneous hardware. Measured saved time: 0 us in this read-only pass; expected savings require implementation/profiler proof.

## R4 - AUP Representation Finding

Problem: Entity AUP snapshot storage uses `RollbackAup48`, while network leaf descriptor/hash byte length uses canonical `double3` stride.

Solution: Report the split as deliberate-looking canonical absolute-position hashing with a future offset hazard. Current descriptors set `ByteOffset = 0`, so no active nonzero-offset defect was proven.

Rejected Alternatives: Declaring it broken outright. The hash path converts each `RollbackAup48` to absolute `double3` before mixing, matching AUP determinism intent.

Scalability potential: Low tier can hash compact canonical AUPs. Ultra tier can carry additional presentation/local-grid debug state out of the authoritative hash.

Hardware Impact: Canonical `double3` hashing avoids including padding/grid-local representation noise. Measured saved time: 0 us in this read-only pass.

## R5 - Cosmetic State Proof

Problem: The prompt requires proof that cosmetic variables do not contaminate frame hashes.

Solution: For network rollback, prove visual state/history/telemetry/audio/tuning are not registered as Merkle leaf descriptors. For save Merkle, report the opposite nuance: `LeafFlagCosmetic` is a delta-prune flag, not a root-hash exclusion.

Rejected Alternatives: Saying "cosmetic excluded" globally. Save Merkle leaf hashing does not skip `LeafFlagCosmetic`.

Scalability potential: Low tier may prune cosmetic delta payload bytes; High/Ultra may retain more cosmetic deltas. Persistent root identity still changes if cosmetic leaves are descriptors.

Hardware Impact: Prevents future false claims and prevents mismatched save roots from being misdiagnosed as runtime rollback defects.

## R6 - Rollback Execution Reality

Problem: The code names a headless resimulation command, but audited source does not run a dispatcher replay loop inside rollback.

Solution: Report exact observed flow: mismatch detection, snapshot restore, remote input correction, `MockTickCommand` emission, audio suppression, resim cost estimate, and visual correction.

Rejected Alternatives: Inferring a full rollback replay from DTO names. No audited line executes a multi-frame simulation replay loop.

Scalability potential: Low tier can still use cheap snapshot restore and visual correction. Middle/High/Ultra need an explicit replay owner if future agents implement true rollback.

Hardware Impact: No runtime change. The report prevents future agents from assuming desync repair is already operational.
