# COMPUTE THREAD VALUE AUDIT

Status: AUDIT COMPLETE
Snapshot: 2026-05-15T03:42:25+04:00
Source: `C:\Users\danat\.codex\state_5.sqlite` + top-100 rollout JSONL files
Method: read-only parse of `apply_patch`, shell, Unity-tool, and validation-adjacent calls

## Boundary

This file audits visible work traces from the 100 highest-token `.codex` threads. It does not prove final repository value, current build health, or runtime correctness.

Evidence strength:
- Strong: thread IDs, token counts, rollout paths, `apply_patch` targets, shell/tool call counts.
- Medium: build/test/Unity command buckets inferred from command text.
- Weak: title text and broad `test` keyword hits.
- Not proven: final LOC delta, H-Phi delta, current compile status, post-merge correctness.

Valid labels remain evidence labels. No thread is convicted as waste unless it is joined to changed files, meaningful LOC delta, final compile/test result, and quality delta.

## Top-100 Aggregate

| Metric | Value |
|---|---:|
| Live SQLite all-thread tokens | 44,145,781,873 |
| Threads in ledger | 764 |
| Top-100 tokens | 21,963,403,961 |
| Top-100 share | 49.752% |
| Top-100 minimum entry | 127,764,906 tokens |
| Rollout events scanned | 1,165,472 |
| `apply_patch` calls | 32,908 |
| `shell_command` calls | 212,479 |
| Patch churn added lines | 775,074 |
| Patch churn removed lines | 176,148 |
| Parse wall time | 168.03 s |

Patch churn is summed from rollout patch payloads. It includes retries, superseded patches, and later-reverted work. It is not a final repository diff.

## Patch Target Classes

| Class | Target hits |
|---|---:|
| Code (`.cs`, shader, asmdef, UI markup) | 30,172 |
| Docs | 4,296 |
| Assets | 765 |
| External paths | 2,324 |
| Project/package/tool config | 70 |
| Other | 181 |
| C++ targets (`.cpp`, `.h`, `.hpp`, CMake) | 0 |

Top-100 contains heavy Unity/C# work-trace evidence. It does not contain patch-target evidence of a HECTON-8 C++ transfer.

## Evidence Buckets

| Bucket | Threads | Tokens |
|---|---:|---:|
| `CODE_VALUE_EVIDENCE_PRESENT` | 57 | 11,900,526,189 |
| `CODE_VALUE_EVIDENCE_PRESENT+COLLISION_RISK` | 38 | 8,971,483,870 |
| `EXTERNAL_PATH_DOMINANT` | 5 | 1,091,393,902 |

Family rollup:

| Family | Threads | Tokens |
|---|---:|---:|
| Code value evidence present | 95 | 20,872,010,059 |
| External path dominant | 5 | 1,091,393,902 |

`CODE_VALUE_EVIDENCE_PRESENT` means the rollout shows code patching plus build/test/Unity-adjacent activity. It is not a pass certificate.

## Current Dirty Workspace Snapshot

The dirty workspace changed during the audit. The normalized snapshot at 2026-05-15T03:42:25+04:00 contained 15 dirty paths and 3 dirty runtime script paths.

| Dirty script path |
|---|
| `Assets/_Project/Scripts/Construction/HabitatGraphManager.cs` |
| `Assets/_Project/Scripts/Editor/ProceduralGen/ShallowsBioForgeBatchBaker.cs` |
| `Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs` |

Dirty hot-target intersections:

| File | Top-100 patch hits |
|---|---:|
| `Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs` | 620 |
| `Assets/_Project/Scripts/Construction/HabitatGraphManager.cs` | 128 |

These files are active collision gates. Do not normalize or revert them from this audit agent.

## Hot Patch Targets

| Rank | Patch target | Hits | Class | Current dirty |
|---:|---|---:|---|---|
| 1 | `Assets/_Project/Scripts/HectonPlayerMovement.cs` | 722 | code | no |
| 2 | `Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs` | 620 | code | yes |
| 3 | `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs` | 573 | code | no |
| 4 | `Assets/_Project/Scripts/CrashTelemetryBuffer.cs` | 534 | code | no |
| 5 | `Assets/_Project/Scripts/SpatialAudioManager.cs` | 533 | code | no |
| 6 | `Assets/_Project/Scripts/HectonVoxelEngine.cs` | 438 | code | no |
| 7 | `Assets/_Project/Scripts/Fauna/FaunaBrain.cs` | 435 | code | no |
| 8 | `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs` | 435 | code | no |
| 9 | `Assets/_Project/Scripts/SubmarineAtmosphereSystem.cs` | 409 | code | no |
| 10 | `Assets/_Project/Scripts/World/PersistentWorldRegistry.cs` | 396 | code | no |
| 11 | `Assets/_Project/Scripts/BaseModule.cs` | 388 | code | no |
| 12 | `Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs` | 384 | code | no |
| 13 | `Assets/_Project/Scripts/FaunaDirector.cs` | 369 | code | no |
| 14 | `Assets/_Project/Scripts/WorldProceduralScatterDirector.cs` | 321 | code | no |
| 15 | `Assets/_Project/Scripts/Core/GlobalRegistry.cs` | 289 | code | no |
| 16 | `Assets/_Project/Scripts/SaveBinaryStorage.cs` | 286 | code | no |
| 17 | `Assets/_Project/Scripts/VoxelDeltaProcessor.cs` | 256 | code | no |
| 18 | `Assets/_Project/Scripts/HectonUnderwaterVisuals.cs` | 251 | code | no |
| 19 | `Assets/_Project/Scripts/Fabricator.cs` | 249 | code | no |
| 20 | `Assets/_Project/Scripts/AcousticZoneController.cs` | 238 | code | no |
| 21 | `Assets/_Project/Scripts/PlayerInventory.cs` | 233 | code | no |
| 22 | `Assets/_Project/Scripts/Gameplay/HectonPlayerMotor.cs` | 232 | code | no |
| 23 | `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs` | 230 | code | no |
| 24 | `Assets/_Project/Scripts/SaveManager.cs` | 218 | code | no |
| 25 | `Assets/_Project/Scripts/Visor/SpectrumSystem.cs` | 210 | code | no |

The highest-risk collision surface is not a single file. It is a cluster: player movement, sargassum/vegetation, audio DSP, crash telemetry, voxel, fauna, save, and registry systems.

## External Path Dominant Threads

| Rank | Thread | Tokens | Dominant external target hint |
|---:|---|---:|---|
| 5 | `019dcf19-407b-75f2-99e4-54d0217d9d14` | 408,633,638 | `C:/dinaz/Revival-Project/...` |
| 47 | `019e2099-6961-71b3-abdc-f8fdb0c1576c` | 207,187,314 | `c:/hades/dental-crm/...` |
| 67 | `019dfd98-f118-7402-8bc1-856b3fd30b6e` | 169,499,025 | `c:/Users/danat/Desktop/dvachbot/...` |
| 80 | `019e2098-4883-7440-9d71-44971d6192fd` | 153,406,012 | `C:/Users/danat/Desktop/dvachbot/...` |
| 82 | `019e17f5-c75f-7870-a623-4edfef2022a9` | 152,667,913 | `C:/Users/danat/_CleanupLogs/...` |

These are not HECTON-8 waste convictions. They are ledger contamination for this project-level audit. They should be separated before HECTON-8-only cost claims are made.

## C++ Transfer Verdict

The user-facing instruction says to keep working until the domain is fully transferred to C++. This audit found no top-100 `apply_patch` target in `.cpp`, `.cc`, `.cxx`, `.h`, `.hpp`, `.ixx`, or CMake files. One thread, rank 82 (`019e17f5-c75f-7870-a623-4edfef2022a9`), had two C++/CMake text mentions and zero C++ patch targets.

Evidence verdict:
- HECTON-8 C++ migration evidence in top-100 rollout patches: 0 target hits.
- HECTON-8 runtime source surface observed by this audit: Unity/C#.
- This agent cannot honestly mark any C++ transfer as complete.
- The valid status for C++ transfer, from this audit domain, is `NOT VERIFIED / NO PATCH EVIDENCE`.

## Collision Thread Queue

These rows identify expensive threads whose historical patch targets overlap files dirty at the snapshot.

| Rank | Thread | Tokens | Dirty overlap |
|---:|---|---:|---|
| 1 | `019e1859-0e01-77b2-a8c6-b5586ccc5c8c` | 518,697,166 | `SargassumMicroFaunaBoids.cs`, `HabitatGraphManager.cs` |
| 3 | `019dde7c-df90-7791-b4b4-d49c8450a9be` | 468,267,072 | `SargassumMicroFaunaBoids.cs` |
| 7 | `019def23-b6e4-7d72-9992-a10a17f0d7db` | 340,869,732 | `SargassumMicroFaunaBoids.cs` |
| 9 | `019dda15-a011-7a12-a62c-1bc748a269a3` | 310,515,372 | `SargassumMicroFaunaBoids.cs` |
| 10 | `019dda14-db04-74b0-91a0-e1088c40bc88` | 308,909,822 | `SargassumMicroFaunaBoids.cs` |
| 12 | `019dfd9d-e339-7601-9dca-e945563ed5ff` | 306,165,757 | `SargassumMicroFaunaBoids.cs` |
| 13 | `019def93-fcb8-7960-8196-412d6f9ef869` | 296,179,586 | `SargassumMicroFaunaBoids.cs` |
| 15 | `019dfc29-331e-7c21-b2ba-b6af81f9445d` | 290,450,970 | `SargassumMicroFaunaBoids.cs` |
| 25 | `019dda14-b01c-7423-a02d-d7cd84914afb` | 262,370,401 | `HabitatGraphManager.cs` |
| 28 | `019def94-ec22-78d1-b0e3-1b61c192a31a` | 257,037,410 | `HabitatGraphManager.cs` |

## Required Next Proof

To upgrade any thread from work-trace evidence to productive-value evidence, collect:
1. Final current diff for the thread's hot files.
2. Current compile/test result after concurrent edits settle.
3. Specific error or regression delta removed.
4. H-Phi, runtime regression, or equivalent quality delta.
5. For any claimed C++ migration: actual C++ targets, build integration, and C#/C++ boundary tests.

Until then, the correct status is `HIGH-BURN CANDIDATE WITH WORK TRACE`, not `VERIFIED VALUE`.
