# Status_SAVE_METADATA_ARCHIVIST

Agent: SAVE_METADATA_ARCHIVIST  
Role: CORE_ENGINEER  
Domain: CORE & MEMORY INFRASTRUCTURE / SAVE METADATA  
Status: PENDING VERIFICATION  
Batch prompt extracted: 2026-05-13 via `Docs/Tasks/CURRENT_BATCH.md`  
Task count: 19  

## Mandates Loaded

- `DATA_Save_Persistence_Binary_Delta_Checksum.txt`
- `STRM_Async_Standard.txt`
- `STRM_Async_Asset_Upload_Texture_Settings.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `DBG_Telemetry_Crash_Reporting_PostMortem.txt`
- `UI_Data_Streaming_ZeroGC_Optimization.txt`
- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`

## State Machine

- [x] Prompt extraction | DOD: extracted XML tag from `CURRENT_BATCH.md` using CLI regex, counted 19 tasks | Rejected: IDE tab memory / neighbor prompts | Estimate: 400 us
- [x] Domain and docs boundary | DOD: read domain file plus stable docs for save, screenshot, signals, scalability, UI, quality gates | Rejected: dated report as primary authority | Estimate: 1200 us
- [ ] Task 1 SINGLETON ERADICATION: Purge `SaveScreenshotManager.Instance` | Justification pending | Alternatives rejected pending | Estimate pending
- [ ] Task 2 SIGNAL MIGRATION: Consume `SaveRequestSignal`, emit `SaveMetadataReadySignal` | Justification pending | Alternatives rejected pending | Estimate pending
- [ ] Task 3 ASMDEF ISOLATION: `Hecton8.Core.Persistence.Metadata` -> Contracts | Justification pending | Alternatives rejected pending | Estimate pending
- [ ] Task 4 DEAD CODE HUNT: eradicate `Texture2D.ReadPixels` and `EncodeToPNG` from main thread | Justification pending | Alternatives rejected pending | Estimate pending
- [ ] Task 5 RENDER TARGET: downscaled 256x144 `RenderTexture` | Justification pending | Alternatives rejected pending | Estimate pending
- [ ] Task 6 ASYNC GPU READBACK: issue `AsyncGPUReadback.Request(rt)` without frame block | Justification pending | Alternatives rejected pending | Estimate pending
- [ ] Task 7 NATIVE ARRAY EXTRACTION: extract pixels as native data | Justification pending | Alternatives rejected pending | Estimate pending
- [ ] Task 8 DXT/JPG COMPRESSION: no main-thread PNG encoding | Justification pending | Alternatives rejected pending | Estimate pending
- [ ] Task 9 BINARY INJECTION: write compressed bytes into `.tmp` save header | Justification pending | Alternatives rejected pending | Estimate pending
- [ ] Task 10 UI DECODE: `Texture2D.LoadRawTextureData()` + `Apply()` | Justification pending | Alternatives rejected pending | Estimate pending
- [ ] Task 11 ZERO-GC UI: pass texture to existing RawImage material, no prefab instantiation | Justification pending | Alternatives rejected pending | Estimate pending
- [ ] Task 12 CORRUPTION FALLBACK: default static-noise texture | Justification pending | Alternatives rejected pending | Estimate pending
- [ ] Task 13 AUP SHIFT SAFETY: N/A, post-process event | Justification pending | Alternatives rejected pending | Estimate pending
- [ ] Task 14 MATH LOD: Low tier skips screenshot and writes empty byte array | Justification pending | Alternatives rejected pending | Estimate pending
- [ ] Task 15 ZERO-GC: native readback / background native encoding path | Justification pending | Alternatives rejected pending | Estimate pending
- [ ] Task 16 BLACKBOX DUMP: push `ScreenshotSizeKb` to telemetry | Justification pending | Alternatives rejected pending | Estimate pending
- [ ] Task 17 EVENT BUS: `HUDNotificationSignal(Game Saved)` after persistence + screenshot complete | Justification pending | Alternatives rejected pending | Estimate pending
- [ ] Task 18 CROSS-DOMAIN AUDIT: load screenshots asynchronously as player scrolls | Justification pending | Alternatives rejected pending | Estimate pending
- [ ] Task 19 OMEGA COMPILE CHECK: no readback leak on scene unload | Justification pending | Alternatives rejected pending | Estimate pending

## Verification Log

- PENDING: source audit.
- PENDING: compile check.
- PENDING: Unity Console / Play Mode / profiler / GCMonitor proof.

