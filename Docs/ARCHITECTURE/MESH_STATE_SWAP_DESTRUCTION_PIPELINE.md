# Mesh State Swap Destruction Pipeline

Date: 2026-05-21
Status: PENDING VERIFICATION
Owner: SHINOBU_ARCHIVARIUS_SURGEON
Evidence class: STATIC_DOC

## Rule

Runtime CPU Voronoi fracture is forbidden for habitat modules, wreckage, and large construction pieces.

Use mesh state swaps:

1. built
2. worn
3. damaged
4. ruptured
5. destroyed

Each state is a pre-baked mesh/material/render payload selected by data. The shader handles tear masks, waterlines, corrosion, scorch, and leak visuals.

## Runtime Route

- gameplay damage writes integrity/state DTOs
- renderer selects mesh ID and material state from the DTO
- waterline and breach visuals are shader-controlled
- collision changes use pre-authored simplified collision states
- debris is pooled or pre-authored, not fractured from source geometry at runtime

## Rejected Paths

- runtime CPU Voronoi fragmentation
- per-hit mesh boolean cuts
- spawned crack mesh trees
- per-renderer material clone storms
- physics truth derived from visual shard positions

## Scalability

`GlobalQualityWeight` may scale:

- shader tear detail
- debris density
- particle count
- leak sprite count
- scorch/corrosion overlay sample count

It must not change module integrity, save identity, or damage authority.

## Non-Claims

This document defines the architecture standard. Runtime proof requires Frame Debugger, profiler, GCMonitor, and destruction replay artifacts.
