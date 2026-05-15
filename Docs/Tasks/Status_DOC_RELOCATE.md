# Status_DOC_RELOCATE

- [x] Identify foreign Timaert and dental documents in active Hecton8 task/log folders | Justification: static filename/content audit only; DOD practice is evidence-labeled hygiene scan | Alternatives Rejected: bulk-moving all non-Hecton files by age was rejected because active agents share the directory | Estimate: 2000 us
- [x] Timaert active-folder pass | Justification: exact filename marker scan for `TMA_`, `timaert`, `timaert_c`, `Samosbor`, `Masumo`, and `PRECOMMIT_CYCLE` returned no Timaert-owned files in active Hecton task/log folders; only Hecton audit notes mention Timaert | Alternatives Rejected: moving Hecton audit notes to Timaert, because they are Hecton provenance records | Estimate: 0 files moved
- [x] Move confirmed dental documents to `C:\hades\dental-crm` ownership | Justification: `Status_STOMCHAT_DSD_FIX.md`, `Rationale_STOMCHAT_DSD_FIX.md`, and `LOG_STOMCHAT_DSD_FIX.md` were direct stomchat/stomatology agent files and were moved under `C:\hades\dental-crm\docs` | Alternatives Rejected: archiving under Hecton rejected because the files belong to another project | Estimate: 3 files moved
- [x] Verify Hecton active folders no longer contain confirmed foreign files | Justification: post-move existence checks show dental files absent from Hecton and present in dental-crm; marker scans show no remaining Timaert/dental filenames in Hecton active task/log folders | Alternatives Rejected: runtime/compiler proof irrelevant for documentation moves | Estimate: 3000 us

Verification:
- STATIC_DOC: `C:\hades\Hecton8\Docs\Tasks\Status_STOMCHAT_DSD_FIX.md` absent.
- STATIC_DOC: `C:\hades\Hecton8\Docs\AgentLogs\Rationale_STOMCHAT_DSD_FIX.md` absent.
- STATIC_DOC: `C:\hades\Hecton8\Docs\AgentLogs\LOG_STOMCHAT_DSD_FIX.md` absent.
- STATIC_DOC: `C:\hades\dental-crm\docs\Tasks\Status_STOMCHAT_DSD_FIX.md` present.
- STATIC_DOC: `C:\hades\dental-crm\docs\AgentLogs\Rationale_STOMCHAT_DSD_FIX.md` present.
- STATIC_DOC: `C:\hades\dental-crm\docs\AgentLogs\LOG_STOMCHAT_DSD_FIX.md` present.
- STATIC_DOC: `rg --files` marker scan over Hecton active `Docs\Tasks` and `Docs\AgentLogs` returned no `stomchat`, `dental-crm`, `стомат`, `TMA_`, `timaert`, `timaert_c`, `Samosbor`, `Masumo`, or `PRECOMMIT_CYCLE` filenames.
- STATIC_DOC: content scan still finds Timaert only inside Hecton audit provenance files (`Status_COMPUTE_LOGISTICS_AUDITOR.md`, `LOG_COMPUTE_LOGISTICS_AUDITOR.md`, `Rationale_COMPUTE_LOGISTICS_AUDITOR.md`, `Status_DOC_AUDIT.md`). These were not moved because they are not Timaert task/log ownership files.
- STATIC_DOC: `Select-String` found no `<POLISH_MANDATE>` tag in current `Docs\Tasks\CURRENT_BATCH.md`.
