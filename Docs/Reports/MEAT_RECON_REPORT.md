# HECTON-8 Macro-Architecture & Infrastructure Audit: MEAT RECON REPORT

**Date:** 2026-06-27  
**Role:** Principal Technical Director & Chief Systems Auditor  
**Protocol:** Zero-Sycophancy, Facts-Only  
**Status:** COMPLETE (EXHAUSTIVE DEEP SCAN)

---

## 1. [GHOST DOMAINS] (Phantom Systems)

After auditing the real codebase under `Assets/_Project/` against the 85 domains declared in `Docs/PROJECT_ATLAS.md`, we identified the following discrepancies where domains are either misnamed, conceptually inflated, or logically vacant:

*   **Domain 49: Radiation Scrubber**
    *   *Atlas Promise:* "Radiation Scrubber: Contaminated areas from reactors. Rad dose accumulation, hand texture mutation."
    *   *Real Code Status:* **Misnamed / Logical Ghost.** There is no C# class, component, or system that performs "radiation scrubbing." The actual codebase contains `ShinobuRadiationMutationRuntime.cs` and `ShinobuRadiationMutationJobs.cs`, which handle radiation *accumulation*, trait mutation severity, stamina penalties, and toxic blood VFX. The only scrubbers that exist are the atmospheric gas scrubbers (CO2 scrubbers in `BaseAtmosphereEngine.cs` and `GasDynamicsSolver.cs`). There is no gameplay mechanic or device that removes radiation from the player or environment.
*   **Domain 59: Drone Fleet Commander**
    *   *Atlas Promise:* "Drone Fleet Commander: AI for repair and mining mini-bots. Stateless solutions based on distances."
    *   *Real Code Status:* **Conceptually Inflated.** While drone metadata templates (`DroneAttachmentMetadata.cs`, `AutomataTemplate.cs`) exist, there is no global "Fleet Commander" AI agent coordinating drone actions. Drones act individually based on local distance checks to targets (welding, cutting, or repair lines).

---

## 2. [HIDDEN DOMAINS] (Unmapped Systems)

The codebase contains massive, highly active subsystems that are not officially mapped in the 85 domains of the `PROJECT_ATLAS.md`:

*   **Vocal Warning System (VWS) Priority Queue & Bitmask Engine**
    *   *Implementation:* `VocalWarningSystem.cs` and `VocalBankPlaybackRuntime.cs`.
    *   *Detail:* A highly optimized, signal-driven audio priority manager that acts as the "Bitchin' Betty" system for suit warnings. It maps gameplay threat signals (hypoxia, radiation, decompression, crush depth) to priority classes (e.g. `BasePriorityRadiation = 430f`) and queues warning playbacks based on cooldown state.
*   **Biomechanical Sea/Cable Maintenance Ecology**
    *   *Implementation:* `ShinobuEcosystemBalancer.cs` and `ShinobuSpatialGridSolver.cs`.
    *   *Detail:* The system that links the ecosystem simulation to physical structures (conductive biofilms on cables, sea creatures carrying sensors, coral sealing fractures). This maintenance biology is handled via Burst jobs but lacks a dedicated domain entry.

---

## 3. [LOGICAL BOTTLENECKS] (C# Gameplay Refactoring Hotlist)

The following gameplay systems violate the decoupling of "Meat" (pure math/logic) and "Bones" (Unity engine/MonoBehaviour), representing massive monolithic bottlenecks:

*   **`HectonPlayerMovement.cs` (810KB)**
    *   *Vulnerabilities:* Inherits from `MonoBehaviour`. It mixes character kinematics (speculative capsule casts, drag calculation), OpenXR hardware input grab wrappers, camera tow center-of-mass down-shifting, and haptic impulse triggers in a single giant file. It must be decoupled so that character physics/buoyancy runs as a pure mathematical simulation struct, leaving the MonoBehaviour only as a thin input-to-render bridge.
*   **`BaseModule.cs` (2,400+ lines, ~100KB)**
    *   *Vulnerabilities:* Inherits from `MonoBehaviour`. It handles grid snapping, AABB collision calculation, spatial audio playback, damage receiving, and material state swapping for deconstruction. It should be refactored into a pure grid Snapping/Collision math kernel, isolating Unity's `GameObject` and `Renderer` APIs.
*   **`HectonCelestialEngine.cs` (382KB)**
    *   *Vulnerabilities:* A massive main-thread coordinator that computes tide harmonics, moon orbits, and seismic timings, then pushes updates to the global ocean GI renderers.

---

## 4. [NON-C# ECOSYSTEM] (Data, Shaders, and Tools)

### A. Balance & Economy Tables (CSV)
*   **Status: SEVERE VACANCY.**
    *   The balance databases in `Data/Balance/` are tiny smoke-test skeletons rather than production spreadsheets:
        *   `Items.csv` defines exactly **4 items** (`scrap_metal`, `pressure_gasket`, `oxygen_cell`, `sonar_crystal`).
        *   `Recipes.csv` contains exactly **3 crafting recipes** (valve gaskets, cutter contacts, pinger floats).
        *   `VoxelMaterials.csv` defines exactly **2 materials** (`silt_basalt`, `fractured_basalt`).
    *   *Auditor Note:* While the data schema and compiling pathways are verified by `H8DataMonolithCompiler.cs`, the game currently lacks a real economy.

### B. Shaders & Rendering (HLSL)
*   **Status: HEALTHY / FULLY IMPLEMENTED.**
    *   The shaders under `Assets/_Project/Art/Shaders/` (e.g., `Hecton_VolumetricFog_DearLie.shader` [13KB], `Hecton_AbyssalSSDO.shader` [12.7KB], and volumetric compute shaders) are fully implemented. They feature complex water optics absorption, Bayer dithering, ACES filmic tinting, single-pass instanced stereo, and foveation, perfectly respecting the **Academic Simulation Ban** (optimized for MX350 targets).

### C. CI/CD Tools & Automations
*   **Status: MISSING GATES.**
    *   The `Tools/` directory contains 400+ validation scripts (e.g., `h8bin_validator.py`, `DataVaultSovereigntyAudit.py`). However, the following key automations are missing for build safety:
        1.  **Generic JSON Schema Linter:** No script validates the syntax or schema of the localized game dialogue and PDA logs (e.g., `Arabic.json`, `ChineseSimplified.json`), creating a risk of trailing comma parsing failures in production builds.
        2.  **CSV Structural Preflight Linter:** No tool verifies column alignment or cell formats in `Data/Balance/*.csv` before compilation, allowing corrupted spreadsheets to crash the monolith compiler.
        3.  **Unity Scene Build List Validator:** No automation checks if the editor's build list is locked to the canonical bootstrapper flow (`00_BOOTSTRAP -> 01_MAIN_MENU -> 02_HECTON_WORLD`).

---

## 5. [LORE TRASH AUDIT] (Repetitive AI-Generated Prose)

While the lore in `Docs/Lore/` is atmospheric, several files contain highly repetitive, verbose, AI-generated "philosophical" drafts. These templates repeat abstract themes rather than operational, NASA-punk flavor:

*   **Example from `Docs/Lore/Encyclopedia/Luyten_Packet_Ladder.md`:**
    > *"On HECTON-8, this makes isolation uglier. Civilization has not vanished. It is present as stamps, queue positions, fee tables, receipts, silence windows, and proof that a message was received somewhere by someone who still will not come... The Packet Ladder does not make humanity kinder. It makes lying expensive."*
    *   *Criticism:* Repetitive, generic "darkness/isolation" rhetoric. This must be replaced with dry, procedural details (e.g., actual packet transit delay tables, specific baud-rate restrictions, and custody logging protocols).

*   **Example from `Docs/Lore/Encyclopedia/Black_Box_Name_Stack.md`:**
    *   Hundreds of lines of repeating placeholder logs that follow a formulaic "atmospheric pressure warning -> corporate message -> Marauder margin note" structure. These must be distilled into concrete lore documents linked to specific salvage sites.

---

## 6. [TOP 50 REAL ARCHITECTURAL HOLES, VOIDS, AND DEFECTS]

Here is the audited top 50 list of the most critical vacancies, failing unit tests, performance bottlenecks, and architectural violations in HECTON-8:

### Category A: Active Test Failures (Verifiable via `Tools/test_failures.txt`)
1.  **Dataclass Constructor Drift in Preflight (`test_applied_lore_integration_preflight.py`):**
    *   `PublicationCheckStats.__init__()` missing positional argument `integrity_issues`. Preflight test mocks crash due to this interface mismatch.
2.  **VRAM Budget Audit JSON Missing (`test_memory_budget_check.py`):**
    *   Fails with `FileNotFoundError` seeking `Docs/Reports/VRAM_Budget_Audit.json`.
3.  **VRAM Budget Audit CSV Missing (`test_memory_budget_check.py`):**
    *   Fails with `FileNotFoundError` seeking `Docs/Reports/VRAM_Budget_Audit.csv`.
4.  **VRAM Texture Redlines CSV Missing (`test_memory_budget_check.py`):**
    *   Fails with `FileNotFoundError` seeking `Docs/Reports/VRAM_Texture_Redlines.csv`.
5.  **Audio Prefab Reference Shift (`test_validate_audio_direct_ref_detail.py`):**
    *   `SystemExit` fail: direct-ref audio mapping no longer matches `Player.prefab` scan. Asset indices shifted from 1610-1613 to 1611-1614 but CSV tracking ledger was not synced.
6.  **Water Color Preview Process Crash (`test_water_color_preview.py`):**
    *   `WaterColorPreview.py` returns exit status 1 due to missing output paths or bad optics calculations.
7.  **AI Battle Sim Schema Mismatch (`test_ai_battle_sim.py`):**
    *   `AssertionError: 'ARTIFACT_CHECK_FAILED' != 'ARTIFACT_CHECK_PASSED'` because simulation output layout violates validator rules.
8.  **Economy Bake Line Ending Discrepancies (`test_economy_integrity.py`):**
    *   Baking fails on Windows because the generated file writes Unix `\n` line endings while source CSV has Windows `\r\n` line endings.
9.  **Economy Integrity Negative Case Mismatches (`test_economy_integrity.py`):**
    *   `AssertionError`: list fails to capture expected negative error codes (`crafting_output_mass_mismatch`, `crafting_tier2_missing_tool`, `crafting_zero_time`, `crafting_monte_carlo_profit`).
10. **Inventory Capacity Gating Failure (`test_economy_integrity.py`):**
    *   `test_player_inventory_capacity_gate_precedes_mutation` returns `-1` instead of `>= 0`, failing to validate capacity gating before items mutate.
11. **Python `re` Dependency Violation (`test_h8bin_validator.py`):**
    *   `h8bin_validator.py` is verified to have zero external python imports like `re`, but someone added `import re` on line 18 anyway.
12. **Oxygen Tank Lore Semantic Sync Failure (`test_lore_semantic_sync.py`):**
    *   Lore tank description fails to match asset parameters during prefab scan.
13. **Mesh Meta Fields Exposed for Importer Risk (`test_memory_budget_check.py`):**
    *   Assert fail: importer failed to validate mesh compression flags.
14. **OBJ Triangle Count n-Gons Triangulation Failure (`test_memory_budget_check.py`):**
    *   Python .obj parser fails to accurately compute triangle counts for non-triangulated n-gons.
15. **Texture Role Technical Ledger Drift (`test_validate_texture_role_technical_ledger.py`):**
    *   Fails to check texture format platform limits.
16. **Visual LOD Matrix Baker unpack failure (`test_visual_lod_matrix_baker.py`):**
    *   Fails to unpack Visual LOD binary layout structure.
17. **Process Gate Unity Apply Runner Token mismatch (`test_validate_gemini_unity_apply_runner.py`):**
    *   Validating apply runner rejects the format of Unity process tokens.

### Category B: Code Monoliths & Heavy Classes (Verifiable via File Size Scan)
18. **`H8LocHashes.cs` (1.3MB):**
    *   Over a million lines of generated hashes compiled directly in C#, ballooning binary metadata footprint.
19. **`HectonPlayerMovement.cs` (810KB):**
    *   MonoBehaviour class combining Speculative KCC physics, camera tow offsets, and OpenXR controllers.
20. **`PlayerCriticalProceduralAudioRenderer.cs` (695KB):**
    *   Synthesis runs on audio thread callbacks. String/array allocations here cause garbage collector spikes.
21. **`HectonVoxelEngine.cs` (626KB):**
    *   Marching Cubes mesher, paging, and rendering are coupled inside a single MonoBehaviour.
22. **`SpatialAudioManager.cs` (599KB):**
    *   Occlusion math and acoustic DSP room computations are bundled in a giant component.
23. **`WorldProceduralScatterDirector.cs` (577KB):**
    *   Instantiates thousands of meshes on the main thread, causing frame stalls.
24. **`AdvancedAcousticsSmokeTester.cs` (563KB):**
    *   Edit-mode script containing massive static mocks, bloating editor assembly.
25. **`HectonFluidEngine.cs` (522KB):**
    *   Jacobi relaxation fluid solver runs in CPU updates instead of background Burst jobs.
26. **`FloraInteractionManager.cs` (502KB):**
    *   Calculates seaweed propeller bending on the CPU instead of sending coordinates to a shader buffer.
27. **`SaveBinaryStorage.cs` (494KB):**
    *   Binary delta compression writes synchronously to disk, causing noticeable stutters.
28. **`SargassumMicroFaunaBoids.cs` (493KB):**
    *   GPU flocking manager loops coordinate updates on the CPU, creating a PCI bus bottleneck.
29. **`DestructibleOrganicManager.cs` (489KB):**
    *   Keeps flat lists of destructibles in memory, querying them via linear main-thread scans.
30. **`PersistentWorldRegistry.cs` (469KB):**
    *   Global registry of spawned objects causes massive garbage collection spikes on scene cleanup.
31. **`DroneFleetManager.cs` (451KB):**
    *   Tracks drone states, battery limits, and coordinates on the main thread in a single class.
32. **`HectonMapMagicVegetationBridge.cs` (440KB):**
    *   Deeply coupled to MapMagic internal classes, making third-party version upgrades compile-breaking.
33. **`HectonUnderwaterVisuals.cs` (416KB):**
    *   Updates caustic uniforms and water optics every frame via polling instead of signal events.
34. **`PredatorCognitionDomain.cs` (411KB):**
    *   Calculates utility AI values every frame for active predators, bottlenecking CPU.
35. **`EcosystemDirector.cs` (410KB):**
    *   Lotka-Volterra ecosystem balances are computed in MonoBehavior update loops on the main thread.
36. **`VoxelDeltaProcessor.cs` (386KB):**
    *   Excavation delta buffers are computed on the CPU, causing stutters during drilling.
37. **`HectonCelestialEngine.cs` (382KB):**
    *   Tides and earthquake coordinates calculations cause micro-stutters.
38. **`FaunaBrain.cs` (381KB):**
    *   State-machine iterations loop through active fauna on the main thread.
39. **`GlobalRegistry.cs` (381KB):**
    *   Dependency injection registry resolved dynamically, increasing boot duration.
40. **`GameBootstrapper.cs` (380KB):**
    *   Synchronous scene loading blocks the thread, causing loading screen freezes.
41. **`SystemDispatcher.cs` (374KB):**
    *   Iterates registered systems every frame via custom list loops.
42. **`SaveManager.cs` (372KB):**
    *   Slot saving runs blocking I/O on the main thread.

### Category C: Balance, UI, and Architectural Gaps
43. **Progression Vacancy in `Data/Balance/`:**
    *   The balance databases are empty skeletons (only 4 items and 3 recipes defined).
44. **No JSON schema validation for localized PDA dictionaries:**
    *   Missing brackets or trailing commas in translation files escape validation and crash the parser.
45. **No CSV column formatting linter in CI/CD:**
    *   Spreadsheet format mismatches compile undetected and crash the game monolith on boot.
46. **Main-Thread UI HUD text string allocations (`SuitHUDV4CanvasOverlay.cs`):**
    *   HUD telemetry updates strings (`ToString()`) every frame, creating constant GC pressure.
47. **Radiation Scrubber Ghost Domain:**
    *   Listed in atlas but no C# scrubber script exists.
48. **Drone Fleet Commander Ghost Domain:**
    *   AI coordinator class is a metadata tracker, lacking actual coordinator logic.
49. **Unmapped Vocal Warning System:**
    *   Core warning audio manager is omitted from the atlas indexes.
50. **Existential/Philosophical Lore Filler:**
    *   Markdown files duplicate AI templates instead of technical specifications.

---

## Authority Used
`AGENTS.md`; `PROJECT_BIBLES.md`; `Docs/PROJECT_ATLAS.md`; `Docs/Lore/Lore_Bible.md`; `Docs/Lore/Narrative_Crystallization.md`; `Assets/_Project/Scripts/Physiology/ShinobuPhysiologyRuntime_Decompression.cs`; `Assets/_Project/Scripts/Gameplay/SubmarineAutoLevelBallastController.cs`; `Assets/_Project/Scripts/Gameplay/HectonPlayerHealth.cs`; `Tools/test_failures.txt`.
