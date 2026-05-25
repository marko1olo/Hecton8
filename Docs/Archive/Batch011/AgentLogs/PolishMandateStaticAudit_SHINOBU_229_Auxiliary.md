# Polish Mandate Static Audit

Evidence class: STATIC_SOURCE. No Unity import, compile, Play Mode, profiler, GC, memory, player build, or device proof was executed.

- Schema: `hecton8.polish_mandate_static_audit.v1`
- Source root: `Assets/_Project/Scripts/Equipment/Auxiliary`
- C# files: `6`

## Counts

| Category | Matches | Files |
|---|---:|---:|
| `binaryHardwareSwitch` | 0 | 0 |
| `burstCompile` | 3 | 1 |
| `burstMissingCompileSynchronously` | 0 | 0 |
| `burstMissingFloatMode` | 0 | 0 |
| `burstMissingFloatPrecision` | 0 | 0 |
| `globalQualityWeight` | 22 | 4 |
| `jobHandleComplete` | 0 | 0 |
| `linqSurface` | 0 | 0 |
| `noAlias` | 13 | 1 |
| `packOne` | 0 | 0 |
| `privateNativeCollectionField` | 0 | 0 |
| `structAutoProperties` | 0 | 0 |
| `unityRandom` | 0 | 0 |
| `unityTimeCritical` | 0 | 0 |
| `unityUpdateMethod` | 0 | 0 |

## Top Files

### burstCompile

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/Equipment/Auxiliary/AuxiliaryEquipmentJobs.cs` | 3 |

### globalQualityWeight

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/Equipment/Auxiliary/AuxiliaryEquipmentJobs.cs` | 9 |
| `Assets/_Project/Scripts/Equipment/Auxiliary/AuxiliaryEquipmentRouterRuntime.cs` | 6 |
| `Assets/_Project/Scripts/Equipment/Auxiliary/AuxiliaryEquipmentContracts.cs` | 4 |
| `Assets/_Project/Scripts/Equipment/Auxiliary/Editor/AuxiliaryEquipmentEditorTools.cs` | 3 |

### noAlias

| Path | Count |
|---|---:|
| `Assets/_Project/Scripts/Equipment/Auxiliary/AuxiliaryEquipmentJobs.cs` | 13 |

## Interpretation

- `Pack=1`, private persistent native collections, and Burst attribute drift are platform-portability risks until each hit is classified as cold file-format, owner-local scratch, or hot runtime.
- `jobHandleComplete`, Unity `Update` methods, `Time.*`, and `UnityEngine.Random` are not automatically defects, but they are mandatory review surfaces for gameplay/runtime code.
- Binary hardware switches are suspect unless they are presentation-only or build-time/platform setup. Runtime scalability should flow through continuous `GlobalQualityWeight` curves.
- This audit is a pressure map. It does not mutate code and does not prove frame cost.
