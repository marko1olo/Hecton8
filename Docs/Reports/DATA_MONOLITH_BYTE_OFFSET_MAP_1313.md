# DATA MONOLITH BYTE OFFSET MAP - 1313

Source: `Assets/_Project/Scripts/Data/Monolith/H8DataMonolithTypes.cs`

Policy: explicit layout, natural alignment, struct size multiple of 8. Strict 8-byte-first field-order policy is reported separately because header ABI may require compatibility-preserving migration.

## H8DataBlobHeader - FAIL
- Lines: layout 127, struct 128
- Declared size: 64 (`H8DataLayoutConstants.HeaderSizeBytes`), multipleOf8=True
- Violation: 8-byte field Checksum64 begins at offset 8 after smaller fields; alignment OK but strict field-order policy FAIL
| Offset | Size | Type | Field | Line | Natural Align |
|---:|---:|---|---|---:|---|
| 0 | 4 | `uint` | `Magic` | 131 | True |
| 4 | 2 | `ushort` | `FormatVersion` | 134 | True |
| 6 | 2 | `ushort` | `HeaderBytes` | 137 | True |
| 8 | 8 | `ulong` | `Checksum64` | 140 | True |
| 16 | 4 | `uint` | `BlobBytes` | 143 | True |
| 20 | 4 | `uint` | `DirectoryOffset` | 146 | True |
| 24 | 4 | `uint` | `DirectoryBytes` | 149 | True |
| 28 | 4 | `uint` | `SectionTableOffset` | 152 | True |
| 32 | 4 | `uint` | `SectionCount` | 155 | True |
| 36 | 4 | `uint` | `Flags` | 158 | True |
| 40 | 4 | `uint` | `WorldSeed` | 161 | True |
| 44 | 4 | `uint` | `AppVersionHash` | 164 | True |
| 48 | 4 | `uint` | `SchemaHash` | 167 | True |
| 52 | 4 | `uint` | `Reserved0` | 169 | True |
| 56 | 4 | `uint` | `Reserved1` | 170 | True |
| 60 | 4 | `uint` | `Reserved2` | 171 | True |

## H8DataBlobDirectory - PASS
- Lines: layout 177, struct 178
- Declared size: 64 (`H8DataLayoutConstants.DirectorySizeBytes`), multipleOf8=True
| Offset | Size | Type | Field | Line | Natural Align |
|---:|---:|---|---|---:|---|
| 0 | 4 | `uint` | `Magic` | 180 | True |
| 4 | 2 | `ushort` | `FormatVersion` | 181 | True |
| 6 | 2 | `ushort` | `SectionCount` | 182 | True |
| 8 | 4 | `uint` | `SectionTableOffset` | 183 | True |
| 12 | 4 | `uint` | `SectionTableBytes` | 184 | True |
| 16 | 4 | `uint` | `BlobBytes` | 185 | True |
| 20 | 4 | `uint` | `DataStartOffset` | 186 | True |
| 24 | 4 | `uint` | `LocalizationOffset` | 187 | True |
| 28 | 4 | `uint` | `LocalizationBytes` | 188 | True |
| 32 | 4 | `uint` | `Flags` | 189 | True |
| 36 | 4 | `uint` | `WorldSeed` | 190 | True |
| 40 | 4 | `uint` | `AppVersionHash` | 191 | True |
| 44 | 4 | `uint` | `Reserved0` | 192 | True |
| 48 | 4 | `uint` | `Reserved1` | 193 | True |
| 52 | 4 | `uint` | `Reserved2` | 194 | True |
| 56 | 4 | `uint` | `Reserved3` | 195 | True |
| 60 | 4 | `uint` | `Reserved4` | 196 | True |

## H8DataSectionEntry - PASS
- Lines: layout 202, struct 203
- Declared size: 16 (`16`), multipleOf8=True
| Offset | Size | Type | Field | Line | Natural Align |
|---:|---:|---|---|---:|---|
| 0 | 4 | `uint` | `SectionId` | 205 | True |
| 4 | 4 | `uint` | `RecordSize` | 206 | True |
| 8 | 4 | `uint` | `Count` | 207 | True |
| 12 | 4 | `uint` | `OffsetBytes` | 208 | True |

## H8ItemRecord - PASS
- Lines: layout 214, struct 215
- Declared size: 80 (`80`), multipleOf8=True
| Offset | Size | Type | Field | Line | Natural Align |
|---:|---:|---|---|---:|---|
| 0 | 8 | `ulong` | `RecipeMask0` | 217 | True |
| 8 | 8 | `ulong` | `RecipeMask1` | 218 | True |
| 16 | 4 | `uint` | `HashId` | 219 | True |
| 20 | 4 | `uint` | `RecordIndex` | 220 | True |
| 24 | 4 | `uint` | `CategoryHash` | 221 | True |
| 28 | 4 | `uint` | `Flags` | 222 | True |
| 32 | 4 | `float` | `MassKg` | 223 | True |
| 36 | 4 | `float` | `VolumeM3` | 224 | True |
| 40 | 4 | `float` | `BaseQuality` | 225 | True |
| 44 | 4 | `float` | `HeatCapacity` | 226 | True |
| 48 | 4 | `uint` | `YieldHash` | 227 | True |
| 52 | 4 | `uint` | `NameUtf8Offset` | 228 | True |
| 56 | 4 | `uint` | `DescriptionUtf8Offset` | 229 | True |
| 60 | 4 | `uint` | `NameUtf8ByteLength` | 230 | True |
| 64 | 4 | `uint` | `DescriptionUtf8ByteLength` | 231 | True |
| 68 | 4 | `uint` | `Cost` | 232 | True |
| 72 | 4 | `float` | `AccessFrequency` | 233 | True |
| 76 | 2 | `ushort` | `MaxStack` | 234 | True |
| 78 | 2 | `ushort` | `RecipeIngredientCount` | 235 | True |

## H8CreatureGenomeTraitBlock - PASS
- Lines: layout 241, struct 242
- Declared size: 32 (`H8DataLayoutConstants.CreatureGenomeTraitBlockSize`), multipleOf8=True
| Offset | Size | Type | Field | Line | Natural Align |
|---:|---:|---|---|---:|---|
| 0 | 4 | `float` | `Aggression` | 244 | True |
| 4 | 4 | `float` | `Metabolism` | 245 | True |
| 8 | 4 | `float` | `MaxHealth` | 246 | True |
| 12 | 4 | `float` | `CruiseSpeed` | 247 | True |
| 16 | 4 | `float` | `BurstSpeed` | 248 | True |
| 20 | 4 | `float` | `SpawnCreditCost` | 249 | True |
| 24 | 4 | `float` | `PressureMinMeters` | 250 | True |
| 28 | 4 | `float` | `PressureMaxMeters` | 251 | True |

## H8CreatureTraitRecord - PASS
- Lines: layout 257, struct 258
- Declared size: 64 (`H8DataLayoutConstants.CreatureTraitRecordSize`), multipleOf8=True
| Offset | Size | Type | Field | Line | Natural Align |
|---:|---:|---|---|---:|---|
| 0 | 4 | `uint` | `SpeciesHash` | 260 | True |
| 4 | 4 | `uint` | `RecordIndex` | 261 | True |
| 8 | 4 | `uint` | `MateMask` | 262 | True |
| 12 | 4 | `uint` | `BiomeMask` | 263 | True |
| 16 | 32 | `H8CreatureGenomeTraitBlock` | `Genome` | 264 | True |
| 48 | 4 | `uint` | `DisplayNameUtf8Offset` | 265 | True |
| 52 | 4 | `uint` | `LootTableHash` | 266 | True |
| 56 | 4 | `uint` | `Flags` | 267 | True |
| 60 | 4 | `uint` | `DisplayNameUtf8ByteLength` | 268 | True |

## H8BiomeRecord - PASS
- Lines: layout 274, struct 275
- Declared size: 64 (`H8DataLayoutConstants.BiomeRecordSize`), multipleOf8=True
| Offset | Size | Type | Field | Line | Natural Align |
|---:|---:|---|---|---:|---|
| 0 | 4 | `uint` | `BiomeHash` | 277 | True |
| 4 | 4 | `uint` | `RecordIndex` | 278 | True |
| 8 | 4 | `uint` | `Flags` | 279 | True |
| 12 | 4 | `uint` | `SurfaceId` | 280 | True |
| 16 | 4 | `float` | `MinDepthMeters` | 281 | True |
| 20 | 4 | `float` | `MaxDepthMeters` | 282 | True |
| 24 | 4 | `float` | `TemperatureCelsius` | 283 | True |
| 28 | 4 | `float` | `PressureScalar` | 284 | True |
| 32 | 4 | `float` | `FogDensity` | 285 | True |
| 36 | 4 | `float` | `LightScatterR` | 286 | True |
| 40 | 4 | `float` | `LightScatterG` | 287 | True |
| 44 | 4 | `float` | `LightScatterB` | 288 | True |
| 48 | 4 | `uint` | `DisplayNameUtf8Offset` | 289 | True |
| 52 | 4 | `uint` | `HeatmapId` | 290 | True |
| 56 | 4 | `uint` | `RadiationFieldHash` | 291 | True |
| 60 | 4 | `uint` | `DisplayNameUtf8ByteLength` | 292 | True |

## H8RecipeRecord - PASS
- Lines: layout 295, struct 296
- Declared size: 64 (`64`), multipleOf8=True
| Offset | Size | Type | Field | Line | Natural Align |
|---:|---:|---|---|---:|---|
| 0 | 8 | `ulong` | `IngredientMask0` | 298 | True |
| 8 | 8 | `ulong` | `IngredientMask1` | 299 | True |
| 16 | 4 | `uint` | `OutputHash` | 300 | True |
| 20 | 4 | `uint` | `StationHash` | 301 | True |
| 24 | 4 | `uint` | `Flags` | 302 | True |
| 28 | 4 | `uint` | `IngredientCount` | 303 | True |
| 32 | 4 | `uint` | `IngredientHash0` | 304 | True |
| 36 | 4 | `uint` | `IngredientHash1` | 305 | True |
| 40 | 4 | `uint` | `IngredientHash2` | 306 | True |
| 44 | 4 | `uint` | `IngredientHash3` | 307 | True |
| 48 | 4 | `float` | `CraftSeconds` | 308 | True |
| 52 | 4 | `uint` | `OutputCount` | 309 | True |
| 56 | 4 | `uint` | `Reserved0` | 310 | True |
| 60 | 4 | `uint` | `Reserved1` | 311 | True |

## H8BiomeHeatmapCellRecord - PASS
- Lines: layout 314, struct 315
- Declared size: 16 (`16`), multipleOf8=True
| Offset | Size | Type | Field | Line | Natural Align |
|---:|---:|---|---|---:|---|
| 0 | 4 | `uint` | `BiomeHash` | 317 | True |
| 4 | 4 | `uint` | `Reserved0` | 318 | True |
| 8 | 4 | `uint` | `Reserved1` | 319 | True |
| 12 | 2 | `ushort` | `X` | 320 | True |
| 14 | 2 | `ushort` | `Y` | 321 | True |

## H8QuestNodeRecord - PASS
- Lines: layout 324, struct 325
- Declared size: 32 (`32`), multipleOf8=True
| Offset | Size | Type | Field | Line | Natural Align |
|---:|---:|---|---|---:|---|
| 0 | 4 | `uint` | `NodeHash` | 327 | True |
| 4 | 4 | `uint` | `CompletionFlagId` | 328 | True |
| 8 | 4 | `uint` | `FirstEdgeIndex` | 329 | True |
| 12 | 4 | `uint` | `RequiredMask0` | 330 | True |
| 16 | 4 | `uint` | `RequiredMask1` | 331 | True |
| 20 | 4 | `uint` | `RequiredMask2` | 332 | True |
| 24 | 4 | `uint` | `RequiredMask3` | 333 | True |
| 28 | 2 | `ushort` | `EdgeCount` | 334 | True |
| 30 | 2 | `ushort` | `NodeType` | 335 | True |

## H8QuestEdgeRecord - PASS
- Lines: layout 338, struct 339
- Declared size: 16 (`16`), multipleOf8=True
| Offset | Size | Type | Field | Line | Natural Align |
|---:|---:|---|---|---:|---|
| 0 | 4 | `uint` | `FromNodeHash` | 341 | True |
| 4 | 4 | `uint` | `ToNodeHash` | 342 | True |
| 8 | 4 | `uint` | `GateFlagId` | 343 | True |
| 12 | 4 | `uint` | `Reserved0` | 344 | True |

## H8LootCdfRecord - PASS
- Lines: layout 347, struct 348
- Declared size: 16 (`16`), multipleOf8=True
| Offset | Size | Type | Field | Line | Natural Align |
|---:|---:|---|---|---:|---|
| 0 | 4 | `uint` | `TableHash` | 350 | True |
| 4 | 4 | `uint` | `ItemHash` | 351 | True |
| 8 | 4 | `uint` | `CumulativeWeight` | 352 | True |
| 12 | 4 | `uint` | `TotalWeight` | 353 | True |

## H8VoxelMaterialRecord - PASS
- Lines: layout 356, struct 357
- Declared size: 32 (`32`), multipleOf8=True
| Offset | Size | Type | Field | Line | Natural Align |
|---:|---:|---|---|---:|---|
| 0 | 4 | `uint` | `VoxelHash` | 359 | True |
| 4 | 4 | `uint` | `YieldHash` | 360 | True |
| 8 | 4 | `float` | `Hardness` | 361 | True |
| 12 | 4 | `float` | `MeltingPointCelsius` | 362 | True |
| 16 | 4 | `float` | `Density` | 363 | True |
| 20 | 4 | `uint` | `SurfaceId` | 364 | True |
| 24 | 4 | `uint` | `Flags` | 365 | True |
| 28 | 4 | `uint` | `Reserved0` | 366 | True |

## H8AudioClipRegistryRecord - PASS
- Lines: layout 369, struct 370
- Declared size: 16 (`16`), multipleOf8=True
| Offset | Size | Type | Field | Line | Natural Align |
|---:|---:|---|---|---:|---|
| 0 | 4 | `uint` | `EventHash` | 372 | True |
| 4 | 4 | `uint` | `AddressableKeyUtf8Offset` | 373 | True |
| 8 | 4 | `uint` | `BankHash` | 374 | True |
| 12 | 4 | `uint` | `AddressableKeyUtf8ByteLength` | 375 | True |

## H8VfxScalarRecord - PASS
- Lines: layout 378, struct 379
- Declared size: 32 (`32`), multipleOf8=True
| Offset | Size | Type | Field | Line | Natural Align |
|---:|---:|---|---|---:|---|
| 0 | 4 | `uint` | `EffectHash` | 381 | True |
| 4 | 4 | `float` | `EmissionRate` | 382 | True |
| 8 | 4 | `float` | `ColorR` | 383 | True |
| 12 | 4 | `float` | `ColorG` | 384 | True |
| 16 | 4 | `float` | `ColorB` | 385 | True |
| 20 | 4 | `float` | `ColorA` | 386 | True |
| 24 | 4 | `float` | `Intensity` | 387 | True |
| 28 | 4 | `uint` | `Flags` | 388 | True |

## H8DepthPressureSampleRecord - PASS
- Lines: layout 391, struct 392
- Declared size: 16 (`16`), multipleOf8=True
| Offset | Size | Type | Field | Line | Natural Align |
|---:|---:|---|---|---:|---|
| 0 | 4 | `float` | `DepthMeters` | 394 | True |
| 4 | 4 | `float` | `PressureAtmospheres` | 395 | True |
| 8 | 4 | `float` | `Normalized` | 396 | True |
| 12 | 4 | `uint` | `Reserved0` | 397 | True |

## H8ToolHeatCapacityRecord - PASS
- Lines: layout 400, struct 401
- Declared size: 16 (`16`), multipleOf8=True
| Offset | Size | Type | Field | Line | Natural Align |
|---:|---:|---|---|---:|---|
| 0 | 4 | `uint` | `ToolHash` | 403 | True |
| 4 | 4 | `float` | `HeatCapacity` | 404 | True |
| 8 | 4 | `float` | `MaxSafeTemperature` | 405 | True |
| 12 | 4 | `uint` | `Reserved0` | 406 | True |

## H8SubmarineHullConstantRecord - PASS
- Lines: layout 409, struct 410
- Declared size: 32 (`32`), multipleOf8=True
| Offset | Size | Type | Field | Line | Natural Align |
|---:|---:|---|---|---:|---|
| 0 | 4 | `uint` | `PartHash` | 412 | True |
| 4 | 4 | `float` | `MassKg` | 413 | True |
| 8 | 4 | `float` | `DragScalar` | 414 | True |
| 12 | 4 | `float` | `BuoyancyScalar` | 415 | True |
| 16 | 4 | `float` | `CrushDepthMeters` | 416 | True |
| 20 | 4 | `float` | `IntegrityCap` | 417 | True |
| 24 | 4 | `uint` | `Flags` | 418 | True |
| 28 | 4 | `uint` | `Reserved0` | 419 | True |

## H8NarrativeTriggerRecord - PASS
- Lines: layout 422, struct 423
- Declared size: 32 (`32`), multipleOf8=True
| Offset | Size | Type | Field | Line | Natural Align |
|---:|---:|---|---|---:|---|
| 0 | 8 | `double` | `AupX` | 425 | True |
| 8 | 8 | `double` | `AupY` | 426 | True |
| 16 | 8 | `double` | `AupZ` | 427 | True |
| 24 | 4 | `uint` | `TriggerHash` | 428 | True |
| 28 | 4 | `float` | `RadiusMeters` | 429 | True |

## H8PhysicsMaterialRecord - PASS
- Lines: layout 432, struct 433
- Declared size: 16 (`16`), multipleOf8=True
| Offset | Size | Type | Field | Line | Natural Align |
|---:|---:|---|---|---:|---|
| 0 | 4 | `uint` | `SurfaceHash` | 435 | True |
| 4 | 4 | `float` | `Friction` | 436 | True |
| 8 | 4 | `float` | `Restitution` | 437 | True |
| 12 | 4 | `uint` | `Flags` | 438 | True |

## H8GhostModuleRecord - PASS
- Lines: layout 441, struct 442
- Declared size: 64 (`64`), multipleOf8=True
| Offset | Size | Type | Field | Line | Natural Align |
|---:|---:|---|---|---:|---|
| 0 | 4 | `uint` | `ModuleHash` | 444 | True |
| 4 | 4 | `uint` | `Flags` | 445 | True |
| 8 | 4 | `float` | `SnapOffsetX` | 446 | True |
| 12 | 4 | `float` | `SnapOffsetY` | 447 | True |
| 16 | 4 | `float` | `SnapOffsetZ` | 448 | True |
| 20 | 4 | `float` | `PowerRequirement` | 449 | True |
| 24 | 4 | `float` | `BuildCostScalar` | 450 | True |
| 28 | 4 | `uint` | `RecipeHash` | 451 | True |
| 32 | 4 | `uint` | `DisplayNameUtf8Offset` | 452 | True |
| 36 | 4 | `uint` | `PortMask0` | 453 | True |
| 40 | 4 | `uint` | `PortMask1` | 454 | True |
| 44 | 4 | `uint` | `PortMask2` | 455 | True |
| 48 | 4 | `uint` | `PortMask3` | 456 | True |
| 52 | 4 | `uint` | `DisplayNameUtf8ByteLength` | 457 | True |
| 56 | 4 | `uint` | `Reserved0` | 458 | True |
| 60 | 4 | `uint` | `Reserved1` | 459 | True |

## H8RadiationIntensityCellRecord - PASS
- Lines: layout 462, struct 463
- Declared size: 16 (`16`), multipleOf8=True
| Offset | Size | Type | Field | Line | Natural Align |
|---:|---:|---|---|---:|---|
| 0 | 4 | `uint` | `CellHash` | 465 | True |
| 4 | 4 | `float` | `IntensitySv` | 466 | True |
| 8 | 4 | `float` | `FalloffMeters` | 467 | True |
| 12 | 4 | `uint` | `Reserved0` | 468 | True |

## H8SpawnCreditCostRecord - PASS
- Lines: layout 471, struct 472
- Declared size: 16 (`16`), multipleOf8=True
| Offset | Size | Type | Field | Line | Natural Align |
|---:|---:|---|---|---:|---|
| 0 | 4 | `uint` | `EntityHash` | 474 | True |
| 4 | 4 | `float` | `CreditCost` | 475 | True |
| 8 | 4 | `uint` | `DirectorMask` | 476 | True |
| 12 | 4 | `uint` | `Reserved0` | 477 | True |

## H8LightAttenuationSampleRecord - PASS
- Lines: layout 480, struct 481
- Declared size: 32 (`32`), multipleOf8=True
| Offset | Size | Type | Field | Line | Natural Align |
|---:|---:|---|---|---:|---|
| 0 | 4 | `float` | `DepthMeters` | 483 | True |
| 4 | 4 | `float` | `FogDensity` | 484 | True |
| 8 | 4 | `float` | `ScatterR` | 485 | True |
| 12 | 4 | `float` | `ScatterG` | 486 | True |
| 16 | 4 | `float` | `ScatterB` | 487 | True |
| 20 | 4 | `float` | `Absorption` | 488 | True |
| 24 | 4 | `uint` | `Flags` | 489 | True |
| 28 | 4 | `uint` | `Reserved0` | 490 | True |

## H8SopErrorRecord - PASS
- Lines: layout 493, struct 494
- Declared size: 16 (`16`), multipleOf8=True
| Offset | Size | Type | Field | Line | Natural Align |
|---:|---:|---|---|---:|---|
| 0 | 4 | `uint` | `ErrorHash` | 496 | True |
| 4 | 4 | `uint` | `MessageUtf8Offset` | 497 | True |
| 8 | 4 | `uint` | `Severity` | 498 | True |
| 12 | 4 | `uint` | `MessageUtf8ByteLength` | 499 | True |

## H8HudLayoutRecord - PASS
- Lines: layout 502, struct 503
- Declared size: 64 (`64`), multipleOf8=True
| Offset | Size | Type | Field | Line | Natural Align |
|---:|---:|---|---|---:|---|
| 0 | 4 | `uint` | `ElementHash` | 505 | True |
| 4 | 4 | `uint` | `Flags` | 506 | True |
| 8 | 4 | `float` | `M00` | 507 | True |
| 12 | 4 | `float` | `M01` | 508 | True |
| 16 | 4 | `float` | `M02` | 509 | True |
| 20 | 4 | `float` | `M03` | 510 | True |
| 24 | 4 | `float` | `M10` | 511 | True |
| 28 | 4 | `float` | `M11` | 512 | True |
| 32 | 4 | `float` | `M12` | 513 | True |
| 36 | 4 | `float` | `M13` | 514 | True |
| 40 | 4 | `float` | `M20` | 515 | True |
| 44 | 4 | `float` | `M21` | 516 | True |
| 48 | 4 | `float` | `M22` | 517 | True |
| 52 | 4 | `float` | `M23` | 518 | True |
| 56 | 4 | `float` | `M30` | 519 | True |
| 60 | 4 | `float` | `M31` | 520 | True |

## H8SectorPageRecord - PASS
- Lines: layout 523, struct 524
- Declared size: 32 (`32`), multipleOf8=True
| Offset | Size | Type | Field | Line | Natural Align |
|---:|---:|---|---|---:|---|
| 0 | 8 | `long` | `AupX` | 526 | True |
| 8 | 8 | `long` | `AupZ` | 527 | True |
| 16 | 4 | `uint` | `SectorHash` | 528 | True |
| 20 | 4 | `uint` | `BiomeHash` | 529 | True |
| 24 | 4 | `uint` | `FileOffsetBytes` | 530 | True |
| 28 | 4 | `uint` | `ByteCount` | 531 | True |

## H8EconomyRecord - PASS
- Lines: layout 534, struct 535
- Declared size: 64 (`H8DataLayoutConstants.EconomyRecordSize`), multipleOf8=True
| Offset | Size | Type | Field | Line | Natural Align |
|---:|---:|---|---|---:|---|
| 0 | 4 | `uint` | `HashId` | 537 | True |
| 4 | 4 | `uint` | `NameUtf8Offset` | 538 | True |
| 8 | 4 | `uint` | `DescriptionUtf8Offset` | 539 | True |
| 12 | 4 | `float` | `BasePrice` | 540 | True |
| 16 | 4 | `float` | `Scarcity01` | 541 | True |
| 20 | 4 | `float` | `Demand01` | 542 | True |
| 24 | 4 | `float` | `SupplyRefreshSeconds` | 543 | True |
| 28 | 4 | `float` | `AccessFrequency` | 544 | True |
| 32 | 4 | `uint` | `NameUtf8ByteLength` | 545 | True |
| 36 | 4 | `uint` | `DescriptionUtf8ByteLength` | 546 | True |
| 40 | 4 | `uint` | `Flags` | 547 | True |
| 44 | 4 | `uint` | `Reserved0` | 548 | True |
| 48 | 4 | `uint` | `Reserved1` | 549 | True |
| 52 | 4 | `uint` | `Reserved2` | 550 | True |
| 56 | 4 | `uint` | `Reserved3` | 551 | True |
| 60 | 4 | `uint` | `Reserved4` | 552 | True |

## H8PhysicsConstantsRecord - PASS
- Lines: layout 555, struct 556
- Declared size: 64 (`H8DataLayoutConstants.PhysicsConstantsRecordSize`), multipleOf8=True
| Offset | Size | Type | Field | Line | Natural Align |
|---:|---:|---|---|---:|---|
| 0 | 4 | `uint` | `HashId` | 558 | True |
| 4 | 4 | `uint` | `NameUtf8Offset` | 559 | True |
| 8 | 4 | `uint` | `DescriptionUtf8Offset` | 560 | True |
| 12 | 4 | `uint` | `NameUtf8ByteLength` | 561 | True |
| 16 | 4 | `uint` | `DescriptionUtf8ByteLength` | 562 | True |
| 20 | 4 | `float` | `MassKg` | 563 | True |
| 24 | 4 | `float` | `AddedMass` | 564 | True |
| 28 | 4 | `float` | `LinearDrag` | 565 | True |
| 32 | 4 | `float` | `Buoyancy` | 566 | True |
| 36 | 4 | `float` | `CrushDepthM` | 567 | True |
| 40 | 4 | `float` | `AupSectorSizeMeters` | 568 | True |
| 44 | 4 | `float` | `MaxWorldBoundsMeters` | 569 | True |
| 48 | 4 | `float` | `AccessFrequency` | 570 | True |
| 52 | 4 | `uint` | `Flags` | 571 | True |
| 56 | 4 | `uint` | `Reserved0` | 572 | True |
| 60 | 4 | `uint` | `Reserved1` | 573 | True |

## H8DataMonolithTelemetryEntry - PASS
- Lines: layout 576, struct 577
- Declared size: 64 (`H8DataLayoutConstants.TelemetryEntrySize`), multipleOf8=True
| Offset | Size | Type | Field | Line | Natural Align |
|---:|---:|---|---|---:|---|
| 0 | 8 | `ulong` | `Checksum64` | 579 | True |
| 8 | 8 | `long` | `LoadTicks` | 580 | True |
| 16 | 8 | `long` | `IoTicks` | 581 | True |
| 24 | 4 | `uint` | `FrameIndex` | 582 | True |
| 28 | 4 | `uint` | `BlobBytes` | 583 | True |
| 32 | 4 | `uint` | `SectionCount` | 584 | True |
| 36 | 4 | `uint` | `LoadStatus` | 585 | True |
| 40 | 4 | `uint` | `PathFlags` | 586 | True |
| 44 | 4 | `uint` | `StateHash` | 587 | True |
| 48 | 4 | `uint` | `Reserved0` | 588 | True |
| 52 | 4 | `uint` | `Reserved1` | 589 | True |
| 56 | 4 | `uint` | `Reserved2` | 590 | True |
| 60 | 4 | `uint` | `Reserved3` | 591 | True |

## H8StaticLocalizationReference - PASS
- Lines: layout 597, struct 598
- Declared size: 16 (`16`), multipleOf8=True
| Offset | Size | Type | Field | Line | Natural Align |
|---:|---:|---|---|---:|---|
| 0 | 4 | `uint` | `KeyHash` | 600 | True |
| 4 | 4 | `uint` | `Utf8Offset` | 601 | True |
| 8 | 4 | `int` | `ByteLength` | 602 | True |
| 12 | 4 | `uint` | `Reserved0` | 603 | True |

## H8StaticLocalizationCursor - PASS
- Lines: layout 609, struct 610
- Declared size: 8 (`8`), multipleOf8=True
| Offset | Size | Type | Field | Line | Natural Align |
|---:|---:|---|---|---:|---|
| 0 | 4 | `int` | `Section` | 612 | True |
| 4 | 4 | `int` | `RecordIndex` | 613 | True |


