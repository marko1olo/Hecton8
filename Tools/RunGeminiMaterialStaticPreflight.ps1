param(
    [switch]$RebuildCatalog
)

$ErrorActionPreference = "Stop"

$projectRoot = Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")
$packValidator = Join-Path $projectRoot "Tools\ValidateExternalPbrPack.py"
$litPreviewValidator = Join-Path $projectRoot "Tools\ValidateExternalPbrLitPreview.py"
$bindingValidator = Join-Path $projectRoot "Tools\ValidateExternalPbrImporterBindings.py"
$geminiStateValidator = Join-Path $projectRoot "Tools\ValidateGeminiGeneratedMaterialState.py"
$geminiUnityApplyRunnerValidator = Join-Path $projectRoot "Tools\ValidateGeminiUnityApplyRunner.py"
$heldToolValidator = Join-Path $projectRoot "Tools\ValidateHeldToolExternalPbrRules.py"
$worldToolValidator = Join-Path $projectRoot "Tools\ValidateWorldToolExternalPbrRules.py"
$worldProxyValidator = Join-Path $projectRoot "Tools\ValidateWorldProxyGeminiBiomeAssignments.py"
$constructionValidator = Join-Path $projectRoot "Tools\ValidateConstructionGeminiMaterialAssignments.py"
$floraImportedValidator = Join-Path $projectRoot "Tools\ValidateGeminiBiomeFloraImportedAssignments.py"
$batch34DirectPromptQueueValidator = Join-Path $projectRoot "Tools\ValidateBatch34DirectPromptQueue.py"
$batch34TargetedPromptQueueValidator = Join-Path $projectRoot "Tools\ValidateBatch34TargetedPromptQueues.py"
$batch34RegenTargetsValidator = Join-Path $projectRoot "Tools\ValidateBatch34RegenTargets.py"
$batch34TextureExpansionIntakeManifestValidator = Join-Path $projectRoot "Tools\ValidateBatch34TextureExpansionIntakeManifest.py"
$batch34SourceAtlasValidator = Join-Path $projectRoot "Tools\ValidateBatch34SourceAtlasPack.py"
$batch34AlphaCandidateValidator = Join-Path $projectRoot "Tools\ValidateBatch34AlphaCandidatePack.py"
$batch34SourceAtlasImporterValidator = Join-Path $projectRoot "Tools\ValidateBatch34SourceAtlasImporter.py"
$batch34TerrainLayerBuilderValidator = Join-Path $projectRoot "Tools\ValidateBatch34TerrainLayerBuilder.py"
$productFacePlayerSuitValidator = Join-Path $projectRoot "Tools\ValidateProductFacePlayerSuitGeminiMaterialRoute.py"
$resourcePickupMaterialValidator = Join-Path $projectRoot "Tools\ValidateResourcePickupGeminiMaterialRoute.py"
$worldSupportMaterialValidator = Join-Path $projectRoot "Tools\ValidateWorldSupportGeminiMaterialRoute.py"
$toolSurfaceDetailValidator = Join-Path $projectRoot "Tools\ValidateToolSurfaceDetailGeminiRoute.py"
$uvAtlasMaterialHandoffValidator = Join-Path $projectRoot "Tools\ValidateBatch34UvAtlasMaterialHandoff.py"
$constructionInsulationValidator = Join-Path $projectRoot "Tools\ValidateConstructionInsulationBackingRoute.py"
$batch34VisorTraumaDecalArrayValidator = Join-Path $projectRoot "Tools\ValidateBatch34VisorTraumaDecalArrayRoute.py"
$batch34VisorTraumaProfileCsvValidator = Join-Path $projectRoot "Tools\ValidateBatch34VisorTraumaProfileCsv.py"
$batch34PaddedAtlasSourcesValidator = Join-Path $projectRoot "Tools\ValidateBatch34PaddedAtlasSources.py"
$batch34SplitAtlasCandidatesValidator = Join-Path $projectRoot "Tools\ValidateBatch34SplitAtlasCandidates.py"
$oopHitboxScanner = Join-Path $projectRoot "Tools\OOP_Hitbox_Scanner.py"
$catalogBuilder = Join-Path $projectRoot "Tools\BuildGeminiMaterialCatalog.py"
$catalogBuilderTest = Join-Path $projectRoot "Tools\test_build_gemini_material_catalog.py"

function Invoke-PbrPackValidation {
    param(
        [string]$ManifestPath,
        [int]$MinSize
    )

    if (-not (Test-Path -LiteralPath $ManifestPath)) {
        throw "External PBR manifest does not exist: $ManifestPath"
    }

    & python -B $packValidator --manifest $ManifestPath --min-size $MinSize
    if ($LASTEXITCODE -ne 0) {
        throw "External PBR pack validation failed. manifest=$ManifestPath"
    }
}

function Invoke-RequiredPythonValidator {
    param([string]$ValidatorPath)

    & python -B $ValidatorPath
    if ($LASTEXITCODE -ne 0) {
        throw "Validator failed: $ValidatorPath"
    }
}

$geminiSingleManifest = Join-Path $projectRoot "Assets\_Project\Art\TEXTURES\Generated\GeminiMaterialIntake_20260607\GeminiSingleMaterials_Manifest.json"
$geminiBiomeManifest = Join-Path $projectRoot "Assets\_Project\Art\TEXTURES\Generated\GeminiBiomeMaterialIntake_20260607\GeminiBiomeMaterials_Manifest.json"
$geminiAtlasRoot = Join-Path $projectRoot "Assets\_Project\Art\TEXTURES\Generated\GeminiMaterialAtlases"

Invoke-PbrPackValidation -ManifestPath $geminiSingleManifest -MinSize 1024
Invoke-PbrPackValidation -ManifestPath $geminiBiomeManifest -MinSize 1024

if (Test-Path -LiteralPath $geminiAtlasRoot) {
    $atlasManifests = Get-ChildItem -LiteralPath $geminiAtlasRoot -Recurse -Filter "GeminiMaterialAtlas_Manifest.json" |
        Sort-Object FullName
    foreach ($atlasManifest in $atlasManifests) {
        Invoke-PbrPackValidation -ManifestPath $atlasManifest.FullName -MinSize 512
    }
}

$litPreviewManifests = @(
    (Join-Path $geminiAtlasRoot "Batch20260607_MicroPanel\GeminiMaterialAtlas_Manifest.json"),
    (Join-Path $geminiAtlasRoot "Batch20260608_TextureExpansion\GeminiMaterialAtlas_Manifest.json")
)
foreach ($litPreviewManifest in $litPreviewManifests) {
    if (-not (Test-Path -LiteralPath $litPreviewManifest)) {
        continue
    }

    & python -B $litPreviewValidator --manifest $litPreviewManifest --tile-size 220 --columns 4
    if ($LASTEXITCODE -ne 0) {
        throw "Gemini lit material preview validation failed: $litPreviewManifest"
    }
}

Invoke-RequiredPythonValidator -ValidatorPath $catalogBuilder
if (Test-Path -LiteralPath $catalogBuilderTest) {
    Invoke-RequiredPythonValidator -ValidatorPath $catalogBuilderTest
}
Invoke-RequiredPythonValidator -ValidatorPath $geminiStateValidator
Invoke-RequiredPythonValidator -ValidatorPath $geminiUnityApplyRunnerValidator
Invoke-RequiredPythonValidator -ValidatorPath $bindingValidator
Invoke-RequiredPythonValidator -ValidatorPath $heldToolValidator
Invoke-RequiredPythonValidator -ValidatorPath $worldToolValidator
Invoke-RequiredPythonValidator -ValidatorPath $worldProxyValidator
Invoke-RequiredPythonValidator -ValidatorPath $constructionValidator
Invoke-RequiredPythonValidator -ValidatorPath $floraImportedValidator
if (Test-Path -LiteralPath $batch34DirectPromptQueueValidator) {
    Invoke-RequiredPythonValidator -ValidatorPath $batch34DirectPromptQueueValidator
}
if (Test-Path -LiteralPath $batch34TargetedPromptQueueValidator) {
    Invoke-RequiredPythonValidator -ValidatorPath $batch34TargetedPromptQueueValidator
}
if (Test-Path -LiteralPath $batch34RegenTargetsValidator) {
    Invoke-RequiredPythonValidator -ValidatorPath $batch34RegenTargetsValidator
}
if (Test-Path -LiteralPath $batch34TextureExpansionIntakeManifestValidator) {
    Invoke-RequiredPythonValidator -ValidatorPath $batch34TextureExpansionIntakeManifestValidator
}
if (Test-Path -LiteralPath $batch34SourceAtlasValidator) {
    Invoke-RequiredPythonValidator -ValidatorPath $batch34SourceAtlasValidator
}
if (Test-Path -LiteralPath $batch34AlphaCandidateValidator) {
    Invoke-RequiredPythonValidator -ValidatorPath $batch34AlphaCandidateValidator
}
if (Test-Path -LiteralPath $batch34SourceAtlasImporterValidator) {
    Invoke-RequiredPythonValidator -ValidatorPath $batch34SourceAtlasImporterValidator
}
if (Test-Path -LiteralPath $batch34TerrainLayerBuilderValidator) {
    Invoke-RequiredPythonValidator -ValidatorPath $batch34TerrainLayerBuilderValidator
}
if (Test-Path -LiteralPath $productFacePlayerSuitValidator) {
    Invoke-RequiredPythonValidator -ValidatorPath $productFacePlayerSuitValidator
}
if (Test-Path -LiteralPath $resourcePickupMaterialValidator) {
    Invoke-RequiredPythonValidator -ValidatorPath $resourcePickupMaterialValidator
}
if (Test-Path -LiteralPath $worldSupportMaterialValidator) {
    Invoke-RequiredPythonValidator -ValidatorPath $worldSupportMaterialValidator
}
if (Test-Path -LiteralPath $toolSurfaceDetailValidator) {
    Invoke-RequiredPythonValidator -ValidatorPath $toolSurfaceDetailValidator
}
if (Test-Path -LiteralPath $uvAtlasMaterialHandoffValidator) {
    Invoke-RequiredPythonValidator -ValidatorPath $uvAtlasMaterialHandoffValidator
}
if (Test-Path -LiteralPath $constructionInsulationValidator) {
    Invoke-RequiredPythonValidator -ValidatorPath $constructionInsulationValidator
}
if (Test-Path -LiteralPath $batch34PaddedAtlasSourcesValidator) {
    Invoke-RequiredPythonValidator -ValidatorPath $batch34PaddedAtlasSourcesValidator
}
if (Test-Path -LiteralPath $batch34SplitAtlasCandidatesValidator) {
    Invoke-RequiredPythonValidator -ValidatorPath $batch34SplitAtlasCandidatesValidator
}
if (Test-Path -LiteralPath $batch34VisorTraumaDecalArrayValidator) {
    Invoke-RequiredPythonValidator -ValidatorPath $batch34VisorTraumaDecalArrayValidator
}
if (Test-Path -LiteralPath $batch34VisorTraumaProfileCsvValidator) {
    Invoke-RequiredPythonValidator -ValidatorPath $batch34VisorTraumaProfileCsvValidator
}
if (Test-Path -LiteralPath $oopHitboxScanner) {
    & python -B $oopHitboxScanner --check-visual-only-coalescing
    if ($LASTEXITCODE -ne 0) {
        throw "OOP visual-only coalescing contract validation failed: $oopHitboxScanner"
    }
}

Write-Host "Gemini material static preflight passed."
