# Rationale 1884

Evidence class: STATIC_SOURCE / STATIC_DOC.

This task was constrained to report-only static audit. I did not run Unity, dotnet build, menu items, or asset generation.

The severity choice is based on the task definition: `BLOCKER` is allowed for a source/route mismatch likely to waste the future Unity slot. Four output folder constants conflict with the 1879 relink CSV expected folders:

- tools: flat `Assets/_Project/Art/Generated/ProductFace/Tools` versus per-tool `Assets/_Project/Art/Generated/Tools/<ToolName>/`;
- resources: flat `Assets/_Project/Art/Generated/ProductFace/Resources` versus per-resource `Assets/_Project/Art/Generated/Resources/<ResourceName>/`;
- transport: flat `Assets/_Project/Art/Generated/ProductFace/Transport` versus per-transport `Assets/_Project/Art/Generated/Transport/<TransportName>/`;
- player suit: `Assets/_Project/Art/Generated/ProductFace/PlayerSuit` versus `Assets/_Project/Art/Generated/PlayerSuit/`.

No compile success was claimed. No visual/runtime acceptance was claimed.

No destructive source/asset API route was found. Asset writes are Editor mesh-asset writes only in source-authoring files. The sky/ocean validator is read-only by static scan.
