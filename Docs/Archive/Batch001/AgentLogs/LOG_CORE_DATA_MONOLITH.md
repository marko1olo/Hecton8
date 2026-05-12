# LOG_CORE_DATA_MONOLITH

## 2026-05-11 Data Monolith Pipeline
STATUS: PENDING VERIFICATION

What was wrong:
- Static gameplay data was scattered across managed and asset-backed surfaces, which forces object graph reads and makes balancing drift-prone.
- Runtime data access still had avoidable non-constant tails: sparse biome fallback scan and loot table linear edge expansion.
- Core build remains blocked outside Data Monolith ownership.

What was done:
- Built the Data Monolith runtime surface under `Assets/_Project/Scripts/Data/Monolith/`.
- Added `H8StaticDataArena` with 10MB minimum Persistent `NativeArray<byte>` reserve, resident blob byte tracking, 16-byte header validation, XXHash3 payload checksum, and guarded native blit.
- Added exact-size records for items, creatures, recipes, loot CDF, biome heatmap, voxel material atlas, audio hash registry, depth pressure LUT, hull constants, and physics material LUT.
- Added editor compiler under `Assets/_Project/Scripts/Editor/DataMonolith/` reading CSV/JSON from `Assets/_SourceData/` and baking `.h8bin`, with FileSystemWatcher hot reload.
- Added Burst SoA unpack jobs for item and creature records.
- Omega polish removed biome fallback scan and replaced loot range expansion with binary lower/upper bounds.

`H8ItemRecord` layout:
```csharp
[StructLayout(LayoutKind.Sequential, Pack = 1, Size = H8DataLayoutConstants.ItemRecordSize)]
public struct H8ItemRecord
{
    public uint HashId;
    public uint RecordIndex;
    public uint CategoryHash;
    public uint Flags;
    public ushort MaxStack;
    public ushort RecipeIngredientCount;
    public ulong RecipeMask0;
    public ulong RecipeMask1;
    public float MassKg;
    public float VolumeM3;
    public float BaseQuality;
    public float HeatCapacity;
    public uint YieldHash;
    public int NameUtf8Offset;
    public int DescriptionUtf8Offset;
}
```

`UnsafeUtility.MemCpy` logic:
```csharp
byte[] source = File.ReadAllBytes(absolutePath);
byte* destination = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(_arena);
fixed (byte* sourcePtr = source)
{
    UnsafeMemoryCopyGuard.TryMemCpy(destination, _arena.Length, sourcePtr, source.Length);
}
```

Cinematic cheats used:
- Biome lookup: terrain/MapMagic query replaced by dense 256x256 1D LUT index `(y << 8) + x`.
- Depth pressure: runtime pressure formula replaced by 256-sample baked LUT.
- Recipes: ingredient set/string checks replaced by two `ulong` hash bitmasks.
- Loot: float random cumulative work replaced by deterministic integer CDF and binary search.

Scalability matrix:
- Low: 10MB static arena, compact sections, direct hash/LUT reads, integer CDF, zero managed hot-path residency.
- Middle: broader authored tables with the same runtime accessors.
- High: richer audio/voxel/hull/static sections without changing lookup complexity.
- Ultra: larger baked data and downstream visual overkill using the saved CPU budget; data lookup remains direct native memory.

Estimated microseconds saved:
- String ID lookup to uint FNV hash lookup: ~2-8 us per lookup.
- ScriptableObject/object graph item reads to 64B contiguous records: ~10-40 us per section access batch.
- Biome query to direct LUT: ~5-50 us per lookup; Omega removal of fallback scan avoids ~20-100 us worst-case on bad blobs.
- Recipe list/string comparison to `ulong` masks: ~4-20 us per craftability batch.
- Loot linear/float path to integer binary CDF: ~2-15 us per roll; Omega lower/upper bounds avoid ~1-8 us on medium tables.
- Depth pressure formula to LUT: ~0.3-2 us per sample.

Build evidence:
- Command: `dotnet build Hecton8.Core.csproj`
- Result: failed, external dependency.
- Error: `Assets/_Project/Scripts/HectonFloatingOrigin.cs(620,17): CS0103 The name 'PublishAupShiftSignal' does not exist in the current context`.
- Data Monolith runtime audit found no banned `foreach`, LINQ, `string.Format`, interpolated string, `.ToString()`, `math.pow`, `sqrt`, `normalize`, or `Vector3.magnitude` in owned runtime files.

Final Git diff evidence:
- `?? Assets/_Project/Scripts/Data/Monolith/`
- `?? Assets/_Project/Scripts/Editor/DataMonolith/`
- `?? Docs/Tasks/Status_CORE_DATA_MONOLITH.md`
- `?? Docs/AgentLogs/Rationale_CORE_DATA_MONOLITH.md`
- `?? Docs/AgentLogs/LOG_CORE_DATA_MONOLITH.md`
- Shared dirty files outside Data domain remain present in workspace; only compile-compatibility touch was justified and logged.
