# 01 - Core Signal / Memory / Persistence Documentation Anchors

Status: READY_FOR_AGENT

Evidence class: STATIC_DOC target.

## Mission

Close exact stable-doc anchor gaps for these live classes:

- `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs`
- `Assets/_Project/Scripts/SaveSystem/H8BinaryWorldPager.cs`
- `Assets/_Project/Scripts/Core/Memory/VaultMemoryContracts.cs`

## Target Docs

- `Docs/SYSTEMS_CONTRACTS.md`
- `Docs/ARCHITECTURE/GLOBAL_SIGNAL_CORRIDOR.md`
- `Docs/ARCHITECTURE/SAVE_PAGING_PROTOCOL.md`
- `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_BOUNDARIES.md`
- `data.md`
- `persistence.md`

## Required Output

Add concise anchors that state:

- owner system;
- dispatcher/phase boundary where known;
- signal lane or DataVault handle class;
- hot-path forbidden claims;
- black-box/fault dump boundary;
- exact missing proof artifacts.

Do not add broad architecture prose. Do not mark anything runtime-ready.
