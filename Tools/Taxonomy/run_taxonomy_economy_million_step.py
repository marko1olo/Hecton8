#!/usr/bin/env python3
"""Taxonomy-side evidence hook for the economy million-step audit.

This does not tune economy data and does not write Unity assets. It reuses the
existing economy Monte Carlo simulation functions, stops only after the script
has actually mined at least 1,000,000 nodes, and writes a compact report for
the XENO_TAXONOMY_WRITER handoff.
"""

from __future__ import annotations

import json
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
ECONOMY_TOOL_DIR = ROOT / "Tools" / "Economy"
REPORT_JSON_PATH = ROOT / "Docs" / "AgentLogs" / "TaxonomyEconomyMillionStep_XENO_TAXONOMY_WRITER.json"
REPORT_MD_PATH = ROOT / "Docs" / "AgentLogs" / "TaxonomyEconomyMillionStep_XENO_TAXONOMY_WRITER.md"
GRAPH_AUDIT_JSON_PATH = ROOT / "Docs" / "Reports" / "Economy_Integrity_Audit_XENO_TAXONOMY_WRITER.json"
COMPAT_JSON_PATH = ROOT / "Docs" / "AgentLogs" / "EconomyMonteCarlo_XENO_TAXONOMY_WRITER_Compatible.json"

sys.path.insert(0, str(ECONOMY_TOOL_DIR))
import MonteCarloEconomySim as economy  # noqa: E402


def load_graph_status() -> dict:
    if not GRAPH_AUDIT_JSON_PATH.exists():
        return {
            "exists": False,
            "cycleCount": None,
            "status": "MISSING",
        }
    data = json.loads(GRAPH_AUDIT_JSON_PATH.read_text(encoding="utf-8-sig"))
    return {
        "exists": True,
        "cycleCount": data.get("graph", {}).get("cycle_count"),
        "isDag": data.get("graph", {}).get("is_dag"),
        "status": data.get("status"),
        "zeroIngredientRecipes": data.get("exploits", {}).get("zero_ingredient_recipes", []),
        "nonpositiveCosts": data.get("exploits", {}).get("nonpositive_costs", []),
        "nonpositiveQuantities": data.get("exploits", {}).get("nonpositive_quantities", []),
    }


def build_markdown(report: dict) -> str:
    status = "PASS" if report["passed"] else "FAIL"
    lines = [
        "# Taxonomy Economy Million-Step Audit",
        "",
        f"Status: {status}",
        f"Players simulated: {report['players']}",
        f"Monte Carlo node steps: {report['summary']['monte_carlo_steps']}",
        f"Million-step gate: {report['summary']['million_step_audit_passed']}",
        f"Failures: {report['summary']['failures']}",
        f"Recipe graph cycle count: {report['graphAudit'].get('cycleCount')}",
        f"Graph audit status: {report['graphAudit'].get('status')}",
        "",
        "Scope: first-base raw demand only. This proves the sampled resource path does not dead-end and the recipe graph has no cycles in the checked data. Runtime balance still needs gameplay telemetry.",
    ]
    return "\n".join(lines) + "\n"


def main() -> int:
    root = ROOT
    distribution = economy.load_distribution(root)
    recipes = economy.load_recipes(root)
    demands = economy.resolve_raw_demands(recipes, economy.FIRST_BASE_TARGETS)
    dependency_gaps = economy.validate_dependency_coverage(demands, distribution)
    if dependency_gaps:
        raise SystemExit(f"dependency gaps: {dependency_gaps}")

    node_minutes, overhead_minutes, time_model_source = economy.load_time_model(root, demands)
    compact = economy.build_compact_distribution(distribution, demands)
    results = []
    total_nodes = 0
    player_index = 0
    max_nodes = 10_000

    while total_nodes < economy.MONTE_CARLO_STEP_FLOOR:
        result = economy.simulate_player_compact(
            player_index,
            compact,
            economy.DEFAULT_WORLD_SEED,
            max_nodes,
            node_minutes,
            overhead_minutes,
        )
        results.append(result)
        total_nodes += result.nodes_to_base
        player_index += 1

    summary = economy.summarize_results(results)
    graph_status = load_graph_status()
    passed = (
        summary["million_step_audit_passed"] is True
        and summary["failures"] == 0
        and graph_status.get("cycleCount") == 0
        and not graph_status.get("zeroIngredientRecipes")
        and not graph_status.get("nonpositiveCosts")
        and not graph_status.get("nonpositiveQuantities")
    )

    report = {
        "agent": "XENO_TAXONOMY_WRITER",
        "schema": "H8.TAXONOMY.ECONOMY_MILLION_STEP.V1",
        "evidenceClass": "CLI_PYTHON_STATIC_DATA",
        "distributionSource": str(distribution.source_path.relative_to(root)).replace("\\", "/"),
        "distributionSourceFormat": distribution.source_format,
        "distributionSourceSha256": distribution.source_sha256,
        "timeModel": time_model_source,
        "players": player_index,
        "maxNodesPerPlayer": max_nodes,
        "rawDemands": dict(sorted(demands.items())),
        "summary": summary,
        "graphAudit": graph_status,
        "passed": passed,
    }

    REPORT_JSON_PATH.parent.mkdir(parents=True, exist_ok=True)
    REPORT_JSON_PATH.write_text(json.dumps(report, indent=2, sort_keys=True), encoding="utf-8")
    REPORT_MD_PATH.write_text(build_markdown(report), encoding="utf-8")
    COMPAT_JSON_PATH.write_text(
        json.dumps(
            {
                "status": "ECONOMY PROVEN" if passed else "PENDING VERIFICATION - ECONOMY RISK",
                "evidence_class": "CLI_PYTHON_STATIC_DATA",
                "source": str(REPORT_JSON_PATH.relative_to(ROOT)).replace("\\", "/"),
                "params": {
                    "players": player_index,
                    "max_nodes": max_nodes,
                    "monte_carlo_step_floor": economy.MONTE_CARLO_STEP_FLOOR,
                    "world_seed": economy.DEFAULT_WORLD_SEED,
                },
                "final_summary": summary,
            },
            indent=2,
            sort_keys=True,
        )
        + "\n",
        encoding="utf-8",
    )

    if not passed:
        print(
            "TAXONOMY ECONOMY MILLION-STEP FAIL "
            f"steps={summary['monte_carlo_steps']} failures={summary['failures']} "
            f"cycles={graph_status.get('cycleCount')}"
        )
        return 1

    print(
        "TAXONOMY ECONOMY MILLION-STEP PASS "
        f"players={player_index} steps={summary['monte_carlo_steps']} "
        f"failures={summary['failures']} cycles={graph_status.get('cycleCount')}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
