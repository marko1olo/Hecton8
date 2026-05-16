# CONTENT_AUTHORITY_DICTATOR Rationale

## Mandate Selection
Problem: Asset pipeline work crosses runtime streaming, build validation, content tiers, and Babel text lookup.
Solution: Use the eight mandates recorded in Status_CONTENT_AUTHORITY_DICTATOR.md as governing constraints before code.
Rejected Alternatives: Narrow reading of only Addressables docs was rejected because tasks include save topology, VRAM, visibility, and localization.
Scalability potential: Low uses tiny proxies, strict bundle denial, and early eviction. Middle keeps deterministic LOD. High and Ultra spend saved cycles on dense visible content and overkill groups.
Hardware Impact: Expected i3/MX350 gain is avoiding missing asset stalls, reducing redundant bundle loads, and cutting small-collider/LOD/shadow overhead before runtime.

## Initial Architecture Decision
Problem: The required domain folder does not exist, but optimization/runtime services already exist in Hecton8.Core.
Solution: Add a CORE/ASSETS content authority layer under Assets/_Project/Scripts/Core/Content and bind to existing GlobalRegistry, AssetLifecycleGovernor, AssetLoadDispatcher, VRAMMonitor, and typed SignalBus lanes.
Rejected Alternatives: Moving or rewriting Optimization services was rejected as cross-domain churn and collision risk with 20+ parallel agents.
Scalability potential: Low and Quest paths deny Overkill bundles before download; High and Ultra allow richer groups after validation.
Hardware Impact: Keeps main thread load dispatch under the existing 2 ms window and shifts content failures to editor/build gates.
