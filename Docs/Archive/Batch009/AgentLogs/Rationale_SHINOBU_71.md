# SHINOBU_71 Rationale

Status: STATIC VERIFIED / BUILD HELD BY GUARD

## Decision 001 - Attach DRS To Existing Adapter

Problem: The repository already contains `ThermalDynamicResolutionAdapter` and `DynamicResolutionScaler`; adding a third scaler would create competing render-scale writers.
Solution: Keep ownership in the existing graphics scalability adapter and patch only the missing SHINOBU_71 acceptance gaps.
Rejected Alternatives: A new MonoBehaviour would race the existing adapter and duplicate `DynamicResolutionHandler` state. Direct `Screen.SetResolution` is forbidden because it reallocates display buffers.
Scalability potential: Low keeps scale near survival floor with sharpened reconstruction; Middle/High recover smoothly; Ultra spends headroom on visual overkill shader globals.
Hardware Impact: i3/MX350 avoids display-buffer reallocations and keeps DRS work scalar-only; estimated hot-path cost remains below 100 microseconds, pending profiler proof.

## Decision 002 - Cache GlobalQualityWeight From Vault Contract

Problem: The adapter had a stress-derived quality proxy, so DRS could pass static scans while not explicitly consuming the published `GlobalQualityWeight` scalar.
Solution: Cache the sanitized global quality scalar once per Tick from `BufferID.ShinobuScalabilityState` / `ScalabilityStateDTO.GlobalQualityWeight`, fall back only to the last valid cached quality/default 1.0 when the vault handle is absent, let mock quality weight clamp it, and use `TargetRenderScale = lerp(minScaleLimit, 1.0, cachedWeight)` before existing pressure collapses. Expose the cached scalar in `ResolutionScaleState` at offset 52 without growing the 64 B contract.
Rejected Alternatives: Native `Shader.GetGlobalFloat` reads were rejected because they cross into presentation state from the policy Tick. Creating the scalability dictator buffer from DRS was rejected because DRS does not own that domain's memory.
Scalability potential: Low uses the same continuous scalar to fall toward survival scale. Middle rises smoothly without threshold chatter. High keeps scale close to 1.0 while still reducing post/shader pressure under stress. Ultra uses headroom for visual-overkill globals instead of a separate binary branch.
Hardware Impact: Removes two shader-global native bridge reads from Tick and avoids concrete Homeostasis polling in the policy fallback. Vault handle resolution is cached; estimated hot-path source read remains sub-1 us. The preserved render-scale drop can save 500-3000 us GPU fill time under pressure, scene dependent, not profiler-measured in this pass.

## Decision 003 - Repair No-Runtime URP Fallback

Problem: The adapter fallback accepted a render-scale parameter but discarded it, only resizing scalable buffers. If `IDynamicResolutionRuntime` is absent, URP asset scale could remain stale.
Solution: In `ApplyDirectRenderScale`, clamp the render scale, write `_urpAsset.renderScale` only when the value changes beyond epsilon, then resize scalable buffers with the sanitized buffer scale. The existing registry runtime remains the normal writer.
Rejected Alternatives: Allocating replacement render targets was rejected for GC/VRAM churn. Forcing `Screen.SetResolution` was rejected for display-buffer stutter. Writing URP asset every frame was rejected because epsilon-gated writes are cheaper.
Scalability potential: Low/Middle keep the survival scale even if registry runtime is missing. High/Ultra keep 1.0 scale without extra writes. All tiers preserve the same shader global contract.
Hardware Impact: Fallback branch adds one float compare and rare property write; sub-2 us CPU when active. Prevents stale full-resolution rendering that can cost 500-3000 us GPU on i3/MX350 pressure scenes.

## Decision 004 - Keep Upscaling Tiered And Screen-Space Only

Problem: A uniform FSR path can burn compute on mobile/low-tier hardware, and tying DRS to AUP/world coordinates would widen a screen-space system for no gain.
Solution: Keep the existing tier resolver: native at full scale, bilinear/TAA for low/mobile/no-compute, FSR hash only when hardware tier and compute support justify it. Publish screen pixel dimensions and post-process weight as shader globals; keep DRS state to scalar screen-space fields.
Rejected Alternatives: FSR on Quest/MX350-class weak ALUs was rejected because compute reconstruction can erase fill-rate savings. Passing AUP doubles into DRS was rejected because render scale is camera/screen-space, not world-space.
Scalability potential: Low gets cheap bilinear/TAA and culls heavy post. Middle uses the same reconstruction budget with smoother recovery. High/Ultra can spend the saved cycles on FSR and visual-overkill shader flags.
Hardware Impact: Static estimate: mobile/low avoids 90-220 us FSR compute overhead; panic/post cull can reclaim 500-3000 us GPU in overdraw-heavy scenes. No profiler capture in this pass.

## Decision 005 - Keep Human Facades Cold

Problem: The prompt requires tuner, CSV override, and oscilloscope support, but those facilities can become managed allocation sources if copied into the player Tick path.
Solution: Keep parser logic on the adapter as span-based code and the file picker/string read inside the editor window only. Runtime DRS state, telemetry, and DTO writes remain DataVault/native paths.
Rejected Alternatives: `string.Split`, LINQ CSV, or per-frame editor polling were rejected because they would hide allocation debt behind tooling. Moving graph samples into a managed runtime list was rejected; the editor copies from the fixed telemetry ring into preallocated arrays.
Scalability potential: Low devices pay no player-build editor facade cost. Middle/High/Ultra keep the same runtime telemetry and can tune stronger visual-overkill budgets without changing the hot path.
Hardware Impact: Player build cost is 0 us for editor facades. Runtime telemetry write remains estimated 1-2 us and buys deterministic postmortem evidence when a frame-scale fault occurs.

## Decision 006 - Stop Before dotnet Under CPU Guard

Problem: The batch requires compile verification, but the machine was under active load above the explicit 50 percent CPU limit.
Solution: Ran static scans and stopped before `dotnet build`. Recorded the guard values and left compile status blocked rather than violating the build rule.
Rejected Alternatives: Launching `dotnet build` under 79-99 percent CPU was rejected because the AGENTS rule explicitly forbids it. Skipping evidence was rejected; static DRS scans and self-audit were written.
Scalability potential: No runtime design change. The guard prevents local validation from adding noise or starving other concurrent agents.
Hardware Impact: Compile not run. Runtime impact of this decision is 0 us. Latest recheck found no dotnet/csc process, but CPU samples were 100%, 100%, 100%, 100%, 100%, so the build guard still holds.

## Decision 007 - Pixel-Stable EWMA And TAA Sharpen Curve

Problem: A pure EWMA render scale can still commit fractional internal sizes every frame. URP will round those dimensions differently across camera/render-target paths, causing sub-pixel crawl and perceived sharp pixel jumps even though the scalar itself is smooth. The previous sharpen was a raw inverse-scale multiplier, which recovers edges but risks ringing and shimmer at low quality weights.
Solution: Keep EWMA as the temporal governor, then snap the smoothed render scale to a 2-pixel dominant-axis grid before committing it to `DynamicResolutionHandler`. Replace raw sharpen with a polynomial/inverse blend: `Smooth01(linear deficit)` is lerped toward normalized inverse deficit, then damped by `GlobalQualityWeight` to avoid low-quality ringing.
Rejected Alternatives: Raising `ScaleEpsilon` only was rejected because it reduces commit frequency but still allows arbitrary fractional render-target sizes. A fixed sharpening table was rejected because it cannot breathe with continuous `GlobalQualityWeight` and would reintroduce visual thresholds.
Scalability potential: Low uses the stable pixel grid and stronger but damped reconstruction so 0.6-0.7 scale remains legible without shimmer. Middle transitions through the same grid with no binary tier change. High/Ultra keep scale near native and run minimal sharpening, spending headroom on visual-overkill shader globals.
Hardware Impact: Added scalar math is O(1), estimated sub-1 us on i3/MX350. Prevents per-frame internal-size crawl that otherwise manifests as visible pixel instability; GPU fill-rate savings remain 500-3000 us scene dependent when DRS drops scale.

## Decision 008 - Do Not Own The Scalability Dictator Buffer

Problem: Reading `GlobalQualityWeight` from shader globals kept DRS decoupled but used the wrong direction of data flow: policy code depended on presentation state. Directly allocating the dictator state from DRS would invert ownership and create H-PHI debt.
Solution: Add a cached `VaultBufferHandle<ScalabilityStateDTO>` and resolve only the existing `BufferID.ShinobuScalabilityState` buffer. If it is missing or still zeroed on frame 0, hold the last valid cached quality/default 1.0. DRS owns only its DRS state, resolution scale state, and telemetry buffers.
Rejected Alternatives: `Shader.GetGlobalFloat` in Tick was rejected for native bridge overhead and wrong layer. `GetBufferHandle<ScalabilityStateDTO>` creation from DRS was rejected because Hardware Homeostasis owns that payload.
Scalability potential: Low/Middle/High/Ultra all consume the same continuous quality scalar without polling shader presentation state. Mock quality still clamps the value for blind dependency proof.
Hardware Impact: Avoids two shader-global reads per frame, estimated sub-2 us on i3/MX350. Adds one cached handle metadata field and a NativeArray struct read when the vault buffer exists; no runtime allocation.

## Decision 009 - Cached Quality Fallback Instead Of Concrete Polling

Problem: The hardened vault source still had a concrete `HomeostasisBrain.GlobalQualityWeight` fallback. That is cheaper than shader polling but still couples the DRS policy to a core implementation detail instead of the vault contract.
Solution: Use the external vault scalar when present; otherwise reuse `_latestGlobalQualityWeight01` or default to 1.0 on frame 0. Mock quality clamps after fallback, so blind thermal tests still drive scale down without live dictator state.
Rejected Alternatives: Keeping the Homeostasis static fallback was rejected as compile-wall leakage. Returning 0.0 when the vault is missing was rejected because it would panic-drop render scale during bootstrap gaps.
Scalability potential: Low/Middle/High/Ultra remain continuous once the vault scalar exists; bootstrap gaps are visually stable instead of falsely throttled.
Hardware Impact: Removes one concrete static read from the fallback path. Runtime impact is scalar-only, 0 B GC.

## Decision 010 - Delete Residual Shader Quality Fallback

Problem: A post-polish forbidden-symbol scan still found `TryReadPublishedShaderQualityWeight` using `Shader.GetGlobalFloat` inside `ResolvePublishedGlobalQualityWeight`. That contradicted the vault-only quality-source decision.
Solution: Removed the fallback method and the two shader property IDs. The quality source order is now external vault state, then cached/default scalar, then mock clamp. Shader globals remain output-only for render/post-process consumers.
Rejected Alternatives: Keeping shader-global reads as a missing-vault fallback was rejected because it reintroduces native bridge cost and presentation-to-policy coupling.
Scalability potential: Low/Middle/High/Ultra keep the same continuous curve when the dictator vault is present; bootstrap remains stable at cached/default 1.0 instead of polling presentation state.
Hardware Impact: Removes two native shader-global reads per missing-vault Tick, estimated sub-2 us on i3/MX350. Runtime allocation impact remains 0 B GC.
