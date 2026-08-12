# Hecton8 Documentation Audit, Consolidation & Knowledge Graph Master Plan

## Authority Quote & Constraints
> "The canonical authority is C:\hades\Hecton8\AGENTS.md... PROJECT_BIBLES.md for domain/bible selection... VISION_LOCKS.md for product vision, ambiguity, priority, or taste conflicts... TASTE.md for player-visible work." — HECTON-8 Global Router & AGENTS.md

## Architecture & Scope
This plan covers the comprehensive audit, consolidation, mandate verification, refactoring, and knowledge graph generation across the HECTON-8 project documentation.

Key Objectives:
- R1: Integrity Audit & Stale Data Removal across `Docs/`.
- R2: Mandate Verification against Unity C# codebase in `Assets/_Project/Scripts/`.
- R3: Documentation Refactoring & archiving obsolete task files to `Docs/Archive/`.
- R4: Knowledge Graph Generation (`Docs/HECTON8_KNOWLEDGE_GRAPH.md`).

## Milestones & Status
| # | Name | Scope | Dependencies | Status |
|---|------|-------|-------------|--------|
| M1-M11 | Previous Voxel & Terrain Milestones | Core engine, SDF determinism, erosion stability, physics signal. | none | DONE |
| M12 | Documentation Reconnaissance & Contradiction Survey | Survey all `Docs/` files, mandate registry (`Tools/Docs/TestMandateRegistry.py`), codebase alignment (`Assets/_Project/Scripts/`), obsolete tasks in `Docs/Tasks/`, and knowledge graph requirements. | none | DONE |
| M13 | Documentation Refactoring & Knowledge Graph Implementation | Clean up `Docs/`, archive completed tasks to `Docs/Archive/Batch014_LegacyTasks/`, move unauthorized root docs (`BACKLOG.md`, `goose_audit_test.md`), standardize mandate header, resolve contradictions, and author `Docs/HECTON8_KNOWLEDGE_GRAPH.md`. | M12 | DONE |
| M14 | Verification, Review, Stress Test & Forensic Integrity Audit | Reviewer verification of docs, Challenger automated execution (`python Tools/Docs/TestMandateRegistry.py --strict`, `git diff --check`, `python Tools/Docs/TestAgentRuleRouting.py`), and Forensic Integrity Audit (CLEAN). | M13 | IN_PROGRESS |

## Interface Contracts & Constraints
- Mandatory strict mandate test: `python Tools/Docs/TestMandateRegistry.py --strict` exits 0 with 0 errors and 0 warnings.
- Mandatory formatting check: `git diff --check` shows no trailing whitespace or unresolved merge conflicts in documentation.
- Mandatory routing test: `python Tools/Docs/TestAgentRuleRouting.py` exits 0.
- No conflicting technical limits across active `.md` files.
- Completed task files moved from `Docs/Tasks/` to `Docs/Archive/Batch014_LegacyTasks/`.
- `Docs/HECTON8_KNOWLEDGE_GRAPH.md` created linking all active bibles and routing files.

## Verification Gate Summary
- **Reviewer**: Pending
- **Challenger**: Pending
- **Forensic Auditor**: Pending
