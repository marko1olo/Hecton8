# HECTON8_BIOS_REGISTRY Rationale

Status: PENDING VERIFICATION

## Decision 001 - Ledger Recovery Before Source Edits

Problem: Context compression removed reliable chat-state continuity while AGENTS.md requires disk-backed state tracking before every response.
Solution: Create a dedicated BIOS/Registry status file and rationale file before further source edits.
Rejected Alternatives: Continuing from chat memory only; that fails anti-amnesia and makes compile recovery non-auditable.
Scalability potential: Low/Middle/High/Ultra unaffected at runtime; improves multi-agent integration discipline.
Hardware Impact: 0 us runtime; prevents duplicate work and accidental cross-domain edits.

## Decision 002 - Keep Generated Project Files Secondary

Problem: Current compile errors suggest generated csproj files may be stale and missing valid source files.
Solution: Fix confirmed source-level errors first; avoid editing generated project files unless source is clean and the build still cannot see files.
Rejected Alternatives: Manually inserting compile includes into generated csproj; Unity can overwrite it and it masks assembly layout defects.
Scalability potential: Low/Middle/High/Ultra unaffected; preserves Unity assembly ownership.
Hardware Impact: 0 us runtime; reduces integration breakage.

## Decision 003 - Strict Compile Gate After Incremental Masking

Problem: A normal project build produced a DLL while later strict Core-only compilation exposed transient source errors from concurrent edits.
Solution: Re-run strict builds with `BuildProjectReferences=false` after the source tree settled, covering `Hecton8.Core`, `Assembly-CSharp`, and `Hecton8.Editor`.
Rejected Alternatives: Trusting the first incremental build output; it can mask stale references in a multi-agent workspace.
Scalability potential: Low/Middle/High/Ultra unaffected directly; compile stability is prerequisite for Math LOD deployment.
Hardware Impact: 0 us runtime; prevents broken builds from reaching low-end test hardware.

## Decision 004 - Two-Tier Math LOD Registry

Problem: BIOS needed a single hardware-authoritative precision switch without hardcoding render/physics consumers.
Solution: Store `MathPrecisionLevel` in the hardware profile and GlobalRegistry, push `_MATH_LOD_LOW/_MATH_LOD_HIGH` keywords, and let the watchdog degrade from High to Low over 60 frames.
Rejected Alternatives: Per-system ad hoc device checks; they drift, branch in hot paths, and create contradictory visual states.
Scalability potential: Low uses dominant-axis/cheap math fakes; Middle can opt into Low selectively; High/Ultra retain true-normal/visual-overkill paths.
Hardware Impact: Estimated 8-40 us/frame saved on i3/MX350 once shader and simulation consumers honor the global low-precision path.

## Decision 005 - Editor-Only Meta Generation

Problem: Missing script `.meta` files can break Unity assembly discovery and generated project files.
Solution: Add `MetaFileGenerator` as an editor script that scans first-party scripts, imports missing scripts, and writes a minimal MonoImporter meta only if Unity still did not generate one.
Rejected Alternatives: Runtime repair; writing inside `Assets/_Project/Scripts/Plugins` or third-party folders.
Scalability potential: Low/Middle/High/Ultra unaffected at runtime; reduces source-control churn and compile stalls.
Hardware Impact: 0 us runtime; editor-only maintenance.
