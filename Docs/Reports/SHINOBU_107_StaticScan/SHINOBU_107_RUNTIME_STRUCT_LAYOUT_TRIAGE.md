# SHINOBU_107 Runtime Struct Layout Triage

Source report: `Docs/Reports/SHINOBU_107_StaticScan/SHINOBU_140_Runtime_Struct_Layout.json`

## Summary

- Critical findings: `9`
- Warning findings: `0`
- Scanner rule: `STRUCT_BOOL_FIELD_ARM64_RISK`
- Patch decision: no C# source patch in this loop.

Reason: the remaining rows are serialized authoring schemas or a persistent save DTO. Blindly changing these `bool` fields to `byte` would alter Unity asset serialization or save payload identity. Runtime packet/black-box/SignalBus DTO bools already patched in earlier loops are no longer present in this report.

## Rows

| Row | Classification | Patch Requirement |
| --- | --- | --- |
| `EncounterProfile.cs:15` `EncounterThreatBand.allowDuringCriticalHealth` | ScriptableObject authoring schema | Requires asset migration/default verification. |
| `FaunaDataTemplate.cs:83` `FaunaInteractionMatrixEntry.forceRetreat` | Serialized fauna authoring matrix | Requires asset migration/default verification. |
| `FaunaStateMachine.cs:35` `FaunaStateMachine.useTerritory` | Serialized fauna authoring/runtime inspector cache | Requires prefab/template migration. |
| `FaunaStateMachine.cs:39` `FaunaStateMachine.isFlockingFish` | Serialized fauna authoring/runtime inspector cache | Requires prefab/template migration. |
| `SaveData.cs:489` `PlayerStatsDTO.hasLastDeathRecord` | Persistent save DTO identity | Requires save version bump, migration read path, and binary/JSON compatibility proof. |
| `SubmarineFluidDynamics.cs:223` `BulkheadDefinition.isSealed` | Serialized submarine compartment authoring schema | Requires prefab migration and inspector bridge. |
| `WorldChunkStreamingProfile.cs:12` `LayerProfile.useChunkResidency` | ScriptableObject streaming authoring schema | Requires profile asset migration. |
| `WorldChunkStreamingProfile.cs:13` `LayerProfile.useVisualProxyLayer` | ScriptableObject streaming authoring schema | Requires profile asset migration. |
| `WorldChunkStreamingProfile.cs:14` `LayerProfile.useFullSimulationNearPlayer` | ScriptableObject streaming authoring schema | Requires profile asset migration. |

## Rejected Changes

- Rejected changing Unity-serialized authoring structs to byte flags without migration tooling. That can silently reset assets or break inspector semantics.
- Rejected changing `PlayerStatsDTO.hasLastDeathRecord` without save-version migration. This is persistent player identity, not throwaway runtime scratch.
- Rejected adding `[StructLayout(Pack=1)]` anywhere. ARM64 alignment risk remains forbidden.
- Rejected adding wrapper properties around byte flags as a cosmetic patch. Hot DTOs must use raw fields; authoring schemas need explicit migration.

## Required Owner Follow-Up

- AI/Fauna owner: migrate authoring bools with an editor upgrader that rewrites existing assets and keeps inspector labels.
- Save owner: replace `PlayerStatsDTO.hasLastDeathRecord` with a byte flag only with versioned read/write compatibility and migration tests.
- Submarine/World owners: move authoring bools to byte-backed serialized fields only with prefab/profile migration or keep them out of runtime DTO copy paths.

## Proof Notes

These rows are not `NativeArray` packet payloads, Burst job fields, SignalBus payloads, or rollback state ring entries in the current static report. They remain red because the scanner correctly treats any struct bool as an ARM64 layout risk until the owning domain provides migration proof.
