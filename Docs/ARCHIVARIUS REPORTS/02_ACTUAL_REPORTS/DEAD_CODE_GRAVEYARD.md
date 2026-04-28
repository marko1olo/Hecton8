# DEAD CODE GRAVEYARD — Static Analysis Report

> **Status:** ETA SANITIZED  
> **Mandates Followed:** AGENTS.md § Architecture First · § No God Objects  
> **Method:** Regex-based static analysis. AST-unaware. Cross-file call detection limited to same-file scope.

---

## 1. METHODOLOGY DISCLAIMER

This report uses **heuristic regex scanning**, not a full Roslyn AST walk. Therefore:

- **False positives exist.** Methods called via `reflection`, `interface dispatch`, `delegates`, or from **other C# files** will appear as "dead" even if they are alive.
- **False negatives exist.** Methods with non-`private` visibility that are unused are not flagged.
- **Event handlers** (`OnClick`, `OnValueChanged`) subscribed in Inspector or via `+=` in another file may appear dead.
- **Burst job `Execute()` methods** called by the job system may appear dead.

> **Action required:** A human must review each entry before deletion. Do **not** mass-delete based on this list alone.

---

## 2. EXECUTIVE SUMMARY

| Category | Count | Severity |
|----------|-------|----------|
| Suspect dead `private` methods | **787** | 🔴 High bloat risk |
| Suspect dead `struct` declarations | **31** | 🟡 Moderate bloat risk |
| Files with ≥10 dead methods | **7** | 🔴 Priority cleanup targets |

---

## 3. TOP OFFENDERS (Files with highest dead-method density)

| # Dead Methods | File | Notes |
|----------------|------|-------|
| 55 | `WorldProceduralScatterDirector.cs` | Likely contains many editor-only or WIP scatter logic paths |
| 31 | `Input/InputManager.cs` | Legacy input abstraction; may have platform-specific stubs |
| 25 | `UI/SettingsPanel.cs` | UI callback stubs for unimplemented settings |
| 21 | `SaveBinaryPayloadCodec.cs` | Serialization helper overloads; verify before delete |
| 19 | `World/HectonMapMagicVegetationBridge.cs` | Bridge code with dormant integration hooks |
| 12 | `UI/PDAControlsRebindUI.cs` | Rebind UI with placeholder handler methods |
| 12 | `MainMenuController.cs` | Menu state stubs |
| 12 | `Core/InputDispatcher.cs` | Input event handlers possibly wired externally |
| 12 | `UI/PauseControlsPanel.cs` | Pause menu stub callbacks |
| 10 | `Audio/HectonMusicDirector.cs` | Music transition stubs |
| 10 | `UI/PauseMenuController.cs` | Pause state stubs |
| 10 | `UI/PDALoadoutTab.cs` | Loadout tab placeholder methods |
| 9 | `PlayerToolManager.cs` | Tool state callbacks possibly orphaned |
| 9 | `PlayerPDA.cs` | PDA tab switch stubs |
| 9 | `UI/PDAShellChrome.cs` | Chrome animation stubs |
| 9 | `HectonFabricatorUI.cs` | Fabricator UI callback stubs |

---

## 4. SAMPLE DEAD METHODS (Verified by name-only occurrence count ≤1)

> **Review each before deletion.** Some may be event handlers wired via Unity Inspector or `+=` in another file.

| File | Method | Likely Nature |
|------|--------|---------------|
| `AcousticZoneController.cs` | `HandleSoundscapeTierChanged` | Event handler — verify subscription |
| `AcousticZoneController.cs` | `HandleSonarPingSent` | Event handler — verify subscription |
| `AcousticZoneController.cs` | `HandlePhysicsImpact` | Event handler — verify subscription |
| `BaseModule.cs` | `EnsureRepairIntegrityCapInitialized` | One-shot init — may be dead |
| `BaseModule.cs` | `InitializeBreathableReserveCold` | Cold alloc path — may be dead |
| `BaseModule.cs` | `ResolveAirRefillScale` | Calculation helper — may be inlined |
| `BaseModule.cs` | `TrackAirReserveStateTransitions` | Telemetry — may be dead |
| `BiomeMatrixDirector.cs` | `EditorUpdate` | Editor-only — should be `#if UNITY_EDITOR` |
| `BuoyancyObject.cs` | `IsFinitePositive` | Math guard — may be unused after refactor |
| `BuoyancyObject.cs` | `IsFiniteNonNegative` | Math guard — may be unused after refactor |
| `CrashTelemetryBuffer.cs` | `HandleLogMessageReceived` | Event handler — verify subscription |
| `CrashTelemetryBuffer.cs` | `HandleLogMessageReceivedThreaded` | Threaded callback — verify subscription |
| `CrashTelemetryBuffer.cs` | `HandleUnhandledException` | Exception callback — verify subscription |
| `CrashTelemetryBuffer.cs` | `ExecuteBackgroundExport` | Background job — verify caller |
| `Fabricator.cs` | `HandleScanLogChanged` | Event handler — verify subscription |
| `Fabricator.cs` | `HandleModRecipeRegistryChanged` | Event handler — verify subscription |
| `Fabricator.cs` | `HandleLanguageChanged` | Localization callback — verify subscription |
| `FaunaDirector.cs` | `CullDistantCreatures` | Culling logic — may be replaced by spatial hash |
| `FaunaDirector.cs` | `FindCreatureTypeIndex` | Lookup helper — may be unused |
| `FaunaDirector.cs` | `IsPredatorArchetype` | Predicate — may be unused |
| `GameTickManager.cs` | `RegisterEditorPlayModeHooks` | Editor-only — should be `#if UNITY_EDITOR` |
| `GameTickManager.cs` | `HandleEditorPlayModeStateChanged` | Editor-only — should be `#if UNITY_EDITOR` |
| `GlobalPhysicsStateManager.cs` | `HandleSceneLoaded` | Scene callback — verify subscription |
| `HectonAtmosphereManager.cs` | `EditorTick` | Editor-only — should be `#if UNITY_EDITOR` |

---

## 5. SAMPLE DEAD STRUCTS (Declared but never referenced in same file)

> **Caution:** Structs may be referenced from other files or used as serialization DTOs.

| File | Struct | Likely Nature |
|------|--------|---------------|
| `CaveTypes.cs` | `CaveSpawnData` | Spawn payload — may be referenced from world gen |
| `FastCandidateMap.cs` | `FastCandidateMap` | Internal map type — may be used in other files |
| `LocalizedAudioClipSet.cs` | `LocalizedAudioClipSet` | Localization data — may be referenced by audio system |
| `LocalizedTextReference.cs` | `LocalizedTextReference` | Text ref — may be referenced by UI |
| `SaveBinaryStorage.cs` | `DeltaCell` | Save DTO — may be referenced by SaveManager |
| `SaveData.cs` | `InventoryCellDTO` | Save DTO — may be referenced by inventory system |
| `WorldGenerativeGeologySeamPlan.cs` | `WorldGenerativeGeologySeamPlan` | Geology data — may be referenced by bridge |
| `WorldGenerativeGeologyVoxelBlendRequest.cs` | `WorldGenerativeGeologyVoxelBlendRequest` | Job payload — may be referenced by geology system |
| `Atmosphere/SurfaceWeatherMath.cs` | `SurfaceWeatherMathJob` | Burst job — `Execute()` may appear dead but is job-scheduled |
| `Audio/HectonMusicClip.cs` | `HectonMusicClip` | Audio data — may be referenced by music director |
| `Core/PlayerInputState.cs` | `PlayerInputState` | Input snapshot — may be referenced by input dispatcher |
| `Core/Data/InventoryCost.cs` | `InventoryCost` | Economy data — may be referenced by crafting |
| `Economy/ResourceStack.cs` | `ResourceStack` | Stack data — may be referenced by inventory |
| `Ecosystem/FaunaGeneticTraits.cs` | `FaunaGeneticTraits` | Genetics — may be referenced by fauna spawn |
| `Fauna/FaunaBrain.Compatibility.cs` | `PredatorMemory` | AI memory — may be referenced by brain |
| `Gameplay/HectonCameraState.cs` | `HectonCameraState` | Camera data — may be referenced by camera controller |
| `ModdingAPI/ModMetadata.cs` | `ModMetadata` | Mod data — may be referenced by mod loader |
| `ModdingAPI/ModRuntimeInfo.cs` | `ModRuntimeInfo` | Mod data — may be referenced by mod loader |
| `Optimization/AssetRecord.cs` | `AssetRecord` | Streaming data — may be referenced by asset manager |
| `Optimization/AssetRecord.cs` | `AssetDispatchTicket` | Streaming ticket — may be referenced by asset manager |

---

## 6. RECOMMENDED CLEANUP PROTOCOL

| Step | Action | Owner |
|------|--------|-------|
| 1 | **Never mass-delete.** For each suspect, grep the entire codebase for the method/struct name. If 0 hits outside declaration → mark for deletion. | Developer |
| 2 | For methods named `Handle*` or `On*` — search for `+=` subscriptions and Unity Inspector event bindings before deleting. | Developer |
| 3 | For Burst job structs — verify `IJob*` implementation and `Schedule()` calls in other files. | Developer |
| 4 | For DTO structs in `Save*.cs` — verify `ISaveable` or binary serializer references. | Developer |
| 5 | After deletion, run a full build + playmode smoke test. | QA |
| 6 | Re-run this scan monthly. Target: <200 suspect methods. | ARCHIVARIUS |

---

## 7. REGRESSION MODEL

| Risk | Impact | Mitigation |
|------|--------|------------|
| Deleting a method called via reflection | Runtime crash / missing feature | Grep full codebase before delete |
| Deleting an event handler wired in Inspector | UI unresponsive | Search `.prefab` and `.unity` YAML for method name |
| Deleting a Burst job struct | Build failure / job system error | Verify `IJob` interface usage |
| Deleting a save DTO | Save corruption / load failure | Verify `SaveManager` binary layout references |

---

*Report generated by ARCHIVARIUS regex sweep. 787 methods + 31 structs flagged for human review. Raw list available on request.*
