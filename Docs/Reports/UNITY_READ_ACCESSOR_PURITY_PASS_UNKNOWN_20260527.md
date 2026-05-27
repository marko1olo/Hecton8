# UNKNOWN Read Accessor Purity Pass - 2026-05-27

Status: SOURCE PATCHED, STATIC PROOF PASSED, BUILD BOUNDARY HIT, DOC GATES PASSED
Agent: UNKNOWN
Evidence class: STATIC_SOURCE / CLI_BUILD_BOUNDARY / DOC_VALIDATION

## Problem

Three clean runtime files still had read-shaped APIs hiding mutable or allocating work.

- `PerformanceBudgetController.GetBudgetStatus()` allocated a new `Dictionary` on every call.
- `WorldShippingContentFilter.GetSuppressedHierarchyIds(..., createIfMissing:true)` mixed read lookup and cache creation behind a `Get*` name.
- `RTLProcessor.GetBuffer()` could lazily allocate a thread-local `char[]` behind a `Get*` name.

This violates the active accessor doctrine:

- `Get*`, `TryGet*`, `Resolve*`, and `Read*` accessors must not allocate or grow buffers.
- Read accessors must not hide ownership creation behind a read name.
- Hot and diagnostic readers should use owner-owned snapshots or caller-owned output buffers.

First-20-minutes route impact: this removes hidden managed allocation and ambiguous cache creation from runtime diagnostics and scene shipping filters used during boot/world object suppression.

## Changed Files

| File | Change |
|---|---|
| `Assets/_Project/Scripts/Tools/PerformanceBudgetController.cs` | Added a fixed-capacity owner snapshot dictionary and made `GetBudgetStatus()` reuse it. Kept `CopyBudgetStatusNonAlloc()` as the preferred hot/read-retained API. |
| `Assets/_Project/Scripts/World/WorldShippingContentFilter.cs` | Split pure lookup into `TryGetSuppressedHierarchyIds()` and mutating creation into `EnsureSuppressedHierarchyIds()`. |
| `Assets/_Project/Scripts/RTLProcessor.cs` | Renamed lazy staging-buffer route from `GetBuffer()` to explicit `EnsureBuffer()`. |

## Rejected Alternatives

- Removing `GetBudgetStatus()` was rejected because it is public API and no compile-wide proof exists for external callers.
- Keeping the per-call dictionary allocation was rejected because the class already exposes a non-alloc copy API.
- Leaving `GetSuppressedHierarchyIds(..., createIfMissing)` was rejected because a boolean flag made the read route secretly mutating.
- Converting scene suppression storage to native containers was rejected because the cache stores Unity object-derived `EntityId` membership and is built in cold scene-filter setup.
- Prewarming every possible RTL thread-local buffer was rejected because there is no proven multi-threaded localization caller surface; the correct fix here is naming the lazy allocation route honestly.

## Static Proof

Touched-file accessor scanner:

```text
touched_accessor_alloc_or_forbidden_hits=0
```

Touched-file exact hot-method scanner:

```text
touched_exact_hot_hits=0
```

Brace proof:

```text
PerformanceBudgetController.cs braceBalance=0 lines=655
WorldShippingContentFilter.cs braceBalance=0 lines=335
RTLProcessor.cs braceBalance=0 lines=81
```

Targeted route proof:

```text
new Dictionary<string, SystemBudgetInfo> inside GetBudgetStatus: 0
GetSuppressedHierarchyIds residuals: 0
RTLProcessor private static char[] GetBuffer residuals: 0
TryGetSuppressedHierarchyIds call sites: 2
EnsureSuppressedHierarchyIds creation route call sites: 1
EnsureBuffer call sites: 3
```

`git diff --check` on touched source files passed with line-ending warnings only.

## Build Boundary

The build guard legally launched:

```text
attempt=1
cpu=5
compilerProcessCount=0
dotnet build Hecton8.slnx
exit=1
warnings=0
errors=62
```

The failure is the known generated-project boundary:

```text
MSB3202: Unity-generated .csproj files are missing from the checkout.
```

The build did not reach C# source compilation.

Raw proof: `BUILD_UNKNOWN_READ_ACCESSOR_PURITY_RECHECK2_20260527.log`.

## Documentation Validation

Validation after UTF-8 BOM repair:

```text
VerifyDocStructure.py pass=true activeDocCount=669 encodingWithoutUtf8Sig=0
OOP_Doc_Scanner.py finalPass=true activeFileCount=669 sourceSyncPass=true wordReductionPercent=50.82702789653103
```

## Residuals

- No runtime/profiler microseconds are claimed.
- Broad clean runtime exact hot-method scan before this patch reported `0` forbidden rows.
- Broader accessor scan still contains many `GlobalRegistry` cold-resolve patterns and intentional save/read allocations; they were not edited without a narrower proof path.
- Dirty source files were not touched.
