#!/usr/bin/env python3
"""Run the offline asset-front static validators."""

from __future__ import annotations

import argparse
import subprocess
import sys
from dataclasses import dataclass
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]


@dataclass(frozen=True)
class ValidatorCommand:
    name: str
    args: tuple[str, ...]


VALIDATOR_COMMANDS = (
    ValidatorCommand("mass_deletion_dirty_set", ("Tools/ValidateMassDeletionDirtySet.py", "--no-fail")),
    ValidatorCommand(
        "asset_static_summary",
        (
            "Tools/ValidateAssetStaticSummary.py",
            "--summary",
            "Docs/Reports/AssetSystem_20260605/ASSET_STATIC_VALIDATION_SUMMARY_20260605.md",
        ),
    ),
    ValidatorCommand("asset_front_file_map", ("Tools/ValidateAssetFrontFileMap.py",)),
    ValidatorCommand("asset_action_queue", ("Tools/ValidateAssetActionQueue.py",)),
    ValidatorCommand("asset_owner_packet_index", ("Tools/ValidateAssetOwnerPacketIndex.py",)),
    ValidatorCommand("asset_proof_artifact_index", ("Tools/ValidateAssetProofArtifactIndex.py",)),
    ValidatorCommand("audio_listening_policy_queues", ("Tools/ValidateAudioListeningPolicyQueues.py",)),
    ValidatorCommand("audio_route_decision_matrices", ("Tools/ValidateAudioRouteDecisionMatrices.py",)),
    ValidatorCommand("audio_critical_cue_source_coverage", ("Tools/ValidateAudioCriticalCueSourceCoverage.py", "--no-fail")),
    ValidatorCommand("audio_direct_ref_detail", ("Tools/ValidateAudioDirectRefDetail.py",)),
    ValidatorCommand("audio_scene_static_route", ("Tools/ValidateAudioSceneStaticRoute.py", "--no-fail")),
    ValidatorCommand("audio_addressables_p0_synthesis", ("Tools/ValidateAudioAddressablesP0Synthesis.py",)),
    ValidatorCommand("audio_import_meta_policy", ("Tools/ValidateAudioImportMetaPolicy.py", "--no-fail")),
    ValidatorCommand("audio_waveform_proof_artifacts", ("Tools/ValidateAudioWaveformProofArtifacts.py",)),
    ValidatorCommand("player_route_static_evidence", ("Tools/ValidatePlayerRouteStaticEvidence.py", "--require-production-static", "--no-fail")),
    ValidatorCommand("foam_contact_decision_queue", ("Tools/ValidateFoamContactDecisionQueue.py",)),
    ValidatorCommand("mapmagic_erosion_source_route", ("Tools/ValidateMapMagicErosionSourceRoute.py",)),
    ValidatorCommand("mesh_prefab_review_queue", ("Tools/ValidateMeshPrefabReviewQueue.py",)),
    ValidatorCommand("texture_import_role_matrix", ("Tools/ValidateTextureImportRoleMatrix.py",)),
    ValidatorCommand("texture_role_technical_ledger", ("Tools/ValidateTextureRoleTechnicalLedger.py", "--no-fail")),
    ValidatorCommand("batch31_local_pbr_import_intent_artifacts", ("Tools/ValidateBatch31LocalPbrImportIntentArtifacts.py",)),
    ValidatorCommand("batch31_promotion_prep_artifacts", ("Tools/ValidateBatch31PromotionPrepArtifacts.py",)),
    ValidatorCommand("vfx_particle_budget_catalog", ("Tools/ValidateVfxParticleBudgetCatalog.py",)),
    ValidatorCommand("visual_asset_review_queue", ("Tools/ValidateVisualAssetReviewQueue.py",)),
    ValidatorCommand("visual_proof_capture_guardrails", ("Tools/ValidateVisualProofCaptureGuardrails.py",)),
    ValidatorCommand(
        "visual_proof_harness_candidate_quarantine",
        (
            "Tools/ValidateVisualProofCaptureGuardrails.py",
            "--mode",
            "harness-candidate",
            "--allow-diagnostic-rejection",
        ),
    ),
    ValidatorCommand("visual_reference_owner_matrix", ("Tools/ValidateVisualReferenceOwnerMatrix.py",)),
    ValidatorCommand("visual_reference_current_rejection_matrix", ("Tools/ValidateVisualReferenceCurrentRejectionMatrix.py",)),
    ValidatorCommand("visual_source_promotion_queue", ("Tools/ValidateVisualSourcePromotionQueue.py",)),
)


def format_command(command: ValidatorCommand) -> str:
    return " ".join((sys.executable, *command.args))


def run_command(command: ValidatorCommand, root: Path = ROOT) -> int:
    completed = subprocess.run(
        (sys.executable, *command.args),
        cwd=root,
        text=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        check=False,
    )
    output = completed.stdout.strip()
    if output:
        print(output)
    if completed.returncode != 0:
        print(f"FAIL: asset static validator failed: {command.name}")
    return completed.returncode


def run_all(dry_run: bool = False) -> int:
    if dry_run:
        for command in VALIDATOR_COMMANDS:
            print(f"{command.name}: {format_command(command)}")
        return 0

    failed: list[str] = []
    for command in VALIDATOR_COMMANDS:
        if run_command(command) != 0:
            failed.append(command.name)

    if failed:
        print(f"ASSET_STATIC_VALIDATORS_FAILED count={len(failed)} names={','.join(failed)}")
        return 1

    print(f"ASSET_STATIC_VALIDATORS_OK count={len(VALIDATOR_COMMANDS)}")
    return 0


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dry-run", action="store_true", help="Print validator commands without running them.")
    return parser


def main(argv: list[str] | None = None) -> int:
    args = build_parser().parse_args(sys.argv[1:] if argv is None else argv)
    return run_all(dry_run=args.dry_run)


if __name__ == "__main__":
    raise SystemExit(main())
