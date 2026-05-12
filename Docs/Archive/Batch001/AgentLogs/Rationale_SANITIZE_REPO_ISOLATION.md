# Rationale: SANITIZE_REPO_ISOLATION

Problem: Existing final-border prompt requires a persisted scalability tier and decoupled broadcast without introducing Core dependencies on render/VFX or third-party SDKs.
Solution: Use a Core platform integration contract plus typed event notification and keep persistence in the existing options MMF owner.
Rejected Alternatives: Direct calls into VFX/render services would create cross-domain references; JSON-only settings storage would keep the tier behind runtime parsing and avoid the requested fixed layout.
Scalability potential: Low uses tier 0 for MX350 path selection; High uses tier 1 as the hook for richer buffers and visual overkill without changing Core.
Hardware Impact: Tier lookup is a byte read from the MMF header; expected runtime frame impact is 0us outside user-triggered settings changes.

Problem: State files were absent for this batch.
Solution: Created fresh status/rationale files under `Docs/Tasks` and `Docs/AgentLogs` with the latest 25-task prompt as the source.
Rejected Alternatives: Relying on chat history is incompatible with the anti-amnesia protocol.
Scalability potential: Disk state survives context compaction.
Hardware Impact: Editor/documentation only; no runtime cost.

Problem: Scalability tier needed to cross the `Hecton8.Input` and `Hecton8.Core` assembly boundary without adding a circular reference.
Solution: Placed `ScalabilityTierProfiles`, `IPlatformIntegration`, and `PlatformIntegrationBridge` in `Hecton8.Bootstrap.Contracts`; Core configures the bridge and Input owns persistence.
Rejected Alternatives: Putting `IPlatformIntegration` only in Core broke Input compilation; making Input reference Core would invert the existing dependency graph.
Scalability potential: Low maps to MX350/math-low; High maps to RTX/math-high. Render/VFX listeners can spend saved cycles only after the typed event fires.
Hardware Impact: One normalized byte and one optional delegate call on change; 0us per idle frame.

Problem: Render/VFX leads need a deterministic notification when the player changes scalability tier, but Core must not hold direct render-domain references.
Solution: Added `ScalabilityChangedEvent` as a 2-byte readonly struct and queued it through a bounded `NativeQueue` drained by `SystemDispatcher`.
Rejected Alternatives: C# events with captured lambdas and direct service calls risk managed allocations and domain coupling.
Scalability potential: Low-tier listeners can shrink buffers next dispatcher lane; high-tier listeners can opt into visual overkill without platform branching in gameplay.
Hardware Impact: Idle cost is a queue count add during dispatcher flush; change-path cost estimated under 5us with small listener count.

Problem: `options.h8cfg` needed a scalability tier without save stutter from payload allocations.
Solution: Promoted the MMF header to 16 bytes and wrote `ScalabilityTier` beside magic/version/payload length while reusing the fixed 64KB payload buffer.
Rejected Alternatives: Appending a separate sidecar config file or doing `File.ReadAllText` for tier lookup would add IO/parsing surfaces.
Scalability potential: Toaster boot can choose the MX350 tier before expensive visual setup; RTX tier persists as a single byte and can unlock richer buffers.
Hardware Impact: Avoids a 64KB managed payload allocation per save/load; frame impact remains outside hot Tick.

Problem: Core previously risked direct middleware references or false compliance by manual review.
Solution: Verified `Hecton8.Core.asmdef` has no Crest/MapMagic/Steamworks references and `HectonComplianceValidator` enforces ACL001 at build preprocess.
Rejected Alternatives: Keeping SDK references in Core for convenience would force full recompiles and leak third-party APIs into gameplay.
Scalability potential: Plugins can be upgraded or disabled without touching Core gameplay contracts.
Hardware Impact: Runtime impact 0us; editor compile graph is smaller for the SDK boundary.

Problem: Steam callbacks can steal random frame time if drained at 60Hz.
Solution: Verified `SteamAPI.RunCallbacks()` appears only inside `SteamManager.FrostTick()` and the manager registers as `IFrostTickable`.
Rejected Alternatives: `Update()` polling and callback logging were rejected because they add random frame spikes and possible strings.
Scalability potential: Low-tier devices avoid callback jitter; high-tier devices keep the same deterministic cadence.
Hardware Impact: Expected saved spike range 1000-2000us on frames that previously drained Steam at render cadence.

Problem: Assembly reload isolation request cannot be honestly marked complete while UI scripts still live under the root Core asmdef scope.
Solution: Marked Task 15 blocked by existing asmdef scope and recorded the needed migration instead of moving broad UI files during the border task.
Rejected Alternatives: Moving UI scripts ad hoc would risk references and prefab script GUID stability across other agents' work.
Scalability potential: Future split into `Hecton8.UI.Runtime` can reduce editor churn without runtime behavior change.
Hardware Impact: Editor workflow only; runtime 0us.

Problem: A broader no-third-party-in-Core interpretation still finds direct `GPUInstancer` and `VLB` namespaces in Core-owned runtime files.
Solution: Recorded the boundary debt instead of deleting asmdef references blindly; the current pass completed the requested Crest/MapMagic/Steamworks border without breaking compile.
Rejected Alternatives: Removing `GPUInstancer` and `VolumetricLightBeam` asmdef references immediately would break `HectonRockManager`, scatter runtime, flashlight, and underwater visual serialized fields unless full adapter/prefab migration happens together.
Scalability potential: A future GPUI/VLB adapter split can let low-tier disable heavy visual integrations cleanly and high-tier keep overkill visuals behind plugin contracts.
Hardware Impact: Not fixed in this pass; no claimed microsecond gain.
