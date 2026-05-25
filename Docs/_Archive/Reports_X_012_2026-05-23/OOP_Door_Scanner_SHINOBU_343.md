# [ARCHIVE] X_012 Historical Report

Archive date: 2026-05-23
Reason: removed from active documentation corpus by X_012; historical evidence only.
Active index: ../../Reports/README.md

# SHINOBU_343 OOP Door Scanner

- Generated UTC: 2026-05-23T00:00:00Z
- Summary: Previous static mirror scan found 0 suspicious OOP door state machines; Roslyn Unity menu execution pending.
- Parser route: Roslyn `CSharpSyntaxTree` primary pass in current scanner source; lexical fallback only on parse exception.
- Scan scope: `Assets/_Project/Scripts/Habitat`, `Assets/_Project/Scripts/Interaction`, `Assets/_Project/Scripts/Construction`
- Scanned files: 89
- Suspicious OOP door state machines: 0
- Post-R9 Unity menu execution: pending behind compile/build gate.

## Evidence

- Previous static mirror scan found no non-editor `void Update`, `void FixedUpdate`, or `void LateUpdate` door/hatch/bulkhead pressure lock state machine in the assigned domains. This is not a fresh Unity menu execution of the Roslyn scanner.
- SHINOBU_343 authority route is `BulkheadContainmentRuntime_HatchLocks -> HatchStateDTO.FsmStateMask -> BulkheadStateDTO.AssociatedLock/Flags`.
