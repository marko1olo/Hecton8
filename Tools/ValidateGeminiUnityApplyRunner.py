#!/usr/bin/env python3
"""Validate the Gemini material Unity apply runner stays one gated Unity batch."""

from __future__ import annotations

import re
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
RUNNER = ROOT / "Tools/RunGeminiMaterialUnityApplyAll.ps1"
STATIC_PREFLIGHT = ROOT / "Tools/RunGeminiMaterialStaticPreflight.ps1"
APPLIER = ROOT / "Assets/_Project/Scripts/Editor/GeminiMaterialIntegrationApplier.cs"
APPLIER_META = APPLIER.with_suffix(APPLIER.suffix + ".meta")
LEGACY_WRAPPERS = (
    ROOT / "Tools/RunExternalPbrUnityImport.ps1",
    ROOT / "Tools/RunWorldProxyGeminiBiomeApply.ps1",
    ROOT / "Tools/RunHeldToolExternalPbrApply.ps1",
    ROOT / "Tools/RunWorldToolExternalPbrApply.ps1",
    ROOT / "Tools/RunConstructionGeminiMaterialApply.ps1",
)
EXPECTED_EXECUTE_METHOD = "Hecton8.EditorTools.GeminiMaterialIntegrationApplier.ApplyAll"
REQUIRED_APPLIER_STAGES = (
    "ExternalPbrTexturePackImporter.ImportExternalPbrTexturePacks();",
    "Batch34SourceAtlasImporter.ImportBatch34SourceAtlases();",
    "WorldSupportGeneratedDecalMaterialBuilder.Build();",
    "Batch34VisorTraumaDecalArrayIntegrator.BakeAndBindVisorTraumaArray();",
    "Batch34TerrainLayerAssetBuilder.BuildTerrainLayers(false);",
    "Hecton8.Editor.ProductFace.ProductFacePlayerSuitGeminiMaterialApplier.Apply(false);",
    "ResourcePickupGeminiMaterialApplier.Apply(false);",
    "WorldSupportGeminiMaterialApplier.Apply(false);",
    "HeldToolExternalPbrMaterialApplier.ApplyExternalPbrToHeldTools(false);",
    "WorldToolExternalPbrMaterialApplier.ApplyWorldToolMaterials(false);",
    "WorldProxyGeminiBiomeMaterialApplier.Apply(false);",
    "ConstructionGeminiMaterialApplier.Apply(false);",
    "ToolSurfaceDetailGeminiIntegrator.Apply();",
    "Batch34UvAtlasMaterialHandoffBuilder.Apply();",
    "ConstructionInsulationBackingIntegrator.Apply();",
)
REQUIRED_POST_APPLY_VALIDATORS = (
    "Invoke-PythonValidator -ValidatorPath $materialAssetValidator",
    'Invoke-PythonValidator -ValidatorPath $heldToolValidator -Arguments @("--post-apply")',
    'Invoke-PythonValidator -ValidatorPath $worldToolValidator -Arguments @("--post-apply")',
    'Invoke-PythonValidator -ValidatorPath $worldProxyValidator -Arguments @("--post-apply")',
    'Invoke-PythonValidator -ValidatorPath $constructionValidator -Arguments @("--post-apply")',
    'Invoke-PythonValidator -ValidatorPath $batch34SourceAtlasImporterValidator -Arguments @("--post-apply")',
    'Invoke-PythonValidator -ValidatorPath $batch34TerrainLayerBuilderValidator -Arguments @("--post-apply")',
    'Invoke-PythonValidator -ValidatorPath $productFacePlayerSuitValidator -Arguments @("--post-apply")',
    'Invoke-PythonValidator -ValidatorPath $resourcePickupMaterialValidator -Arguments @("--post-apply")',
    'Invoke-PythonValidator -ValidatorPath $worldSupportMaterialValidator -Arguments @("--post-apply")',
    'Invoke-PythonValidator -ValidatorPath $toolSurfaceDetailValidator -Arguments @("--post-apply")',
    'Invoke-PythonValidator -ValidatorPath $uvAtlasMaterialHandoffValidator -Arguments @("--post-apply")',
    'Invoke-PythonValidator -ValidatorPath $constructionInsulationValidator -Arguments @("--post-apply")',
    'Invoke-PythonValidator -ValidatorPath $batch34VisorTraumaDecalArrayValidator -Arguments @("--post-apply")',
    "Invoke-PythonValidator -ValidatorPath $batch34VisorTraumaProfileCsvValidator",
)


def display(path: Path) -> str:
    try:
        return path.resolve().relative_to(ROOT).as_posix()
    except ValueError:
        return str(path)


def validate_runner(errors: list[str]) -> None:
    if not RUNNER.exists():
        errors.append(f"missing Unity apply runner: {display(RUNNER)}")
        return

    text = RUNNER.read_text(encoding="utf-8-sig")
    unity_invocations = re.findall(r"(?m)^\s*&\s+\$resolvedUnity\b", text)
    if len(unity_invocations) != 1:
        errors.append(f"runner must invoke Unity exactly once; found={len(unity_invocations)}")

    if f'$executeMethod = "{EXPECTED_EXECUTE_METHOD}"' not in text:
        errors.append(f"runner executeMethod must stay {EXPECTED_EXECUTE_METHOD}")
    if "Unity,'Unity Hub'" not in text:
        errors.append("runner process gate must check both Unity and Unity Hub")
    if "Get-UnityLogIssueSummary" not in text:
        errors.append("runner must summarize Unity log warning/error counts")
    for token in (
        "startUtc=",
        "endUtc=",
        "exitCode=$unityExitCode",
        "warningCount=$($unityLogSummary.WarningCount)",
        "errorCount=$($unityLogSummary.ErrorCount)",
        "logExists=$($unityLogSummary.LogExists)",
        "-or -not $unityLogSummary.LogExists -or $unityLogSummary.ErrorCount -gt 0",
    ):
        if token not in text:
            errors.append(f"runner missing Unity command proof token: {token}")

    forbidden_execute_methods = re.findall(
        r'\$[A-Za-z0-9_]*ExecuteMethod\s*=\s*"Hecton8\.EditorTools\.(?!GeminiMaterialIntegrationApplier\.ApplyAll)[^"]+"',
        text,
    )
    for token in forbidden_execute_methods:
        errors.append(f"runner must not define separate generated-material execute method: {token}")

    order_patterns = (
        r"(?m)^\s*&\s+\$staticPreflightRunner\b",
        r"(?m)^\s*Wait-Or-Assert-Gate\s*$",
        r"(?m)^\s*&\s+\$resolvedUnity\b",
        r"(?m)^\s*Invoke-PythonValidator\s+-ValidatorPath\s+\$materialAssetValidator\b",
    )
    positions = []
    for pattern in order_patterns:
        match = re.search(pattern, text)
        if match is None:
            errors.append(f"runner missing required order pattern: {pattern}")
            positions.append(-1)
        else:
            positions.append(match.start())
    if all(position >= 0 for position in positions) and positions != sorted(positions):
        errors.append("runner order must be static preflight -> gate -> one Unity batch -> post-apply validators")

    unity_match = re.search(r"(?m)^\s*&\s+\$resolvedUnity\b", text)
    unity_position = unity_match.start() if unity_match else -1
    for validator in REQUIRED_POST_APPLY_VALIDATORS:
        position = text.find(validator)
        if position < 0:
            errors.append(f"runner missing required post-apply validator: {validator}")
        elif unity_position >= 0 and position < unity_position:
            errors.append(f"post-apply validator must run after Unity batch: {validator}")


def validate_applier(errors: list[str]) -> None:
    if not APPLIER.exists():
        errors.append(f"missing central applier: {display(APPLIER)}")
        return

    text = APPLIER.read_text(encoding="utf-8-sig")
    positions = []
    for stage in REQUIRED_APPLIER_STAGES:
        position = text.find(stage)
        if position < 0:
            errors.append(f"central applier missing stage: {stage}")
        positions.append(position)
    valid_positions = [position for position in positions if position >= 0]
    if len(valid_positions) == len(positions) and valid_positions != sorted(valid_positions):
        errors.append("central applier stages are out of required order")
    if "TryInvokeStaticEditorTool" in text or "System.Reflection" in text:
        errors.append("central applier must not use reflection for generated material stages")
    if "RunStage(" not in text:
        errors.append("central applier must wrap generated-material stages with named stage diagnostics")
    if "Application.isBatchMode" not in text or "EditorApplication.Exit(1)" not in text:
        errors.append("central applier must exit batchmode with code 1 on stage failure")
    if not APPLIER_META.exists():
        errors.append(f"central applier Unity meta is missing: {display(APPLIER_META)}")
    elif "guid:" not in APPLIER_META.read_text(encoding="utf-8-sig"):
        errors.append(f"central applier Unity meta has no guid: {display(APPLIER_META)}")


def validate_static_preflight(errors: list[str]) -> None:
    if not STATIC_PREFLIGHT.exists():
        errors.append(f"missing static preflight runner: {display(STATIC_PREFLIGHT)}")
        return

    text = STATIC_PREFLIGHT.read_text(encoding="utf-8-sig")
    if "ValidateGeminiUnityApplyRunner.py" not in text:
        errors.append("static preflight must include ValidateGeminiUnityApplyRunner.py")


def validate_legacy_wrappers(errors: list[str]) -> None:
    for wrapper in LEGACY_WRAPPERS:
        if not wrapper.exists():
            errors.append(f"missing compatibility wrapper: {display(wrapper)}")
            continue
        text = wrapper.read_text(encoding="utf-8-sig")
        if "RunGeminiMaterialUnityApplyAll.ps1" not in text:
            errors.append(f"compatibility wrapper must delegate to apply-all runner: {display(wrapper)}")
        if "-executeMethod" in text or "& $resolvedUnity" in text:
            errors.append(f"compatibility wrapper must not launch Unity directly: {display(wrapper)}")


def main() -> int:
    errors: list[str] = []
    validate_runner(errors)
    validate_applier(errors)
    validate_static_preflight(errors)
    validate_legacy_wrappers(errors)

    print("GEMINI_UNITY_APPLY_RUNNER_VALIDATOR")
    print(f"runner={display(RUNNER)}")
    print(f"applier={display(APPLIER)}")
    print(f"errors={len(errors)}")
    for error in errors:
        print(f"ERROR {error}")
    return 1 if errors else 0


if __name__ == "__main__":
    raise SystemExit(main())
