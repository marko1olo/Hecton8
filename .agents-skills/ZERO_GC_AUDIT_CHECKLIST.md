# Zero-GC Audit Checklist

> **Source:** `OPT_Zero_GC_Policy_AllocFree_Mandate.txt` (Technical Mandate #6)
> **GC Budget:** `0 BYTES` for all `Tick` / `FixedTick` paths and every method on the hot-path call stack.
> **Authority:** Principal System Architect / CTO. Build-blocking. No exceptions without written CTO waiver.

**Hot-path scope:** `Tick()`, `FixedTick()`, `Update()`, `FixedUpdate()`, `LateFrameTick`, and any method they directly or transitively call.

---

## 1. FORBIDDEN — Allocation Patterns in Hot Paths

Mark a PR **`BLOCKED: ZERO_GC_VIOLATION`** for any of these inside hot-path scope.

### 1.1 LINQ — BANNED (P0)
Search keywords: `.Where(`, `.Select(`, `.ToList()`, `.Sum()`, `.First(`, `.FirstOrDefault(`, `.Any(`, `.All(`, `.OrderBy(`, `.Count(`, `.GroupBy(`, `.Single(`, `.Min(`, `.Max(`, `.Aggregate(`, `.ForEach(`, `.Cast<`, `.OfType<`.
- No exceptions. Not "just one query", not "it's small".

### 1.2 `foreach` on Dictionary / Interface / `IEnumerable<T>` — BANNED (P1)
Search: `foreach (var kvp`, `IEnumerable<`, `IEnumerator<`, `foreach` inside hot-path method, `GetEnumerator()`.
- `IEnumerable<T>` boxing hazard: value type behind interface = heap wrapper.
- Rule: *"If you cannot iterate with `for(int i...)`, the data structure is wrong. Fix the data structure, not the loop."*

### 1.3 `new` on Class Instances — BANNED (P0)
Search: `new <ClassName>(`, `new List<`, `new Dictionary<`, `new HashSet<`, `new Queue<`, `new Vector3[` in hot path.
- `new` on **struct** is fine. `new` on **class** in hot path is not.
- Must use object pool: `.Rent()` / `.Return()`.

### 1.4 String Concatenation / Interpolation / `.ToString()` — BANNED (P0)
Search: `$"`, `"..." + `, `string.Format(`, `string.Concat(`, `.ToString(`, `.ToString("`, `.ToLower()`, `.ToUpper()`, `new string(`, `string.Create(`, `StringBuilder` in hot path.
- Exception: `.ToString()` allowed in `Awake`/`Start`/`OnEnable`/`OnDisable`, Editor, or `#if UNITY_EDITOR` / `Debug.isDebugBuild`-guarded logging.

### 1.5 Lambdas / Closures / Delegates Created Inline — BANNED (P1)
Search: `=>` (lambda) in hot-path methods, `StartCoroutine(`, `yield return`, `async`, `await`, `Func<`, `Action<`, `Predicate<` assigned inline.
- Each inline lambda allocates a delegate; capturing locals/`this` also allocates a closure.
- Delegates must be pre-cached as fields, initialized in `Awake()`, pointing to named methods.

### 1.6 Unity Find / Reflection APIs — BANNED at Runtime
Search: `FindObjectOfType`, `FindObjectsOfTypeAll`, `FindObjectsOfType`, `GameObject.Find`, `GetComponentsInChildren`, `GetComponentInChildren`, `GetComponentsInParent`, `Resources.`, `GetType()`, `Type.GetType`, `typeof` in hot path, `Activator.CreateInstance`.
- Must use `Registry<T>` instead. Architecture is incomplete if object isn't registered.

### 1.7 Allocating Physics APIs — BANNED (P1)
Search: `Physics.RaycastAll`, `Physics.OverlapSphere`, `Physics.OverlapBox`, `Physics.OverlapCapsule`, `Physics2D.RaycastAll`, `Physics2D.OverlapCircleAll`, `Physics2D.OverlapAreaAll`, `GetContacts` (allocating overload).
- Must use `*NonAlloc` variants into pre-allocated buffers.

### 1.8 Boxing — BANNED
Search: assignment of struct to interface, `(object)`, `object`, `ArrayList`, `Hashtable`, `IList`, `ICollection`, struct passed as `object`.
- Search especially: struct assigned to interface variable, struct passed to `object` parameter.

### 1.9 Coroutines / Iterators in Hot Path — BANNED
Search: `IEnumerator`, `yield return`, `StartCoroutine` in per-frame logic.
- Use explicit `switch` / enum-driven state machines.

---

## 2. REQUIRED — Patterns

### 2.1 Manual Indexed `for` Loop (only approved hot-path iteration)
```csharp
for (int i = 0; i < _count; i++) { _items[i].DoWork(); }
```

### 2.2 Collection Type Priority
| Priority | Type | Notes |
|----------|------|-------|
| 1st | `NativeArray<T>` / `NativeList<T>` | Unmanaged, Burst-compatible, zero GC |
| 2nd | `T[]` (raw array) | Stack-indexed, no GC |
| 3rd | `List<T>` by index | `list[i]` — no boxing |
| 4th | `Registry<T>.GetAt(i)` | O(1) |
| BANNED | `IEnumerable<T>` / `Dictionary<K,V>` iteration | Boxing |

### 2.3 Object Pooling
Search (required): `.Rent()`, `.Return()`, `ObjectPool<`, `pool.Rent`, `pool.Release`.
- All reusable class instances must come from a pool, reset via `.Init(...)`, returned on completion.

### 2.4 `Registry<T>` Pattern
Search (required): `Registry<T>.Register`, `.Unregister`, `.GetAt(`, `.Count`.
- O(1) swap-with-last removal. Iterate with `for(i<Count)`. Never cache array reference across frames (resize invalidates).

### 2.5 Pre-Allocated Buffers
- Static/instance buffers sized for worst-case + 25% margin.
- All buffers allocated at init, never resized mid-frame.

### 2.6 NonAlloc Physics API (required replacements)
| BANNED | REQUIRED |
|--------|----------|
| `Physics.RaycastAll` | `Physics.RaycastNonAlloc` |
| `Physics.OverlapSphere` | `Physics.OverlapSphereNonAlloc` |
| `Physics.OverlapBox` | `Physics.OverlapBoxNonAlloc` |
| `Physics.OverlapCapsule` | `Physics.OverlapCapsuleNonAlloc` |
| `Physics2D.RaycastAll` | `Physics2D.RaycastNonAlloc` |
| `Physics2D.OverlapCircleAll` | `Physics2D.OverlapCircleNonAlloc` |
| `Physics2D.OverlapAreaAll` | `Physics2D.OverlapAreaNonAlloc` |
| `GetContacts` (alloc) | `GetContacts(ContactPoint[], int)` |

### 2.7 Delegate Caching (required pattern)
- Delegates cached as fields, assigned in `Awake()`, pointing to named methods (not lambdas).

### 2.8 String-Free HUD (required pattern)
- Use `StringUtils.IntToBuffer(int, char[], int)` → pre-allocated `char[]` → `TMP_Text.SetText(char[], int, int)`.
- Never `label.text = X.ToString()` per frame.
- `Span<char>` is default formatting surface; use `value.TryFormat(span, out written, format)` into preallocated buffer.

### 2.9 GlobalDataVault Native Sovereignty (required for all NativeArrays)
- All `NativeArray<T>` / `NativeList<T>` / `NativeHashMap<TK,TV>` / `NativeParallelHashMap` / `NativeQueue<T>` must come from GlobalDataVault handle (owner id, capacity, generation, lifetime, disposal path). Local instantiation banned.
- Resize via vault owner. No stale aliases after relocation/generation mismatch.

---

## 3. Struct Layout Requirements

### 3.1 Identity Rule
> If a data container does not require object identity, reference sharing, or inheritance, it **MUST** be `struct`.

Categories that MUST be structs: `DamageEvent`, `PhysicsState`, `BulletData`, `AbilityParameters`, and similar pure-data records.

### 3.2 Decision Flowchart
1. Needs shared mutable state by reference? → **class**
2. Stored in Unity component system (MonoBehaviour/ScriptableObject)? → **class**
3. Needs virtual dispatch/polymorphism? → class only if identity required; else interface + struct impl
4. Holds data + logic on that data? → **MUST be struct**
5. Else → default to struct.

### 3.3 Struct Safety Rules
- **Keep structs under 64 bytes total** — split oversized data.
- Pass large structs with `ref` / `in`: `void Process(in BulletData b)`.
- Never store mutable structs in `readonly` fields (defensive copies).
- Never box struct by assigning to interface — use generic constraints instead:
  ```csharp
  void Process<T>(ref T state) where T : struct, IPhysicsProcessable { ... }
  ```
- Structs in `List<T>` are safe (indexed access doesn't box).
- Structs behind `IMyInterface` are boxed.

*Note: the mandate does not mandate explicit `[StructLayout]` or manual field ordering/alignment in this version; the constraint expressed is the **<64-byte size ceiling** and pass-by-`ref`/`in`. Treat size ceiling and copy cost as the alignment-relevant rules.*

---

## 4. Banned vs Required API Quick Reference

### Banned in hot path
`LINQ`, `new Class`, `string` alloc, `.ToString()`, `$"..."`, `string.Format`, `new string(`, `string.Create(`, `foreach` on Dict/interface, `IEnumerable<T>` params, `FindObjectOfType*`, `GameObject.Find`, `Resources.`, `GetComponentsInChildren`, allocating Physics `*All`/`Overlap*`, `StartCoroutine`, `yield return`, `IEnumerator`, inline lambda/closure, `new NativeArray(...Allocator.Persistent/TempJob)` outside DataVault, boxing to interface/object, reflection in runtime.

### Required
`for(int i...)` loops, `NativeArray`/`T[]`/indexed `List<T>`, `Registry<T>`, object pools (`.Rent()`/`.Return()`), pre-allocated buffers, `*NonAlloc` physics, cached named-method delegates, `IntToBuffer` + `char[]` + `TMP.SetText(char[],int,int)`, `Span<char>`/`TryFormat` into prealloc buffers, `struct` for pure data, `ref`/`in` params, enum/switch state machines, `FixedString32/64/128Bytes` in Burst jobs.

### Native/Span rules
- `[FORBID]` `new string(span)` / `string.Create(...)` in hot paths.
- `[FORBID]` storing `Span<T>` in fields, captured lambdas, async, iterator methods, or job structs.
- `[FORBID]` `stackalloc` above 256 bytes in gameplay frame paths (use `CharBufferPool` / `HectonArenaAllocator`).
- `[SAFE]` `Span<T>` wrapping persistent `char[]` / pooled buffer lease / frame-local arena allocation.
- `[SAFE]` Burst jobs use `FixedString32/64/128Bytes` — not `Span<char>`.

---

## 5. "Cold Allocation" (Permitted) vs Violation

**Permitted / relaxed zones (cold):** `Awake()`, `Start()`, `OnEnable()`/`OnDisable()` (registration only), scene load/unload, `#if UNITY_EDITOR`, `Debug.isDebugBuild`-guarded logging, cutscene/cinematic (document), asset loading callbacks, network deserialization (isolated).

**Everything else is hot path → default to zero.**

**Known-allocation-site waivers:** unavoidable engine/third-party allocations must be logged in a governance-approved ledger (owner, why it can't be eliminated, frame phase, approver name). CTO approval required. *No nonexistent global waiver file may be cited.*

---

## Appendix A — Violation Severity Table

| Violation | Severity | SLA |
|-----------|----------|-----|
| LINQ in Tick/FixedTick | P0 Build Block | Fix before merge |
| `new class` in Tick | P0 Build Block | Fix before merge |
| String alloc in HUD per-frame | P0 Build Block | Fix before merge |
| `foreach` on Dictionary hot path | P1 Must Fix | Same sprint |
| Missing NonAlloc physics | P1 Must Fix | Same sprint |
| Uncached lambda hot path | P1 Must Fix | Same sprint |
| Using Find instead of Registry | P1 Must Fix | Same sprint |
| Struct candidate as class | P2 Required Fix | Within 2 sprints |
| Missing profiler screenshot on hot-path PR | P1 PR Blocked | Attach before review |

---

## Appendix B — Performance Acceptance Criteria

| Metric | Target | Hard Limit |
|--------|--------|------------|
| GC Alloc per frame (gameplay) | 0 B | 0 B |
| GC Alloc per frame (UI) | 0 B | 0 B |
| Physics query alloc per frame | 0 B | 0 B |
| Frame headroom after hot-path | ≥2 ms (MX350) | 1 ms min |
| GC collections / 60 s gameplay | 0 | 0 |

---

## Appendix C — Automated Enforcement

- **`HectonGCAnalyzer`** (Roslyn): flags LINQ, `foreach` on Dictionary/Interface, `new` class in `Tick`/`FixedTick`, string interpolation in hot-path methods. Build-failing.
- **Memory Profiler Gate:** nightly runs on MX350; >0 B in gameplay hot paths during 60-s stress = fail.
- **PR policy:** files under `Runtime/Systems/` or `Runtime/Combat/` require `gc-reviewed` label.
- **Dev workflow:** 300 frames @ 0 B GC Alloc in `Tick`/`FixedTick`, profiler screenshot attached to PR (auto-reject if missing).
