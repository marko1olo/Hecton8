# Status 1826 - COPPER_ROUTE_DATA_OWNER_PATCH_PACKET

State: PATCH_PACKET_COMPLETE  
Evidence class: STATIC_SOURCE / STATIC_DOC only  
Unity/import/build/DataMonolith: NOT RUN / FORBIDDEN BY TASK

## Tasks

- [x] Created owned tracking/report outputs.
- [x] Read AGENTS, project bibles, task-local packet, Batch18 source reports, and six relevant mandates.
- [x] Reconfirmed raw and legacy copper asset paths and `.meta` files.
- [x] Reconfirmed duplicate `stableId: Data_Copper` under `Assets/_Project/Data`.
- [x] Reconfirmed raw owner fields: raw resource, stack 32, material/electronics family, tier0, world prefab.
- [x] Reconfirmed legacy collision fields: non-raw, stack 64, no world prefab, legacy class identifier.
- [x] Searched active first-party references to raw and legacy GUIDs.
- [x] Searched active first-party route/craft references to `Data_Copper`, Copper Wire, and copper quest.
- [x] Produced reference checklist CSV.
- [x] Defined mutation options, recommendation, rollback path, validation gates, Unity gates, DataMonolith gates, and failure labels.
- [x] Final report avoids runtime/import/profiler claims and destructive asset instructions without proof.

## Result

Recommended future fix: preserve raw `Assets/_Project/Data/Items/Resources/Raw/Data_Copper.asset` as the sole `stableId: Data_Copper` owner; quarantine/rename the legacy root asset with preserved GUID before considering deletion.

Remaining blockers are out of scope for this packet:

- starter tool authority for copper `requiredToolClass: 2`;
- generic FirstCraft route gate;
- Unity content validation;
- DataMonolith rebuild/import validation;
- PlayMode pickup/quest/craft/save-load proof.
