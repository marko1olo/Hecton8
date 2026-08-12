# Handoff Report: Codebase vs Documentation Mandate Verification

**Agent**: `explorer_codebase_alignment` (`teamwork_preview_explorer`)  
**Working Directory**: `C:\hades\Hecton8\.agents\explorer_codebase_alignment\`  
**Target Domain**: HECTON-8 Unity C# Codebase (`Assets/_Project/Scripts/`) vs Authority Spine (`AGENTS.md`, `PROJECT_BIBLES.md`, `Docs/SYSTEMS_CONTRACTS.md`, `.agents-skills/` mandates)

---

## 1. Observation

Direct observations and evidence collected during audit:

1. **SaveData Managed Collections**:
   - `Assets/_Project/Scripts/SaveData.cs:153`: `public Dictionary<string, float> toolDurabilityMap = new Dictionary<string, float>();`
   - `Assets/_Project/Scripts/SaveData.cs:156`: `public Dictionary<string, bool> toolBrokenMap = new Dictionary<string, bool>();`
   - `Assets/_Project/Scripts/SaveData.cs:159`: `public HashSet<int> discoveredBiomeIds;`
   - `Assets/_Project/Scripts/SaveData.cs:365`: `public Dictionary<string, string> CustomModData = new Dictionary<string, string>();`
   - Mandate: `AGENTS.md:197` and `DATA_Save_Persistence_Binary_Delta_Checksum.txt:43` state: `"Managed-collections with dynamic allocations (e.g., Dictionary<string, T> or HashSet<string>) in the root structures of SaveData.cs are banned"`.

2. **Voxel SDF Quality Bias (Determinism Breach)**:
   - `Assets/_Project/Scripts/World/OfflineHadalArchBaker/Editor/HadalArchBakeJobs.cs:23`:
     `float arch = SdVerticalTorus(p, math.lerp(18f, 28f, math.saturate(Config.GlobalQualityWeight)), 5.5f);`
   - Mandate: `AGENTS.md:237` & `ORIGINAL_REQUEST.md` (R1/R2) state: `"GlobalQualityWeight... must not change gameplay truth ownership, DTO layout... or deterministic state ownership."`

3. **Kinematic Arrest Gate Missing Player Suspension**:
   - `Assets/_Project/Scripts/HectonPlayerSpawner.cs:1685`:
     `/// STILL MISSING (not implemented here): player suspension itself — IsSuspended, gravity/velocity zero, input lock and screen blackout live in the movement/UI route, which this change does not touch.`
   - `Assets/_Project/Scripts/HectonPlayerSpawner.cs:2109`:
     `if (!WorldChunkPhysicsBakedEvents.IsLaneActive)` releases player via `TeleportPlayer(_spawnPosition)`.
   - Mandate: `AGENTS.md:199` states: `"spawner/KCC logic must execute a Kinematic Arrest Gate. The player must remain suspended (IsSuspended = true, gravity/velocity zero, input locked, screen blacked out) until HectonVoxelVolume or MapMagicBridge broadcasts WorldChunkPhysicsBakedSignal"`.

4. **ARM64 Struct Layout Violations in Jobs / DTOs**:
   - `Assets/_Project/Scripts/World/VoxelSurfaceNets/VoxelSurfaceNetsJobs.cs:88`: `public bool IsCanonicalCollider;` inside Burst `IJob` struct `ExtractSurfaceNetsJob`.
   - `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs:2000-2012`: `ChunkBuildPendingJob` contains `public bool Active;`, `public bool Cancelled;`, and managed interface reference `public IDataVault ReadPinVault;`.
   - Mandate: `DATA_Runtime_Struct_Layout_ARM64.txt:49` and `AGENTS.md:287` ban C# `bool` fields and managed reference fields in job and DTO structs.

5. **Scene Scans in Runtime Initialize**:
   - `Assets/_Project/Scripts/Graphics/HectonVisualsOrchestrator.cs:133`:
     `var oceanRenderer = FindAnyObjectByType<Renderer>();` called inside `[RuntimeInitializeOnLoadMethod]`.
   - Mandate: `AGENTS.md:260` forbids `FindObjectOfType`/scene search in initialization/hot paths.

6. **Hardcoded KeyCode Usage**:
   - `Assets/_Project/Scripts/ControlScheme.cs:21-64`: Contains fields `public KeyCode interactKey = KeyCode.E;`, `public KeyCode swimAscendPrimary = KeyCode.Space;`, etc.
   - Mandate: `Docs/SYSTEMS_CONTRACTS.md:168` states: `"Forbidden: Hardcoded KeyCode in gameplay authority."`

---

## 2. Logic Chain

1. *Observation 1 (SaveData)* -> `AGENTS.md` and `DATA_Save_Persistence_Binary_Delta_Checksum.txt` require flat binary lists and `ISerializationCallbackReceiver` for zero-allocation binary save/delta serialization. `SaveData.cs` directly includes `Dictionary` and `HashSet` fields in its root DTO structure, causing GC allocations during serialization and violating unmanaged layout mandates.
2. *Observation 2 (Voxel SDF)* -> `voxels.md` and `ORIGINAL_REQUEST.md` require SDF terrain distance fields to evaluate using canonical world coordinates independent of camera view or `GlobalQualityWeight`. `HadalArchBakeJobs.cs` scales the torus radius using `GlobalQualityWeight` (18m at weight 0 to 28m at weight 1), causing mesh/collider vertex divergence across graphics settings.
3. *Observation 3 (Kinematic Arrest Gate)* -> `AGENTS.md:199` mandates holding player suspension (`IsSuspended`, zero velocity, input lock, blackout) until `WorldChunkPhysicsBakedSignal` is published. `HectonPlayerSpawner.cs` explicitly comments that player suspension is omitted, and releases the player if `WorldChunkPhysicsBakedEvents.IsLaneActive` is false.
4. *Observation 4 (ARM64 & Managed Refs in Jobs)* -> `DATA_Runtime_Struct_Layout_ARM64.txt` bans C# `bool` fields (1-byte C# layout ambiguity) and managed class/interface fields in Job/DTO structs. `VoxelSurfaceNetsJobs.cs` uses `bool IsCanonicalCollider;` in a Burst job struct, and `HectonMapMagicVegetationBridge.cs` embeds `IDataVault ReadPinVault;` (managed reference) inside a job state struct.
5. *Observation 5 (Scene Scans)* -> `AGENTS.md:260` forbids scene search (`FindObjectOfType`, `FindAnyObjectByType`) in initialization. `HectonVisualsOrchestrator.cs` executes `FindAnyObjectByType<Renderer>()` across the entire scene on `RuntimeInitializeOnLoadMethod`.
6. *Observation 6 (Hardcoded KeyCode)* -> `SYSTEMS_CONTRACTS.md` bans hardcoded `KeyCode` fields in gameplay authority. `ControlScheme.cs` defines legacy `KeyCode` fields instead of routing all inputs through Unity Input System Action Maps.

---

## 3. Caveats

- **Scope of Audit**: The investigation covered `Assets/_Project/Scripts/` and governing documentation in `AGENTS.md`, `PROJECT_BIBLES.md`, `Docs/SYSTEMS_CONTRACTS.md`, and `.agents-skills/` mandates.
- **Read-Only Constraint**: No C# code edits or documentation fixes were made, as this role is strictly `teamwork_preview_explorer` (read-only investigation).
- **Execution & Profiling**: Static analysis was verified using `rg` and file viewing. Interactive Unity playmode runtime profiling requires running Unity batchmode/PlayMode proof gates when implementers refactor these systems.

---

## 4. Conclusion

The HECTON-8 Unity C# codebase demonstrates strong compliance with performance policies in core areas (0 LINQ imports in runtime non-editor scripts, 0 naked `Camera.main` calls, isolated `HectonEventBus` usage for modding). However, concrete documentation-vs-code discrepancies exist:
1. `SaveData.cs` violates the Save Persistence mandate by holding managed `Dictionary` and `HashSet` root fields.
2. `HadalArchBakeJobs.cs` violates Voxel SDF determinism by mutating SDF shape geometry based on `GlobalQualityWeight`.
3. `HectonPlayerSpawner.cs` violates the Kinematic Arrest Gate mandate by omitting actual player suspension.
4. `VoxelSurfaceNetsJobs.cs` and `HectonMapMagicVegetationBridge.cs` violate the ARM64 Struct Layout mandate by using C# `bool` and managed interface fields in job structs.
5. `HectonVisualsOrchestrator.cs` violates bootstrap rules by running `FindAnyObjectByType<Renderer>()` during runtime initialization.
6. `ControlScheme.cs` violates input contracts by hardcoding legacy `KeyCode` fields.

---

## 5. Verification Method

To independently verify these findings, execute the following commands from `C:\hades\Hecton8`:

1. **Verify Mandate Registry Integrity**:
   ```powershell
   python -B Tools/Docs/TestMandateRegistry.py --strict
   ```
   *(Expected output: `MANDATE_REGISTRY_CHECK=PASS errors=0 warnings=0 mandates=80`)*

2. **Verify SaveData Managed Collections Discrepancy**:
   ```powershell
   rg -n "(Dictionary|HashSet)<" Assets/_Project/Scripts/SaveData.cs
   ```
   *(Inspect lines 153, 156, 159, 365)*

3. **Verify Voxel SDF Quality Weight Mutation**:
   ```powershell
   rg -n "GlobalQualityWeight" Assets/_Project/Scripts/World/OfflineHadalArchBaker/Editor/HadalArchBakeJobs.cs
   ```
   *(Inspect lines 22-25)*

4. **Verify Kinematic Arrest Gate Missing Suspension**:
   ```powershell
   rg -n "STILL MISSING" Assets/_Project/Scripts/HectonPlayerSpawner.cs
   ```
   *(Inspect line 1685)*

5. **Verify ARM64 Job Struct bool and Managed Interface Fields**:
   ```powershell
   rg -n "IsCanonicalCollider" Assets/_Project/Scripts/World/VoxelSurfaceNets/VoxelSurfaceNetsJobs.cs
   rg -n "ReadPinVault" Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs
   ```
   *(Inspect `VoxelSurfaceNetsJobs.cs:88` and `HectonMapMagicVegetationBridge.cs:2012`)*

6. **Verify Scene Scan in Visuals Bootstrap**:
   ```powershell
   rg -n "FindAnyObjectByType" Assets/_Project/Scripts/Graphics/HectonVisualsOrchestrator.cs
   ```
   *(Inspect lines 126-133)*

7. **Verify Hardcoded KeyCode**:
   ```powershell
   rg -n "KeyCode\." Assets/_Project/Scripts/ControlScheme.cs
   ```
   *(Inspect lines 21-64)*
