# HECTON-8 Common Sense Engineering (AI Cognitive Blindspots)

Status: STATIC_POLICY
Evidence class: STATIC_DOC
Owner: DOCS_ACTUALIZATION

These are 18 unwritten laws of Unity development that AI agents constantly violate because they lack physical game-dev intuition. They are now binding architectural constraints. You MUST follow them for ALL tasks in Hecton-8.

1. **The Array Axis Swap (2D vs 3D Coordinates)**
   - **Rule**: MapMagic/Terrain 2D arrays (`float[,]`) map to `(X, Z)` in the world. Index `x` is World X. Index `y` is World Z (Forward).
   - **Violation**: AI writes `heights[x, y]` as `(x, y, 0)` or swaps the axis, stretching terrain into oblivion.
   - **Constraint**: Terrain arrays must ALWAYS use `array[z, x]` indexing inside `for(int z=...) for(int x=...)` loops.

2. **The Physics Suicide (Raycast LayerMasks)**
   - **Rule**: `Physics.Raycast` defaults to hitting Water, Triggers, and the Camera Volume.
   - **Violation**: AI shoots a ray down to find the ground, hits the water surface, and spawns coral in mid-air.
   - **Constraint**: ALL Raycasts/Overlaps MUST use explicit `LayerMask.GetMask(...)` and `QueryTriggerInteraction.Ignore`.

3. **The ScriptableObject State Leak**
   - **Rule**: Modifying a `ScriptableObject` field at runtime in the Editor saves the change to disk permanently.
   - **Violation**: AI writes `PlayerStatsSO.Health -= 10`. Next time the game runs, the player starts with 0 health.
   - **Constraint**: ScriptableObjects are STRICTLY READ-ONLY DTOs. Runtime mutation requires instancing or passing to a `struct`.

4. **Float vs Half Precision (The Coordinate Jitter)**
   - **Rule**: `half` is 16-bit precision. It fails at 500 meters from the origin.
   - **Violation**: AI writes `half3 worldPos` in URP shaders to optimize, causing massive Z-fighting and vertex vibration under AUP.
   - **Constraint**: Position (World/Object) in HLSL MUST be `float`. `half` is restricted to Colors, Normals, and 0-1 Masks.

5. **Batchmode Wait Blindness**
   - **Rule**: `yield return null` does not advance the engine in headless `-batchmode` tests.
   - **Violation**: AI writes `yield return null` to wait for async MapMagic, causing tests to hang forever.
   - **Constraint**: Batchmode tests must use explicit engine pumping via `EditorApplication.update` or `EditorApplication.QueuePlayerLoopUpdate()`.

6. **The Material Getter Sin (Asset Leaks)**
   - **Rule**: Calling `renderer.material` creates a permanent clone of the material that leaks RAM/VRAM and breaks SRP Batcher.
   - **Violation**: AI uses `renderer.material.color = Color.red;` for damage flash.
   - **Constraint**: The `renderer.material` getter is BANNED. Visual modifications MUST use `MaterialPropertyBlock`.

7. **The Unity Object Fake Null**
   - **Rule**: Unity overloads `== null` for `UnityEngine.Object` to check the C++ pointer. This is not thread-safe.
   - **Violation**: AI checks `if (gameObject == null)` inside a Burst Job or Task, crashing the engine.
   - **Constraint**: Avoid passing Unity Objects to threads. Use Handle-patterns or pure C# `ReferenceEquals`.

8. **Tick-Spam (The Update Loop Abuse)**
   - **Rule**: Business logic running 60 times a second kills the CPU and Zero-GC budget.
   - **Violation**: AI checks oxygen depletion inside `void Update()`.
   - **Constraint**: NO logic in `Update()`. Use `IGameTickable` with `GameTickManager` at 1Hz/5Hz/10Hz.

9. **The YAML Corruption Trap (Editing Prefabs via Text)**
   - **Rule**: `.prefab`, `.unity`, and `.asset` files are complex serialized YAML graphs heavily dependent on internal `fileID` and `guid` references.
   - **Violation**: AI tries to use `replace_file_content` or `sed` to edit a prefab's scale or add a component directly in the `.prefab` file, corrupting the entire file.
   - **Constraint**: NEVER attempt to parse or edit Unity serialized files (YAML) via text manipulation. Modifications MUST be done via C# Editor scripts (`AssetDatabase`, `PrefabUtility`) or manually by the user in the Unity GUI.

10. **The Async Thread-Safety Crash**
    - **Rule**: The vast majority of the Unity API (`GameObject`, `Transform`, `Texture2D`, `Physics`) is strictly restricted to the Main Thread.
    - **Violation**: AI uses `Task.Run()` to calculate pathfinding and then calls `transform.position = pos` from inside the background task, instantly crashing Unity.
    - **Constraint**: If offloading work to a background thread (`Task.Run` or Burst), you CANNOT touch Unity API objects until you explicitly marshal back to the main thread.

11. **The Silent Memory Leak (Event Subscriptions)**
    - **Rule**: C# events (`+=`) hold strong references to the listening object.
    - **Violation**: AI writes `GameManager.OnPlayerDeath += ShowDeathScreen` in `OnEnable()`, but forgets to `-=` in `OnDisable()`. The UI screen is destroyed, but the GameManager keeps a reference to it, leaking memory and throwing NullReferenceExceptions later.
    - **Constraint**: EVERY `+=` subscription MUST have a strictly guaranteed `-=` unsubscription in `OnDisable` or `Dispose()`.

12. **The Canvas Rebuild Nuke**
    - **Rule**: Disabling a child object of a UI Canvas via `SetActive(false)` marks the entire Canvas hierarchy as dirty, forcing a CPU-heavy geometry rebuild.
    - **Violation**: AI toggles `SetActive` on inventory slot highlights every frame, dropping framerate from 60 to 20.
    - **Constraint**: DO NOT use `SetActive` for hiding active UI elements. Use `CanvasGroup.alpha = 0` and `CanvasGroup.blocksRaycasts = false`.

13. **The `Mathf` vs `math` Burst Collision**
    - **Rule**: Burst compiler and DOTS require `Unity.Mathematics` (`math.abs`, `float3`). They do not support `UnityEngine.Mathf` or standard `Vector3` methods in compiled jobs.
    - **Violation**: AI writes `Vector3.Distance` or `Mathf.Clamp` inside an `IJobParallelFor`, breaking the Burst compilation.
    - **Constraint**: Inside any struct marked `[BurstCompile]` or manipulating ECS data, you are STRICTLY FORBIDDEN from using `UnityEngine.Mathf` or `UnityEngine.Vector3`. Use `Unity.Mathematics` exclusively.

14. **The Addressables Memory Leak**
    - **Rule**: `Addressables.LoadAssetAsync` and `Addressables.InstantiateAsync` bypass the standard Unity Garbage Collector.
    - **Violation**: AI loads a prefab via Addressables and later calls `Destroy(gameObject)`, leaving the asset handle locked in RAM forever.
    - **Constraint**: EVERY Addressable load MUST be paired with `Addressables.Release` or `Addressables.ReleaseInstance`. If you destroy it, you must release it.

15. **The `Destroy` Frame Delay Trap**
    - **Rule**: `Destroy(gameObject)` does NOT delete the object immediately. It marks it for destruction at the end of the current frame.
    - **Violation**: AI calls `Destroy(enemy)`, and then loops through all enemies later in the same frame, hitting the "destroyed" enemy and causing logic bugs.
    - **Constraint**: If you call `Destroy()`, you must also immediately set a structural flag (e.g., `isDead = true`) or remove it from your management lists so the rest of the frame ignores it.

16. **Shader Variant Explosion (Keyword Abuse)**
    - **Rule**: `material.EnableKeyword()` creates a new shader variant. Using `multi_compile` casually multiplies build times geometrically and causes massive runtime stutter.
    - **Violation**: AI adds `#pragma multi_compile _ IS_WET` to a core shader just to make one prop look wet.
    - **Constraint**: Do not introduce new shader keywords without architectural approval. Use vertex colors, packed textures, or global buffers to pass state instead of generating new shader variants.

17. **The Physics Desync (Update vs FixedUpdate)**
    - **Rule**: PhysX runs on a fixed time step. Unity's visual frame rate is variable.
    - **Violation**: AI writes `rigidbody.MovePosition` or applies forces inside `Update()`, causing massive visual stutter and non-deterministic physics.
    - **Constraint**: ALL `Rigidbody` and `Rigidbody2D` manipulations (forces, velocity, `MovePosition`) MUST reside strictly in `FixedUpdate` (or job-based physics callbacks). Direct modification of `transform.position` on physics-driven objects is FORBIDDEN during the gameplay loop.

18. **The MonoBehaviour Monolith (POCO Separation)**
    - **Rule**: `MonoBehaviour` is a bridge to the Unity C++ layer, not a place to write 1000 lines of business logic.
    - **Violation**: AI stuffs inventory, health, math, and saving into a single `Player.cs` MonoBehaviour.
    - **Constraint**: `MonoBehaviour` scripts must ONLY handle presentation, Unity event binds, and input capture. All calculations, state management, and gameplay logic must be written in clean C# POCOs (Plain Old C# Objects) or ECS structs to remain unit-testable.
