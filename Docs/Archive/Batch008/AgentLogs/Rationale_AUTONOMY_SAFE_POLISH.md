# AUTONOMY_SAFE_POLISH Rationale

Date: 2026-05-18
Status: PATCHED - PENDING INTEGRATION VERIFICATION

## Decision 01: Work Only In A Clean, Isolated Runtime File

Problem: The worktree contains many modified and untracked files owned by other active agents. A broad mandate sweep would risk overwriting or conflicting with their work.

Solution: Use `git status --short -- <path>` before touching any candidate. Select only a clean file and keep the edit local to one provable mandate breach.

Rejected Alternatives: A repository-wide `Pack = 1` rewrite was rejected because it would alter busy domains and could create compile walls. Updating shared contracts was rejected because it would trigger wider recompiles.

Scalability potential: Low tier avoids ARM64/Quest alignment penalties. Middle/high/ultra tiers keep identical gameplay semantics while preserving audio transition correctness.

Hardware Impact: Estimated improvement is risk elimination rather than measured frame-time gain. On weak ARM64 silicon, removing packed runtime layout prevents potential unaligned access penalties in repeated acoustic-state copies.

## Decision 02: Target Runtime Struct Layout Before Feature Expansion

Problem: `AcousticZoneController.cs` is clean and contains a private runtime DTO with `[StructLayout(LayoutKind.Sequential, Pack = 1)]`. The active mandate forbids packed runtime memory and requires 8-byte-aligned structs.

Solution: Patch only the private acoustic graph DTO if field usage proves local and layout can be made a multiple of 8 with explicit padding.

Rejected Alternatives: Adding new audio features, new events, or editor tooling was rejected because the first safe gain is to remove the platform-layout breach without adding dependencies.

Scalability potential: Low tier gets safer CPU memory behavior. Middle/high tiers retain the same authored audio graph. Ultra tier can spend saved headroom elsewhere; this patch does not couple visuals or gameplay.

Hardware Impact: Static estimate only. Seven-float packed DTO is 28 bytes; padded 8-byte-multiple DTO is 32 bytes, avoiding array stride/layout ambiguity on ARM64.

## Decision 03: Guard Acoustic Occlusion Normalization Locally

Problem: `UpdateEmitterOcclusionState` checked `totalWeight > 0.0001f` before division, but the project math mandate requires explicit denominator guards.

Solution: Replace two divisions by `totalWeight` with one `math.rcp(math.max(totalWeight, 0.0001f))` and multiply both weighted sums by that reciprocal.

Rejected Alternatives: Leaving the guard implicit was rejected because the NaN-vaccination rule wants denominator protection at the operation site. Adding exception handling was rejected because this is hot-path audio math and exceptions are not an acceptable control path.

Scalability potential: Low tier avoids rare NaN propagation from bad emitter samples. Middle/high/ultra tiers keep identical acoustic behavior for valid weights and spend no extra allocations.

Hardware Impact: Static estimate only. One safe reciprocal replaces two scalar divisions in the acoustic occlusion cold/hot boundary; expected effect is sub-microsecond and primarily safety-oriented.

## Decision 04: Do Not Fix External Compile Wall

Problem: Targeted `dotnet build Hecton8.Core.csproj --no-restore` failed before validating this audio patch because `Assets/_Project/Scripts/Core/InputDispatcher.cs` has syntax errors in the current dirty worktree.

Solution: Stop at one failed targeted build, record the exact external errors, and avoid editing `InputDispatcher.cs` because it is outside this autonomous safe-polish slice and already modified by another agent.

Rejected Alternatives: Fixing `InputDispatcher.cs` was rejected as cross-domain interference. Building the full solution was rejected because it would add rebuild pressure without isolating this patch.

Scalability potential: The local patch remains safe across low/middle/high/ultra tiers. Integration verification must resume after the core input compile wall is cleared.

Hardware Impact: No runtime impact from the blocked build. Process impact: one targeted restore and one targeted build only; no full solution rebuild.

## Decision 05: Fix Local Acoustic Echo DTOs, Not Global AUP

Problem: `AcousticEchoLocationRuntime.cs` uses `EchoTap`, `AcousticEchoTrailState`, and `AcousticEchoBlackBoxEntry` in NativeQueue, NativeArray, Vault, Burst job, and Blackbox paths while all four DTOs had `[StructLayout(..., Pack = 1)]`. `EchoTap` was 124 bytes by static field accounting, which violates the 8-byte multiple rule for runtime memory.

Solution: In the clean acoustic sensory file only, remove `Pack = 1`, rename private reserved fields to mandate-compliant `_pad*`, add a 4-byte `_pad1` to `EchoTap`, and reorder the internal Blackbox entry so its 8-byte `long` grid coordinates are first. Do not touch `AbsoluteUniversePosition` in `PersistentWorldRegistry.cs`; that is a shared world-core type with binary layout claims and requires a dedicated owner pass.

Rejected Alternatives: Editing `AbsoluteUniversePosition` was rejected because it is a large load-bearing world/save type and would create broad risk. Mechanical replacement of every `Pack = 1` candidate was rejected because many candidates are in dirty or shared domains. Leaving `EchoTap` at 124 bytes was rejected because it is a Vault/NativeQueue element.

Scalability potential: Low tier avoids unaligned/native stride hazards in AI sensory echo tracking. Middle tier keeps deterministic echo breadcrumbs. High/ultra tiers preserve the same acoustic fake while allowing richer presentation elsewhere.

Hardware Impact: Static estimate only. The primary gain is ARM64/Quest/Android safety for 32 echo taps per frame and 64 queued taps; no profiler microseconds claimed.

## Decision 06: Keep Loop 2 At Static Proof Because Core Is Externally Broken

Problem: After the acoustic echo DTO patch, targeted Core build no longer reports `InputDispatcher.cs`, but it still fails on other active dirty domains: `WorldChunkResidencyManager.cs`, `GlobalPhysicsStateManager.cs`, and `SubmarineDynamicsRuntime.cs`.

Solution: Record the compile wall as external and keep this loop at `PATCHED - PENDING INTEGRATION VERIFICATION`. Do not edit those files inside this safe-polish pass.

Rejected Alternatives: Fixing world streaming, global physics, or vehicle dynamics from this pass was rejected because those are separate active domains with many missing members and ambiguous ownership.

Scalability potential: The acoustic echo DTOs are safer for low/middle/high/ultra tiers, but runtime proof must wait for the broader Core compile wall to clear.

Hardware Impact: No measured runtime impact. Static layout proof is complete; runtime/Burst/IL2CPP impact remains unverified.
