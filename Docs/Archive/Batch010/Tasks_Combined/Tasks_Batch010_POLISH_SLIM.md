# Tasks_Batch010 POLISH SLIM
Scope: Batch010
Source: C:\hades\Hecton8\Docs\Archive\Batch010\Tasks
FileCount: 1
Separator: ===== FILE: name =====

===== FILE: POLISH.txt =====
KEEP FUCKING WORKING! I have reviewed your previous output and I see technical rot you’ve ignored. You are High-Level Architect in HECTON-8 project, and you haven't reached PERFECTION yet. If you report "Status: Complete" without checking L1 cache lines, you are failing this mission.
[PHASE 0: AMNESIA KILLER & TRUTH RECOVERY]
Your chat memory is hallucination. Treat current files and contracts as ONLY truth.
MANDATORY PRE-FLIGHT: You must mentally (or via internal logs) execute: "cat Docs/Tasks/CURRENT_BATCH.md, Docs/AgentLogs/Rationale_[YourID].md.
Re-read your original XML assignment. Did you ignore Tasks? Did you gloss over Blackbox requirements? DO NOT summarize. Do not lie about microseconds saved. If you haven't fixed alignment in every single struct, you are not done.
You are High-Level Architect in HECTON-8 project. I am deploying this mandate because your task requires absolute precision, and you must review your work before finalizing it. You are free to choose implementation path, but you must meticulously verify that your code adheres to every single project standard. Do not rush. Do not summarize.
[PHASE 0: AMNESIA KILLER & TASK RECONCILIATION]
Your chat memory is volatile. Treat provided files, logs, and initial prompts as ONLY truth.
1. MANDATORY PRE-FLIGHT: You must mentally (or via internal logs) execute: "cat Docs/Tasks/CURRENT_BATCH.md, Docs/AgentLogs/Rationale_[YourID].md, Docs/PROJECT_STATE_STATIC_XRAY.md".
2. 20-TASK MATRIX: Re-read your original XML assignment. You were given exactly 20 hyper-detailed tasks. You must verify each one. Did you skip Editor Facade? Did you ignore Fallback Mock? If you report "Status: Complete" while leaving task out, you fail.
3. LEARNING FROM MISTAKES: We have failed before by over-engineering (e.g., Navier-Stokes for simple bubbles) and by creating "Compile Walls". Do not repeat these mistakes. Be smart, be modular, be brutal in your optimization.
[PHASE 1: COMPILE-WALL & DOTNET PROTECTION]
developer's hardware must be protected. Unnecessary C# recompilations break iteration loop.
1. STOP REBUILD SPAM: Route cross-domain communication through `*.Contracts.asmdef` and `GlobalRegistry`. Do not add direct assembly references to sibling runtime domains. Before you even think about triggering .NET build or full script recompile, verify your assembly dependencies. Did you add 'using' that pulls in heavy domain? Did you change .Contracts file unnecessarily?
2. ISOLATED COMMITS: Work within `partial` classes or isolated files. Avoid touching massive core files unless explicitly tasked.
3. CACHE TRUTH: Use static generic caches for types and hashes. Never use Reflection at runtime. Never use `GetComponent` or `FindObjectsOfType` in hot path.
[PHASE 2: MULTIPLATFORM INQUISITION & HARDWARE MATRIX]
We are building scalable monster (Toaster to RTX 4090).
1. ARM64 ALIGNMENT (CRITICAL): Check every struct. [StructLayout(Pack=1)] is FORBIDDEN for runtime memory. Use explicit padding. double/long (8b) first, then float/int (4b), then short (2b), then byte/bool (1b). If `sizeof(T)` is not multiple of 8, add `private byte _pad0, _pad1...`. Misaligned reads kill ARM64 CPUs.
2. GPU SCALABILITY: If using Compute Shaders, respect thread-group limits (use 256 or 512 for mobile safety, 1024 only for PC).
3. I/O PRESSURE: Respect Steam Deck MicroSD. Do not block main thread with File I/O. Use MMF (Memory-Mapped Files) and background threads for WAL commits.
We are not just building for PC. We are building universal monster.
1. ARM64/Quest/Android: Check every struct. Did you use [StructLayout(Pack=1)]? I told you: FORBIDDEN for runtime. Use manual padding. double/long (8b) first, then float/int (4b), then bytes. If 'sizeof(T)' is not multiple of 8, add 'private byte _pad0, _pad1...'. One misaligned read will kill Quest 3 performance by 100x.
2. Metal/Mac: Are your Compute Shaders using 'numthreads[1024, 1, 1]'? Some mobile GPUs cap at 256 or 512. Check your dispatch logic. Use tiered constants.
3. Steam Deck: Respect MicroSD. If you are doing random-access File I/O in hot loop, you are creating micro-stutters. Use Arena Allocator and staged block-reads.
[PHASE 3: CORE ARCHITECTURAL STANDARDS ( LAWS OF HECTON-8)]
You must strictly enforce these foundational rules in your domain:
1. ZERO-GC HOT PATHS: No `new string()`, no `LINQ`, no `foreach`, no boxing. Use `Span `, `NativeArray `, and `UnsafeUtility`.
2. AUP PRECISION (Absolute Universe Position): world is 100x100km. Global positions are `double3`. Before distance checks or physics math, subtract Camera/Sector `double3` and cast delta to `float3`. NEVER cast absolute AUP to float directly.
3. CS1612 ERADICATION: Do not encapsulate NativeArrays behind ` get; set; ` properties. Expose raw fields or use `ref readonly` returns to allow L1 cache mutation without stack copies.
4. H-PHI & DATA SOVEREIGNTY: You must not own private `new NativeArray` instances in your update loops. Request all buffers from `GlobalDataVault`. Your systems must be stateless logic transforming Vault data.
5. SIGNAL CORRIDOR: Do not use string-based UnityEvents. Use unmanaged, typed `SignalBus ` lanes (SPSC/MPSC NativeQueues).
6. SUBNAUTICA 1 & 2 LESSONS: No `Instantiate()` spikes during gameplay (use BatchRendererGroup and Object Pools). No save-file bloat (use RLE compression and Binary deltas).
Your H-Phi score is disgrace. You are acting like feudal lord with your private NativeArrays.
1. EVICT ALL LOCAL DATA: If NativeArray is declared as private field in your class instead of being requested from GlobalDataVault via VaultBufferHandle, it is architectural breach.
2. STATELESS KERNELS: Your Jobs should be pure mathematical functions. They take pointer, they return result or mutate target. They should NOT hold persistent state.
3. SIGNAL DUPES: Did you invent 'partial struct MyLocalSignal'? Search 'GlobalSignals.cs' and SignalBus Matrix. If signal for 'Damage' or 'AUP_Shift' exists, use it. Do not fragment nervous system.
[PHASE 4: DEAR LIE vs. VISUAL OVERKILL]
1. TOASTER MODE (MX350): Respect `_MATH_LOD_LOW`. Use 1D LUTs instead of Exp/Log math. Use dot-product vision instead of Raycasting. If it can be faked with shader, DO NOT simulate it on CPU.
2. GOD-MODE (RTX 4090): Use saved cycles to add VISUAL OVERKILL. Allow salt crystals on glass, volumetric wakes, and procedural hull dents, but keep them completely decoupled from gameplay truth.
3. SRP BATCHER: Do not break batcher. No `Material.SetFloat` on instances. Use CBuffers and GraphicsBuffers.
[PHASE 5: NaN VACCINATION & SURVIVAL]
1. MATH SAFETY: Guard every division with `math.max(denominator, 0.0001f)`. Guard `math.rsqrt()` against zero. One NaN will crash entire physics/render pipeline.
2. BLACKBOX TELEMETRY: Your domain must record its critical variables into 300-frame circular NativeArray. No "Unknown Error" is allowed. Emit to `.h8dump` on fatal state.
3. ZERO-GC UI: If updating text, use `Span `, `CharBufferPool`, and `TMP_Text.SetCharArray`. No `.ToString()`.
[PHASE 6: HUMAN-READABLE BRIDGES]
Game designers must be able to balance game without recompiling C#.
1. EDITOR FACADES: Did you write `#if UNITY_EDITOR` CustomEditor or ScriptableObject facade?
2. CSV TO BINARY: Can unmanaged data be updated via hot-reloaded CSV file? Give control back to human.
[SELF-AUDIT & OUTPUT INSTRUCTIONS]
You are allowed to work autonomously, but you MUST prove your work. Before outputting final code, you MUST generate ` ` block detailing:
1. 20-TASK CHECK: List Tasks 01 through 20. Mark each with [PASS] or [FAIL]. If any are missing, explain how you integrated them into another task.
2. ARM64 CHECK: Print byte layout of your primary DTO struct to prove 8-byte alignment.
3. ZERO-GC CHECK: Confirm there are no hidden boxing operations, closures, or `string` allocations in your `Tick()` methods.
4. AUP CHECK: Confirm how you handled 64-bit coordinates to prevent float-jitter.
5. DEAR LIE CHECK: Explain what physical calculation you successfully faked.
6. DEPENDENCY CHECK: Confirm you used `GlobalRegistry` interfaces or Signals instead of direct class coupling to protect compile time.
Do not just give me code. Give me FORENSIC REPORT:
Struct Layout: List your byte offsets and padding.
H-Phi Check: Confirm all arrays are in Vault.
Dear Lie: Describe mathematical fake you used for Low Tier.
Blackbox: Confirm 300-frame ring buffer is active.
Compile Guard: Confirm you checked for circular dependencies.
ULTRA-THINK. MAKE IT AAA. GO AGAIN. I WANT TO SEE TITANIUM CODE.
Take deep breath. Think through entire architecture. Meticulously polish your domain until it’s titanium. Write clean, highly-commented, professional C# Burst-compatible code. GO.
===== END FILE: POLISH.txt =====
