param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
)

$ErrorActionPreference = 'Stop'

function Fail([string]$Message) {
    Write-Error "[MOD_API_STATIC_VALIDATION] $Message"
    exit 1
}

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) {
        Fail $Message
    }
}

function Test-StrictDecimalFloat([string]$Text) {
    $trimmed = $Text.Trim()
    if ($trimmed -notmatch '^-?(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)$') {
        return $false
    }

    $style = [System.Globalization.NumberStyles]::AllowLeadingSign -bor [System.Globalization.NumberStyles]::AllowDecimalPoint
    $culture = [System.Globalization.CultureInfo]::InvariantCulture
    [double]$value = 0.0
    return [double]::TryParse($trimmed, $style, $culture, [ref]$value) -and
        -not [double]::IsInfinity($value) -and
        -not [double]::IsNaN($value) -and
        [math]::Abs($value) -le [single]::MaxValue
}

function Test-StrictDecimalFloatRange([string]$Text, [double]$MinValue, [double]$MaxValue) {
    if (-not (Test-StrictDecimalFloat $Text)) {
        return $false
    }

    $style = [System.Globalization.NumberStyles]::AllowLeadingSign -bor [System.Globalization.NumberStyles]::AllowDecimalPoint
    $culture = [System.Globalization.CultureInfo]::InvariantCulture
    [double]$value = 0.0
    return [double]::TryParse($Text.Trim(), $style, $culture, [ref]$value) -and
        $value -ge $MinValue -and
        $value -le $MaxValue
}

function Test-StrictInt32([string]$Text) {
    $trimmed = $Text.Trim()
    if ($trimmed -notmatch '^-?[0-9]+$') {
        return $false
    }

    [long]$value = 0
    return [long]::TryParse($trimmed, [ref]$value) -and
        $value -ge -[int]::MaxValue -and
        $value -le [int]::MaxValue
}

function Test-StrictInt32Range([string]$Text, [int]$MinValue, [int]$MaxValue) {
    if (-not (Test-StrictInt32 $Text)) {
        return $false
    }

    [long]$value = 0
    return [long]::TryParse($Text.Trim(), [ref]$value) -and
        $value -ge $MinValue -and
        $value -le $MaxValue
}

function Test-StrictUInt32OrHex([string]$Text) {
    $trimmed = $Text.Trim()
    $upper = $trimmed.ToUpperInvariant()
    if ($upper -match '^0X[0-9A-F]{1,8}$') {
        [uint64]$hexValue = 0
        return [uint64]::TryParse($upper.Substring(2), [System.Globalization.NumberStyles]::HexNumber, [System.Globalization.CultureInfo]::InvariantCulture, [ref]$hexValue) -and
            $hexValue -le [uint32]::MaxValue
    }

    if ($trimmed -notmatch '^[0-9]+$') {
        return $false
    }

    [uint64]$decimalValue = 0
    return [uint64]::TryParse($trimmed, [ref]$decimalValue) -and
        $decimalValue -le [uint32]::MaxValue
}

$schemaPath = Join-Path $RepoRoot 'Docs\Modding\Signal_Schema.json'
$contractIndexPath = Join-Path $RepoRoot 'Docs\Modding\README.md'
$specPath = Join-Path $RepoRoot 'Docs\Modding\Mod_API_Specification.md'
$sampleModSpecPath = Join-Path $RepoRoot 'Docs\Modding\Sample_InfiniteO2_Mod.md'
$auditPath = Join-Path $RepoRoot 'Docs\Modding\Signal_Audit_Matrix.md'
$commandAuditPath = Join-Path $RepoRoot 'Docs\Modding\Command_Audit_Matrix.md'
$apiSurfaceAuditPath = Join-Path $RepoRoot 'Docs\Modding\API_Surface_Audit_Matrix.md'
$payloadLayoutAuditPath = Join-Path $RepoRoot 'Docs\Modding\Payload_Layout_Audit_Matrix.md'
$loaderSaveAuditPath = Join-Path $RepoRoot 'Docs\Modding\Loader_Save_Audit_Matrix.md'
$eventSubscriptionAuditPath = Join-Path $RepoRoot 'Docs\Modding\Event_Subscription_Audit_Matrix.md'
$resourceContentAuditPath = Join-Path $RepoRoot 'Docs\Modding\Resource_Content_Audit_Matrix.md'
$sdkAuthoringPlanPath = Join-Path $RepoRoot 'Docs\Modding\SDK_Authoring_Interface_Plan.md'
$sdkProductBlueprintPath = Join-Path $RepoRoot 'Docs\Modding\SDK_Product_Blueprint.md'
$externalStarterKitContractPath = Join-Path $RepoRoot 'Docs\Modding\External_Starter_Kit_File_Contract.md'
$externalStarterKitTemplatePath = Join-Path $RepoRoot 'ModdingSDK\ExternalStarterKit'
$externalStarterKitTemplateLauncherPath = Join-Path $externalStarterKitTemplatePath 'h8mod.ps1'
$externalStarterKitTemplateValidatorPath = Join-Path $externalStarterKitTemplatePath 'Tools\validate_structure.ps1'
$externalStarterKitTemplateReviewManifestBuilderPath = Join-Path $externalStarterKitTemplatePath 'Tools\build_review_manifest.ps1'
$externalStarterKitTemplateSubmissionPackageToolPath = Join-Path $externalStarterKitTemplatePath 'Tools\build_submission_package.ps1'
$externalStarterKitTemplateAllowedOpcodeListToolPath = Join-Path $externalStarterKitTemplatePath 'Tools\list_allowed_opcodes.ps1'
$externalStarterKitTemplateGraphNodeSnippetToolPath = Join-Path $externalStarterKitTemplatePath 'Tools\create_graph_node_snippet.ps1'
$externalStarterKitTemplateGraphNodeApplyToolPath = Join-Path $externalStarterKitTemplatePath 'Tools\apply_graph_node_snippet.ps1'
$externalStarterKitTemplateSettingsRowSnippetToolPath = Join-Path $externalStarterKitTemplatePath 'Tools\create_settings_row_snippet.ps1'
$externalStarterKitTemplateLocaleEntrySnippetToolPath = Join-Path $externalStarterKitTemplatePath 'Tools\create_locale_entry_snippet.ps1'
$externalStarterKitTemplateSettingsRowApplyToolPath = Join-Path $externalStarterKitTemplatePath 'Tools\apply_settings_row_snippet.ps1'
$externalStarterKitTemplateLocaleEntryApplyToolPath = Join-Path $externalStarterKitTemplatePath 'Tools\apply_locale_entry_snippet.ps1'
$externalStarterKitTemplateAssetEntrySnippetToolPath = Join-Path $externalStarterKitTemplatePath 'Tools\create_asset_entry_snippet.ps1'
$externalStarterKitTemplateAssetEntryApplyToolPath = Join-Path $externalStarterKitTemplatePath 'Tools\apply_asset_entry_snippet.ps1'
$externalStarterKitTemplateManifestContractToolPath = Join-Path $externalStarterKitTemplatePath 'Tools\configure_manifest_contract.ps1'
$externalStarterKitTemplateDependenciesToolPath = Join-Path $externalStarterKitTemplatePath 'Tools\configure_dependencies.ps1'
$externalStarterKitTemplateFirstModToolPath = Join-Path $externalStarterKitTemplatePath 'Tools\create_first_mod.ps1'
$externalStarterKitTemplateInstallLocalToolPath = Join-Path $externalStarterKitTemplatePath 'Tools\install_local_mod.ps1'
$externalStarterKitTemplateDiagnoseLocalToolPath = Join-Path $externalStarterKitTemplatePath 'Tools\diagnose_local_mods.ps1'
$externalStarterKitTemplateDoctorToolPath = Join-Path $externalStarterKitTemplatePath 'Tools\run_doctor.ps1'
$externalStarterKitTemplateIdentityToolPath = Join-Path $externalStarterKitTemplatePath 'Tools\set_mod_identity.ps1'
$externalStarterKitTemplatePrepareToolPath = Join-Path $externalStarterKitTemplatePath 'Tools\prepare_mod.ps1'
$externalStarterKitTemplateStrictJsonIoToolPath = Join-Path $externalStarterKitTemplatePath 'Tools\strict_json_io.ps1'
$externalStarterKitTemplateReviewManifestPath = Join-Path $externalStarterKitTemplatePath 'Reports\review_manifest.json'
$externalStarterKitTemplateVsCodeSettingsPath = Join-Path $externalStarterKitTemplatePath '.vscode\settings.json'
$externalStarterKitTemplateVsCodeTasksPath = Join-Path $externalStarterKitTemplatePath '.vscode\tasks.json'
$changeControlChecklistPath = Join-Path $RepoRoot 'Docs\Modding\Change_Control_Checklist.md'
$runtimePlaybookPath = Join-Path $RepoRoot 'Docs\Modding\Runtime_Verification_Playbook.md'
$signalSourceDirectoryPath = Join-Path $RepoRoot 'Assets\_Project\Scripts\Core\Signals'
$signalSourceFilePattern = 'GlobalSignalPayloads*.cs'
$projectionPath = Join-Path $RepoRoot 'Assets\_Project\Scripts\ModdingAPI\ModEventProjectionBridge.cs'
$commandDispatcherPath = Join-Path $RepoRoot 'Assets\_Project\Scripts\ModdingAPI\ModCommandDispatcher.cs'
$futureCommandSandboxPath = Join-Path $RepoRoot 'Assets\_Project\Scripts\ModdingAPI\FutureCommandSandboxValidator.cs'
$modApiSandboxTunerWindowPath = Join-Path $RepoRoot 'Assets\_Project\Scripts\ModdingAPI\Editor\ModApiSandboxTunerWindow.cs'
$modKernelInspectorWindowPath = Join-Path $RepoRoot 'Assets\_Project\Scripts\ModdingAPI\Editor\ModKernelInspectorWindow.cs'
$allowedOpcodesCsvPath = Join-Path $RepoRoot 'Docs\Modding\allowed_opcodes.csv'
$kernelTuningProfilesCsvPath = Join-Path $RepoRoot 'Docs\Modding\kernel_tuning_profiles.csv'
$hectonApiPath = Join-Path $RepoRoot 'Assets\_Project\Scripts\ModdingAPI\HectonAPI.cs'
$hectonEventBusPath = Join-Path $RepoRoot 'Assets\_Project\Scripts\ModdingAPI\HectonEventBus.cs'
$hectonGameEventsPath = Join-Path $RepoRoot 'Assets\_Project\Scripts\ModdingAPI\HectonGameEvents.cs'
$eventContractsPath = Join-Path $RepoRoot 'Assets\_Project\Scripts\ModdingAPI\ModEventContracts.cs'
$interactionEventsPath = Join-Path $RepoRoot 'Assets\_Project\Scripts\Interaction\InteractionEvents.cs'
$craftingEventsPath = Join-Path $RepoRoot 'Assets\_Project\Scripts\CraftingEvents.cs'
$resourceProxyPath = Join-Path $RepoRoot 'Assets\_Project\Scripts\ModdingAPI\IModResourceProxy.cs'
$modAssetManagerPath = Join-Path $RepoRoot 'Assets\_Project\Scripts\ModdingAPI\ModAssetManager.cs'
$spatialContractsPath = Join-Path $RepoRoot 'Assets\_Project\Scripts\ModdingAPI\ModSpatialContracts.cs'
$modLoaderPath = Join-Path $RepoRoot 'Assets\_Project\Scripts\ModdingAPI\ModLoader.cs'
$modBuilderWindowPath = Join-Path $RepoRoot 'Assets\_Project\Scripts\Editor\ModdingSDK\ModBuilderWindow.cs'
$moddingSdkHubPath = Join-Path $RepoRoot 'Assets\_Project\Scripts\Editor\ModdingSDK\ModdingSdkHubWindow.cs'
$externalStarterKitWorkbenchPath = Join-Path $RepoRoot 'Assets\_Project\Scripts\Editor\ModdingSDK\ExternalStarterKitWorkbenchWindow.cs'
$iHectonModPath = Join-Path $RepoRoot 'Assets\_Project\Scripts\ModdingAPI\IHectonMod.cs'
$modMetadataPath = Join-Path $RepoRoot 'Assets\_Project\Scripts\ModdingAPI\ModMetadata.cs'
$modRuntimeInfoPath = Join-Path $RepoRoot 'Assets\_Project\Scripts\ModdingAPI\ModRuntimeInfo.cs'
$modRegistryEventsPath = Join-Path $RepoRoot 'Assets\_Project\Scripts\ModdingAPI\ModRegistryEvents.cs'
$modSettingsRegistryPath = Join-Path $RepoRoot 'Assets\_Project\Scripts\ModdingAPI\ModSettingsRegistry.cs'
$modRuntimeStatePath = Join-Path $RepoRoot 'Assets\_Project\Scripts\ModdingAPI\ModRuntimeState.cs'
$modWorldPersistenceManagerPath = Join-Path $RepoRoot 'Assets\_Project\Scripts\ModdingAPI\ModWorldPersistenceManager.cs'
$globalRegistryPath = Join-Path $RepoRoot 'Assets\_Project\Scripts\Core\GlobalRegistry.cs'
$modMenuModEntryViewPath = Join-Path $RepoRoot 'Assets\_Project\Scripts\ModdingAPI\ModMenuModEntryView.cs'
$modMenuSettingToggleViewPath = Join-Path $RepoRoot 'Assets\_Project\Scripts\ModdingAPI\ModMenuSettingToggleView.cs'
$modMenuSettingSliderViewPath = Join-Path $RepoRoot 'Assets\_Project\Scripts\ModdingAPI\ModMenuSettingSliderView.cs'
$modMenuUiControllerPath = Join-Path $RepoRoot 'Assets\_Project\Scripts\ModdingAPI\ModMenuUIController.cs'
$fabricatorPath = Join-Path $RepoRoot 'Assets\_Project\Scripts\Fabricator.cs'
$saveBinaryStoragePath = Join-Path $RepoRoot 'Assets\_Project\Scripts\SaveBinaryStorage.cs'
$saveBinaryPayloadCodecPath = Join-Path $RepoRoot 'Assets\_Project\Scripts\SaveBinaryPayloadCodec.cs'

Assert-True (Test-Path -LiteralPath $schemaPath) "Missing schema: $schemaPath"
Assert-True (Test-Path -LiteralPath $contractIndexPath) "Missing modding contract index: $contractIndexPath"
Assert-True (Test-Path -LiteralPath $specPath) "Missing spec: $specPath"
Assert-True (Test-Path -LiteralPath $sampleModSpecPath) "Missing sample mod spec: $sampleModSpecPath"
Assert-True (Test-Path -LiteralPath $auditPath) "Missing audit matrix: $auditPath"
Assert-True (Test-Path -LiteralPath $commandAuditPath) "Missing command audit matrix: $commandAuditPath"
Assert-True (Test-Path -LiteralPath $apiSurfaceAuditPath) "Missing API surface audit matrix: $apiSurfaceAuditPath"
Assert-True (Test-Path -LiteralPath $payloadLayoutAuditPath) "Missing payload layout audit matrix: $payloadLayoutAuditPath"
Assert-True (Test-Path -LiteralPath $loaderSaveAuditPath) "Missing loader/save audit matrix: $loaderSaveAuditPath"
Assert-True (Test-Path -LiteralPath $eventSubscriptionAuditPath) "Missing event subscription audit matrix: $eventSubscriptionAuditPath"
Assert-True (Test-Path -LiteralPath $resourceContentAuditPath) "Missing resource/content audit matrix: $resourceContentAuditPath"
Assert-True (Test-Path -LiteralPath $sdkAuthoringPlanPath) "Missing SDK authoring plan: $sdkAuthoringPlanPath"
Assert-True (Test-Path -LiteralPath $sdkProductBlueprintPath) "Missing SDK product blueprint: $sdkProductBlueprintPath"
Assert-True (Test-Path -LiteralPath $externalStarterKitContractPath) "Missing external starter kit file contract: $externalStarterKitContractPath"
Assert-True (Test-Path -LiteralPath $externalStarterKitTemplatePath -PathType Container) "Missing external starter kit template: $externalStarterKitTemplatePath"
Assert-True (Test-Path -LiteralPath $externalStarterKitTemplateLauncherPath -PathType Leaf) "Missing external starter kit root launcher: $externalStarterKitTemplateLauncherPath"
Assert-True (Test-Path -LiteralPath $externalStarterKitTemplateValidatorPath -PathType Leaf) "Missing external starter kit template validator: $externalStarterKitTemplateValidatorPath"
Assert-True (Test-Path -LiteralPath $externalStarterKitTemplateReviewManifestBuilderPath -PathType Leaf) "Missing external starter kit review manifest builder: $externalStarterKitTemplateReviewManifestBuilderPath"
Assert-True (Test-Path -LiteralPath $externalStarterKitTemplateSubmissionPackageToolPath -PathType Leaf) "Missing external starter kit submission package tool: $externalStarterKitTemplateSubmissionPackageToolPath"
Assert-True (Test-Path -LiteralPath $externalStarterKitTemplateAllowedOpcodeListToolPath -PathType Leaf) "Missing external starter kit allowed opcode list tool: $externalStarterKitTemplateAllowedOpcodeListToolPath"
Assert-True (Test-Path -LiteralPath $externalStarterKitTemplateGraphNodeSnippetToolPath -PathType Leaf) "Missing external starter kit graph node snippet tool: $externalStarterKitTemplateGraphNodeSnippetToolPath"
Assert-True (Test-Path -LiteralPath $externalStarterKitTemplateGraphNodeApplyToolPath -PathType Leaf) "Missing external starter kit graph node apply tool: $externalStarterKitTemplateGraphNodeApplyToolPath"
Assert-True (Test-Path -LiteralPath $externalStarterKitTemplateSettingsRowSnippetToolPath -PathType Leaf) "Missing external starter kit settings row snippet tool: $externalStarterKitTemplateSettingsRowSnippetToolPath"
Assert-True (Test-Path -LiteralPath $externalStarterKitTemplateLocaleEntrySnippetToolPath -PathType Leaf) "Missing external starter kit locale entry snippet tool: $externalStarterKitTemplateLocaleEntrySnippetToolPath"
Assert-True (Test-Path -LiteralPath $externalStarterKitTemplateSettingsRowApplyToolPath -PathType Leaf) "Missing external starter kit settings row apply tool: $externalStarterKitTemplateSettingsRowApplyToolPath"
Assert-True (Test-Path -LiteralPath $externalStarterKitTemplateLocaleEntryApplyToolPath -PathType Leaf) "Missing external starter kit locale entry apply tool: $externalStarterKitTemplateLocaleEntryApplyToolPath"
Assert-True (Test-Path -LiteralPath $externalStarterKitTemplateAssetEntrySnippetToolPath -PathType Leaf) "Missing external starter kit asset entry snippet tool: $externalStarterKitTemplateAssetEntrySnippetToolPath"
Assert-True (Test-Path -LiteralPath $externalStarterKitTemplateAssetEntryApplyToolPath -PathType Leaf) "Missing external starter kit asset entry apply tool: $externalStarterKitTemplateAssetEntryApplyToolPath"
Assert-True (Test-Path -LiteralPath $externalStarterKitTemplateManifestContractToolPath -PathType Leaf) "Missing external starter kit manifest contract tool: $externalStarterKitTemplateManifestContractToolPath"
Assert-True (Test-Path -LiteralPath $externalStarterKitTemplateDependenciesToolPath -PathType Leaf) "Missing external starter kit dependency config tool: $externalStarterKitTemplateDependenciesToolPath"
Assert-True (Test-Path -LiteralPath $externalStarterKitTemplateDoctorToolPath -PathType Leaf) "Missing external starter kit package doctor tool: $externalStarterKitTemplateDoctorToolPath"
Assert-True (Test-Path -LiteralPath $externalStarterKitTemplateIdentityToolPath -PathType Leaf) "Missing external starter kit identity tool: $externalStarterKitTemplateIdentityToolPath"
Assert-True (Test-Path -LiteralPath $externalStarterKitTemplatePrepareToolPath -PathType Leaf) "Missing external starter kit prepare tool: $externalStarterKitTemplatePrepareToolPath"
Assert-True (Test-Path -LiteralPath $externalStarterKitTemplateStrictJsonIoToolPath -PathType Leaf) "Missing external starter kit strict JSON I/O helper: $externalStarterKitTemplateStrictJsonIoToolPath"
Assert-True (Test-Path -LiteralPath $externalStarterKitTemplateVsCodeSettingsPath -PathType Leaf) "Missing external starter kit VS Code settings: $externalStarterKitTemplateVsCodeSettingsPath"
Assert-True (Test-Path -LiteralPath $changeControlChecklistPath) "Missing change control checklist: $changeControlChecklistPath"
Assert-True (Test-Path -LiteralPath $runtimePlaybookPath) "Missing runtime verification playbook: $runtimePlaybookPath"
Assert-True (Test-Path -LiteralPath $signalSourceDirectoryPath) "Missing signal source directory: $signalSourceDirectoryPath"
Assert-True (Test-Path -LiteralPath $projectionPath) "Missing projection bridge: $projectionPath"
Assert-True (Test-Path -LiteralPath $commandDispatcherPath) "Missing command dispatcher: $commandDispatcherPath"
Assert-True (Test-Path -LiteralPath $futureCommandSandboxPath) "Missing future command sandbox validator: $futureCommandSandboxPath"
Assert-True (Test-Path -LiteralPath $externalStarterKitWorkbenchPath) "Missing external starter kit workbench: $externalStarterKitWorkbenchPath"
Assert-True (Test-Path -LiteralPath $modApiSandboxTunerWindowPath) "Missing mod API sandbox tuner window: $modApiSandboxTunerWindowPath"
Assert-True (Test-Path -LiteralPath $modKernelInspectorWindowPath) "Missing mod kernel inspector window: $modKernelInspectorWindowPath"
Assert-True (Test-Path -LiteralPath $allowedOpcodesCsvPath) "Missing allowed opcode CSV: $allowedOpcodesCsvPath"
Assert-True (Test-Path -LiteralPath $kernelTuningProfilesCsvPath) "Missing kernel tuning profiles CSV: $kernelTuningProfilesCsvPath"
Assert-True (Test-Path -LiteralPath $hectonApiPath) "Missing HectonAPI facade: $hectonApiPath"
Assert-True (Test-Path -LiteralPath $hectonEventBusPath) "Missing HectonEventBus source: $hectonEventBusPath"
Assert-True (Test-Path -LiteralPath $hectonGameEventsPath) "Missing HectonGameEvents source: $hectonGameEventsPath"
Assert-True (Test-Path -LiteralPath $eventContractsPath) "Missing event contracts: $eventContractsPath"
Assert-True (Test-Path -LiteralPath $interactionEventsPath) "Missing interaction event source for native byte payload audit: $interactionEventsPath"
Assert-True (Test-Path -LiteralPath $craftingEventsPath) "Missing crafting event source for native byte payload audit: $craftingEventsPath"
Assert-True (Test-Path -LiteralPath $resourceProxyPath) "Missing resource proxy source: $resourceProxyPath"
Assert-True (Test-Path -LiteralPath $modAssetManagerPath) "Missing mod asset manager source: $modAssetManagerPath"
Assert-True (Test-Path -LiteralPath $spatialContractsPath) "Missing spatial contracts: $spatialContractsPath"
Assert-True (Test-Path -LiteralPath $modLoaderPath) "Missing mod loader: $modLoaderPath"
Assert-True (Test-Path -LiteralPath $modBuilderWindowPath) "Missing mod builder window: $modBuilderWindowPath"
Assert-True (Test-Path -LiteralPath $moddingSdkHubPath) "Missing modding SDK hub window: $moddingSdkHubPath"
Assert-True (Test-Path -LiteralPath $iHectonModPath) "Missing IHectonMod contract: $iHectonModPath"
Assert-True (Test-Path -LiteralPath $modMetadataPath) "Missing mod metadata contract: $modMetadataPath"
Assert-True (Test-Path -LiteralPath $modRuntimeInfoPath) "Missing mod runtime info contract: $modRuntimeInfoPath"
Assert-True (Test-Path -LiteralPath $modRegistryEventsPath) "Missing mod registry events source: $modRegistryEventsPath"
Assert-True (Test-Path -LiteralPath $modSettingsRegistryPath) "Missing mod settings registry source: $modSettingsRegistryPath"
Assert-True (Test-Path -LiteralPath $modRuntimeStatePath) "Missing mod runtime state source: $modRuntimeStatePath"
Assert-True (Test-Path -LiteralPath $modWorldPersistenceManagerPath) "Missing mod world persistence manager source: $modWorldPersistenceManagerPath"
Assert-True (Test-Path -LiteralPath $globalRegistryPath) "Missing GlobalRegistry source: $globalRegistryPath"
Assert-True (Test-Path -LiteralPath $modMenuModEntryViewPath) "Missing mod menu entry view source: $modMenuModEntryViewPath"
Assert-True (Test-Path -LiteralPath $modMenuSettingToggleViewPath) "Missing mod menu toggle setting view source: $modMenuSettingToggleViewPath"
Assert-True (Test-Path -LiteralPath $modMenuSettingSliderViewPath) "Missing mod menu slider setting view source: $modMenuSettingSliderViewPath"
Assert-True (Test-Path -LiteralPath $modMenuUiControllerPath) "Missing mod menu UI controller source: $modMenuUiControllerPath"
Assert-True (Test-Path -LiteralPath $fabricatorPath) "Missing fabricator source for mod registry listener check: $fabricatorPath"
Assert-True (Test-Path -LiteralPath $saveBinaryStoragePath) "Missing save binary storage source: $saveBinaryStoragePath"
Assert-True (Test-Path -LiteralPath $saveBinaryPayloadCodecPath) "Missing save binary payload codec source: $saveBinaryPayloadCodecPath"

$schema = Get-Content -Raw -LiteralPath $schemaPath | ConvertFrom-Json
$contractIndexText = Get-Content -Raw -LiteralPath $contractIndexPath
$sampleModSpecText = Get-Content -Raw -LiteralPath $sampleModSpecPath
$signalSourceFiles = @(Get-ChildItem -LiteralPath $signalSourceDirectoryPath -Filter $signalSourceFilePattern -File | Sort-Object FullName)
Assert-True ($signalSourceFiles.Count -gt 0) "Missing signal source files: $signalSourceDirectoryPath\$signalSourceFilePattern"
$signalSource = ($signalSourceFiles | ForEach-Object { Get-Content -Raw -LiteralPath $_.FullName }) -join "`n"
$projectionSource = Get-Content -Raw -LiteralPath $projectionPath
$commandDispatcherSource = Get-Content -Raw -LiteralPath $commandDispatcherPath
$futureCommandSandboxSource = Get-Content -Raw -LiteralPath $futureCommandSandboxPath
$modApiSandboxTunerWindowSource = Get-Content -Raw -LiteralPath $modApiSandboxTunerWindowPath
$modKernelInspectorWindowSource = Get-Content -Raw -LiteralPath $modKernelInspectorWindowPath
$allowedOpcodesCsvText = Get-Content -Raw -LiteralPath $allowedOpcodesCsvPath
$kernelTuningProfilesCsvText = Get-Content -Raw -LiteralPath $kernelTuningProfilesCsvPath
$hectonApiSource = Get-Content -Raw -LiteralPath $hectonApiPath
$hectonEventBusSource = Get-Content -Raw -LiteralPath $hectonEventBusPath
$hectonGameEventsSource = Get-Content -Raw -LiteralPath $hectonGameEventsPath
$eventContractsSource = Get-Content -Raw -LiteralPath $eventContractsPath
$interactionEventsSource = Get-Content -Raw -LiteralPath $interactionEventsPath
$craftingEventsSource = Get-Content -Raw -LiteralPath $craftingEventsPath
$resourceProxySource = Get-Content -Raw -LiteralPath $resourceProxyPath
$modAssetManagerSource = Get-Content -Raw -LiteralPath $modAssetManagerPath
$spatialContractsSource = Get-Content -Raw -LiteralPath $spatialContractsPath
$modLoaderSource = Get-Content -Raw -LiteralPath $modLoaderPath
$modBuilderWindowSource = Get-Content -Raw -LiteralPath $modBuilderWindowPath
$moddingSdkHubSource = Get-Content -Raw -LiteralPath $moddingSdkHubPath
$iHectonModSource = Get-Content -Raw -LiteralPath $iHectonModPath
$modMetadataSource = Get-Content -Raw -LiteralPath $modMetadataPath
$modRuntimeInfoSource = Get-Content -Raw -LiteralPath $modRuntimeInfoPath
$modRegistryEventsSource = Get-Content -Raw -LiteralPath $modRegistryEventsPath
$modSettingsRegistrySource = Get-Content -Raw -LiteralPath $modSettingsRegistryPath
$modRuntimeStateSource = Get-Content -Raw -LiteralPath $modRuntimeStatePath
$modWorldPersistenceManagerSource = Get-Content -Raw -LiteralPath $modWorldPersistenceManagerPath
$globalRegistrySource = Get-Content -Raw -LiteralPath $globalRegistryPath
$modMenuModEntryViewSource = Get-Content -Raw -LiteralPath $modMenuModEntryViewPath
$modMenuSettingToggleViewSource = Get-Content -Raw -LiteralPath $modMenuSettingToggleViewPath
$modMenuSettingSliderViewSource = Get-Content -Raw -LiteralPath $modMenuSettingSliderViewPath
$modMenuUiControllerSource = Get-Content -Raw -LiteralPath $modMenuUiControllerPath
$fabricatorSource = Get-Content -Raw -LiteralPath $fabricatorPath
$saveBinaryStorageSource = Get-Content -Raw -LiteralPath $saveBinaryStoragePath
$saveBinaryPayloadCodecSource = Get-Content -Raw -LiteralPath $saveBinaryPayloadCodecPath
$auditText = Get-Content -Raw -LiteralPath $auditPath
$commandAuditText = Get-Content -Raw -LiteralPath $commandAuditPath
$apiSurfaceAuditText = Get-Content -Raw -LiteralPath $apiSurfaceAuditPath
$payloadLayoutAuditText = Get-Content -Raw -LiteralPath $payloadLayoutAuditPath
$loaderSaveAuditText = Get-Content -Raw -LiteralPath $loaderSaveAuditPath
$eventSubscriptionAuditText = Get-Content -Raw -LiteralPath $eventSubscriptionAuditPath
$resourceContentAuditText = Get-Content -Raw -LiteralPath $resourceContentAuditPath
$sdkAuthoringPlanText = Get-Content -Raw -LiteralPath $sdkAuthoringPlanPath
$sdkProductBlueprintText = Get-Content -Raw -LiteralPath $sdkProductBlueprintPath
$externalStarterKitContractText = Get-Content -Raw -LiteralPath $externalStarterKitContractPath
$changeControlChecklistText = Get-Content -Raw -LiteralPath $changeControlChecklistPath
$runtimePlaybookText = Get-Content -Raw -LiteralPath $runtimePlaybookPath
$specText = Get-Content -Raw -LiteralPath $specPath
$externalStarterKitWorkbenchSource = Get-Content -Raw -LiteralPath $externalStarterKitWorkbenchPath
$externalStarterKitTemplateLauncherSource = Get-Content -Raw -LiteralPath $externalStarterKitTemplateLauncherPath
$externalStarterKitTemplateReviewManifestBuilderSource = Get-Content -Raw -LiteralPath $externalStarterKitTemplateReviewManifestBuilderPath
$externalStarterKitTemplateSubmissionPackageToolSource = Get-Content -Raw -LiteralPath $externalStarterKitTemplateSubmissionPackageToolPath
$externalStarterKitTemplateAllowedOpcodeListToolSource = Get-Content -Raw -LiteralPath $externalStarterKitTemplateAllowedOpcodeListToolPath
$externalStarterKitTemplateGraphNodeSnippetToolSource = Get-Content -Raw -LiteralPath $externalStarterKitTemplateGraphNodeSnippetToolPath
$externalStarterKitTemplateGraphNodeApplyToolSource = Get-Content -Raw -LiteralPath $externalStarterKitTemplateGraphNodeApplyToolPath
$externalStarterKitTemplateSettingsRowSnippetToolSource = Get-Content -Raw -LiteralPath $externalStarterKitTemplateSettingsRowSnippetToolPath
$externalStarterKitTemplateLocaleEntrySnippetToolSource = Get-Content -Raw -LiteralPath $externalStarterKitTemplateLocaleEntrySnippetToolPath
$externalStarterKitTemplateSettingsRowApplyToolSource = Get-Content -Raw -LiteralPath $externalStarterKitTemplateSettingsRowApplyToolPath
$externalStarterKitTemplateLocaleEntryApplyToolSource = Get-Content -Raw -LiteralPath $externalStarterKitTemplateLocaleEntryApplyToolPath
$externalStarterKitTemplateAssetEntrySnippetToolSource = Get-Content -Raw -LiteralPath $externalStarterKitTemplateAssetEntrySnippetToolPath
$externalStarterKitTemplateAssetEntryApplyToolSource = Get-Content -Raw -LiteralPath $externalStarterKitTemplateAssetEntryApplyToolPath
$externalStarterKitTemplateManifestContractToolSource = Get-Content -Raw -LiteralPath $externalStarterKitTemplateManifestContractToolPath
$externalStarterKitTemplateDependenciesToolSource = Get-Content -Raw -LiteralPath $externalStarterKitTemplateDependenciesToolPath
$externalStarterKitTemplateFirstModToolSource = Get-Content -Raw -LiteralPath $externalStarterKitTemplateFirstModToolPath
$externalStarterKitTemplateInstallLocalToolSource = Get-Content -Raw -LiteralPath $externalStarterKitTemplateInstallLocalToolPath
$externalStarterKitTemplateDiagnoseLocalToolSource = Get-Content -Raw -LiteralPath $externalStarterKitTemplateDiagnoseLocalToolPath
$externalStarterKitTemplateDoctorToolSource = Get-Content -Raw -LiteralPath $externalStarterKitTemplateDoctorToolPath
$externalStarterKitTemplateIdentityToolSource = Get-Content -Raw -LiteralPath $externalStarterKitTemplateIdentityToolPath
$externalStarterKitTemplatePrepareToolSource = Get-Content -Raw -LiteralPath $externalStarterKitTemplatePrepareToolPath
$externalStarterKitTemplateStrictJsonIoToolSource = Get-Content -Raw -LiteralPath $externalStarterKitTemplateStrictJsonIoToolPath
$externalStarterKitTemplateValidatorSource = Get-Content -Raw -LiteralPath $externalStarterKitTemplateValidatorPath
$externalStarterKitTemplateToolSources = @(Get-ChildItem -LiteralPath (Join-Path $externalStarterKitTemplatePath 'Tools') -Filter '*.ps1' -File | ForEach-Object {
    [pscustomobject]@{
        Name = $_.Name
        Path = $_.FullName
        Source = Get-Content -Raw -LiteralPath $_.FullName
    }
})
$externalStarterKitTemplateValidatorOutput = & powershell -NoProfile -ExecutionPolicy Bypass -File $externalStarterKitTemplateValidatorPath -Root $externalStarterKitTemplatePath
$externalStarterKitTemplateLocalValidatorExitCode = $LASTEXITCODE
$externalStarterKitTemplateLauncherValidateOutput = & powershell -NoProfile -ExecutionPolicy Bypass -File $externalStarterKitTemplateLauncherPath -Action validate
$externalStarterKitTemplateLauncherValidateExitCode = $LASTEXITCODE
$externalStarterKitTemplateReviewManifestOutput = & powershell -NoProfile -ExecutionPolicy Bypass -File $externalStarterKitTemplateReviewManifestBuilderPath -Root $externalStarterKitTemplatePath
$externalStarterKitTemplateReviewManifestExitCode = $LASTEXITCODE
$externalStarterKitTemplateVsCodeSettings = Get-Content -Raw -LiteralPath $externalStarterKitTemplateVsCodeSettingsPath | ConvertFrom-Json
$externalStarterKitTemplateVsCodeTasks = Get-Content -Raw -LiteralPath $externalStarterKitTemplateVsCodeTasksPath | ConvertFrom-Json

function Normalize-TextForCompare([string]$Text) {
    return ($Text -replace "`r`n", "`n").TrimEnd()
}

function Invoke-StarterValidatorHardeningProbe([string]$TemplatePath) {
    $tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'Hecton8ModApiStaticValidation'
    $probeRoot = Join-Path $tempRoot ([System.Guid]::NewGuid().ToString('N'))
    $probeFull = $null
    try {
        [void](New-Item -ItemType Directory -Path $probeRoot -Force)
        Get-ChildItem -LiteralPath $TemplatePath -Force | Copy-Item -Destination $probeRoot -Recurse -Force
        $probeFull = (Resolve-Path -LiteralPath $probeRoot).Path
        $validatorPath = Join-Path $probeFull 'Tools\validate_structure.ps1'
        $utf8NoBom = New-Object System.Text.UTF8Encoding $false

        $authoringPath = Join-Path $probeFull 'mod.h8manifest.json'
        $authoringOriginal = Get-Content -Raw -LiteralPath $authoringPath
        $oversizedPadding = ''.PadRight(66000, 'x')
        $oversizedManifest = '{"Schema":"' + $oversizedPadding + '"}'
        [System.IO.File]::WriteAllText($authoringPath, $oversizedManifest, $utf8NoBom)
        $previousErrorActionPreference = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        try {
            $oversizedOutput = & powershell -NoProfile -ExecutionPolicy Bypass -File $validatorPath -Root $probeFull *>&1
            $oversizedExit = $LASTEXITCODE
        } finally {
            $ErrorActionPreference = $previousErrorActionPreference
        }
        [System.IO.File]::WriteAllText($authoringPath, $authoringOriginal, $utf8NoBom)

        $assetManifestPath = Join-Path $probeFull 'Content\assets.h8manifest.json'
        $assetManifest = [pscustomobject][ordered]@{
            Schema = 'hecton8.assets.draft.v1'
            Assets = @(
                [pscustomobject][ordered]@{
                    Id = 'asset.invalid_path'
                    Kind = 'data_blob'
                    Path = 'Content/Assets/invalid.bytes:ads'
                    Bytes = 0
                    Crc32 = '00000000'
                }
            )
        }
        [System.IO.File]::WriteAllText($assetManifestPath, ($assetManifest | ConvertTo-Json -Depth 8), $utf8NoBom)
        $previousErrorActionPreference = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        try {
            $assetPathOutput = & powershell -NoProfile -ExecutionPolicy Bypass -File $validatorPath -Root $probeFull *>&1
            $assetPathExit = $LASTEXITCODE
        } finally {
            $ErrorActionPreference = $previousErrorActionPreference
        }

        return [pscustomobject]@{
            OversizedManifestExitCode = $oversizedExit
            OversizedManifestOutput = @($oversizedOutput | ForEach-Object { [string]$_ })
            InvalidAssetPathExitCode = $assetPathExit
            InvalidAssetPathOutput = @($assetPathOutput | ForEach-Object { [string]$_ })
        }
    } finally {
        if ($null -ne $probeFull) {
            $tempRootFull = [System.IO.Path]::GetFullPath($tempRoot)
            $probeFullPath = [System.IO.Path]::GetFullPath($probeFull)
            if ($probeFullPath.StartsWith($tempRootFull, [System.StringComparison]::OrdinalIgnoreCase) -and
                (Test-Path -LiteralPath $probeFullPath -PathType Container)) {
                Remove-Item -LiteralPath $probeFullPath -Recurse -Force
            }
        }
    }
}

function Invoke-StarterIdentityToolProbe([string]$TemplatePath) {
    $tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'Hecton8ModApiStaticValidation'
    $probeRoot = Join-Path $tempRoot ([System.Guid]::NewGuid().ToString('N'))
    $probeFull = $null
    try {
        [void](New-Item -ItemType Directory -Path $probeRoot -Force)
        Get-ChildItem -LiteralPath $TemplatePath -Force | Copy-Item -Destination $probeRoot -Recurse -Force
        $probeFull = (Resolve-Path -LiteralPath $probeRoot).Path
        $toolPath = Join-Path $probeFull 'Tools\set_mod_identity.ps1'
        $output = & powershell -NoProfile -ExecutionPolicy Bypass -File $toolPath -Root $probeFull -Id 'com.validation.identity' -DisplayName 'Validation Identity' -Author 'StaticValidator' -Version '9.9.9'
        $exitCode = $LASTEXITCODE
        $authoring = Get-Content -Raw -LiteralPath (Join-Path $probeFull 'mod.h8manifest.json') | ConvertFrom-Json
        $runtime = Get-Content -Raw -LiteralPath (Join-Path $probeFull 'mod.json') | ConvertFrom-Json
        return [pscustomobject]@{
            ExitCode = $exitCode
            Output = @($output)
            AuthoringId = [string]$authoring.Id
            RuntimeId = [string]$runtime.Id
            AuthoringDisplayName = [string]$authoring.DisplayName
            RuntimeName = [string]$runtime.Name
            AuthoringAuthor = [string]$authoring.Author
            RuntimeAuthor = [string]$runtime.Author
            AuthoringVersion = [string]$authoring.Version
            RuntimeVersion = [string]$runtime.Version
        }
    } finally {
        if ($null -ne $probeFull) {
            $tempRootFull = [System.IO.Path]::GetFullPath($tempRoot)
            $probeFullPath = [System.IO.Path]::GetFullPath($probeFull)
            if ($probeFullPath.StartsWith($tempRootFull, [System.StringComparison]::OrdinalIgnoreCase) -and
                (Test-Path -LiteralPath $probeFullPath -PathType Container)) {
                Remove-Item -LiteralPath $probeFullPath -Recurse -Force
            }
        }
    }
}

function Invoke-StarterInvalidVersionProbe([string]$TemplatePath) {
    $tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'Hecton8ModApiStaticValidation'
    $probeRoot = Join-Path $tempRoot ([System.Guid]::NewGuid().ToString('N'))
    $probeFull = $null
    try {
        [void](New-Item -ItemType Directory -Path $probeRoot -Force)
        Get-ChildItem -LiteralPath $TemplatePath -Force | Copy-Item -Destination $probeRoot -Recurse -Force
        $probeFull = (Resolve-Path -LiteralPath $probeRoot).Path
        $toolPath = Join-Path $probeFull 'Tools\set_mod_identity.ps1'
        $output = & powershell -NoProfile -ExecutionPolicy Bypass -File $toolPath -Root $probeFull -Id 'com.validation.badversion' -DisplayName 'Bad Version' -Author 'StaticValidator' -Version 'bad version'
        return [pscustomobject]@{
            ExitCode = $LASTEXITCODE
            Output = @($output)
        }
    } finally {
        if ($null -ne $probeFull) {
            $tempRootFull = [System.IO.Path]::GetFullPath($tempRoot)
            $probeFullPath = [System.IO.Path]::GetFullPath($probeFull)
            if ($probeFullPath.StartsWith($tempRootFull, [System.StringComparison]::OrdinalIgnoreCase) -and
                (Test-Path -LiteralPath $probeFullPath -PathType Container)) {
                Remove-Item -LiteralPath $probeFullPath -Recurse -Force
            }
        }
    }
}

function Invoke-StarterIdentityRollbackProbe([string]$TemplatePath) {
    $tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'Hecton8ModApiStaticValidation'
    $probeRoot = Join-Path $tempRoot ([System.Guid]::NewGuid().ToString('N'))
    $probeFull = $null
    try {
        [void](New-Item -ItemType Directory -Path $probeRoot -Force)
        Get-ChildItem -LiteralPath $TemplatePath -Force | Copy-Item -Destination $probeRoot -Recurse -Force
        $probeFull = (Resolve-Path -LiteralPath $probeRoot).Path
        $authoringPath = Join-Path $probeFull 'mod.h8manifest.json'
        $runtimePath = Join-Path $probeFull 'mod.json'
        $authoringBefore = Get-Content -Raw -LiteralPath $authoringPath | ConvertFrom-Json
        $runtimeBefore = Get-Content -Raw -LiteralPath $runtimePath | ConvertFrom-Json
        Remove-Item -LiteralPath (Join-Path $probeFull 'Reference\allowed_opcodes.csv') -Force
        $toolPath = Join-Path $probeFull 'Tools\set_mod_identity.ps1'
        $output = & powershell -NoProfile -ExecutionPolicy Bypass -File $toolPath -Root $probeFull -Id 'com.validation.rollback' -DisplayName 'Rollback Identity' -Author 'StaticValidator' -Version '9.9.8'
        $exitCode = $LASTEXITCODE
        $authoringAfter = Get-Content -Raw -LiteralPath $authoringPath | ConvertFrom-Json
        $runtimeAfter = Get-Content -Raw -LiteralPath $runtimePath | ConvertFrom-Json
        return [pscustomobject]@{
            ExitCode = $exitCode
            Output = @($output)
            AuthoringBeforeId = [string]$authoringBefore.Id
            RuntimeBeforeId = [string]$runtimeBefore.Id
            AuthoringAfterId = [string]$authoringAfter.Id
            RuntimeAfterId = [string]$runtimeAfter.Id
            AuthoringAfterDisplayName = [string]$authoringAfter.DisplayName
            RuntimeAfterName = [string]$runtimeAfter.Name
        }
    } finally {
        if ($null -ne $probeFull) {
            $tempRootFull = [System.IO.Path]::GetFullPath($tempRoot)
            $probeFullPath = [System.IO.Path]::GetFullPath($probeFull)
            if ($probeFullPath.StartsWith($tempRootFull, [System.StringComparison]::OrdinalIgnoreCase) -and
                (Test-Path -LiteralPath $probeFullPath -PathType Container)) {
                Remove-Item -LiteralPath $probeFullPath -Recurse -Force
            }
        }
    }
}

function Invoke-StarterPrepareToolProbe([string]$TemplatePath) {
    $tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'Hecton8ModApiStaticValidation'
    $probeRoot = Join-Path $tempRoot ([System.Guid]::NewGuid().ToString('N'))
    $probeFull = $null
    try {
        [void](New-Item -ItemType Directory -Path $probeRoot -Force)
        Get-ChildItem -LiteralPath $TemplatePath -Force | Copy-Item -Destination $probeRoot -Recurse -Force
        $probeFull = (Resolve-Path -LiteralPath $probeRoot).Path
        $toolPath = Join-Path $probeFull 'Tools\prepare_mod.ps1'
        $output = & powershell -NoProfile -ExecutionPolicy Bypass -File $toolPath -Root $probeFull -Id 'com.validation.prepared' -DisplayName 'Prepared Validation' -Author 'StaticValidator' -Version '10.0.0'
        $exitCode = $LASTEXITCODE
        $authoring = Get-Content -Raw -LiteralPath (Join-Path $probeFull 'mod.h8manifest.json') | ConvertFrom-Json
        $runtime = Get-Content -Raw -LiteralPath (Join-Path $probeFull 'mod.json') | ConvertFrom-Json
        $review = Get-Content -Raw -LiteralPath (Join-Path $probeFull 'Reports\review_manifest.json') | ConvertFrom-Json
        $reviewPaths = @($review.Files | ForEach-Object { [string]$_.Path })
        $outputExisting = & powershell -NoProfile -ExecutionPolicy Bypass -File $toolPath -Root $probeFull
        $existingExitCode = $LASTEXITCODE
        $reviewExisting = Get-Content -Raw -LiteralPath (Join-Path $probeFull 'Reports\review_manifest.json') | ConvertFrom-Json
        return [pscustomobject]@{
            ExitCode = $exitCode
            Output = @($output)
            ExistingExitCode = $existingExitCode
            ExistingOutput = @($outputExisting)
            AuthoringId = [string]$authoring.Id
            RuntimeId = [string]$runtime.Id
            ReviewRootId = [string]$review.RootId
            ReviewIdentityId = [string]$review.Identity.Id
            ExistingReviewRootId = [string]$reviewExisting.RootId
            ExistingReviewIdentityId = [string]$reviewExisting.Identity.Id
            ReviewIdentityDisplayName = [string]$review.Identity.DisplayName
            ReviewIdentityAuthor = [string]$review.Identity.Author
            ReviewIdentityVersion = [string]$review.Identity.Version
            ReviewIdentityRequiredAPIVersion = [int]$review.Identity.RequiredAPIVersion
            ReviewIdentityModPriority = [int]$review.Identity.ModPriority
            ReviewFileCount = [int]$review.FileCount
            ReviewTotalBytes = [long]$review.TotalBytes
            ReviewMaxFiles = [int]$review.Limits.MaxFiles
            ReviewMaxFileBytes = [int]$review.Limits.MaxFileBytes
            ReviewMaxTotalBytes = [int]$review.Limits.MaxTotalBytes
            ReviewHasPrepareTool = $reviewPaths -contains 'Tools/prepare_mod.ps1'
            ReviewHasSubmissionPackageTool = $reviewPaths -contains 'Tools/build_submission_package.ps1'
            ReviewHasAllowedOpcodeListTool = $reviewPaths -contains 'Tools/list_allowed_opcodes.ps1'
            ReviewHasGraphNodeSnippetTool = $reviewPaths -contains 'Tools/create_graph_node_snippet.ps1'
            ReviewHasGraphNodeApplyTool = $reviewPaths -contains 'Tools/apply_graph_node_snippet.ps1'
            ReviewHasSettingsRowSnippetTool = $reviewPaths -contains 'Tools/create_settings_row_snippet.ps1'
            ReviewHasLocaleEntrySnippetTool = $reviewPaths -contains 'Tools/create_locale_entry_snippet.ps1'
            ReviewHasManifestContractTool = $reviewPaths -contains 'Tools/configure_manifest_contract.ps1'
            ReviewHasDependenciesTool = $reviewPaths -contains 'Tools/configure_dependencies.ps1'
            ReviewExcludesReports = (@($reviewPaths | Where-Object { $_ -like 'Reports/*' -or $_ -like 'Generated/*' }).Count -eq 0)
        }
    } finally {
        if ($null -ne $probeFull) {
            $tempRootFull = [System.IO.Path]::GetFullPath($tempRoot)
            $probeFullPath = [System.IO.Path]::GetFullPath($probeFull)
            if ($probeFullPath.StartsWith($tempRootFull, [System.StringComparison]::OrdinalIgnoreCase) -and
                (Test-Path -LiteralPath $probeFullPath -PathType Container)) {
                Remove-Item -LiteralPath $probeFullPath -Recurse -Force
            }
        }
    }
}

function Invoke-StarterReviewManifestLimitProbe([string]$TemplatePath) {
    $tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'Hecton8ModApiStaticValidation'
    $probeRoot = Join-Path $tempRoot ([System.Guid]::NewGuid().ToString('N'))
    $probeFull = $null
    try {
        [void](New-Item -ItemType Directory -Path $probeRoot -Force)
        Get-ChildItem -LiteralPath $TemplatePath -Force | Copy-Item -Destination $probeRoot -Recurse -Force
        $probeFull = (Resolve-Path -LiteralPath $probeRoot).Path
        $oversizedPath = Join-Path $probeFull 'Content\oversized_review_source.bin'
        $stream = [System.IO.File]::Open($oversizedPath, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
        try {
            $stream.SetLength(4194305)
        } finally {
            $stream.Dispose()
        }

        $toolPath = Join-Path $probeFull 'Tools\build_review_manifest.ps1'
        $output = & powershell -NoProfile -ExecutionPolicy Bypass -File $toolPath -Root $probeFull
        return [pscustomobject]@{
            ExitCode = $LASTEXITCODE
            Output = @($output)
        }
    } finally {
        if ($null -ne $probeFull) {
            $tempRootFull = [System.IO.Path]::GetFullPath($tempRoot)
            $probeFullPath = [System.IO.Path]::GetFullPath($probeFull)
            if ($probeFullPath.StartsWith($tempRootFull, [System.StringComparison]::OrdinalIgnoreCase) -and
                (Test-Path -LiteralPath $probeFullPath -PathType Container)) {
                Remove-Item -LiteralPath $probeFullPath -Recurse -Force
            }
        }
    }
}

function Invoke-StarterInvalidGraphOpcodeProbe([string]$TemplatePath) {
    $tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'Hecton8ModApiStaticValidation'
    $probeRoot = Join-Path $tempRoot ([System.Guid]::NewGuid().ToString('N'))
    $probeFull = $null
    try {
        [void](New-Item -ItemType Directory -Path $probeRoot -Force)
        Get-ChildItem -LiteralPath $TemplatePath -Force | Copy-Item -Destination $probeRoot -Recurse -Force
        $probeFull = (Resolve-Path -LiteralPath $probeRoot).Path

        $authoringPath = Join-Path $probeFull 'mod.h8manifest.json'
        $authoring = Get-Content -Raw -LiteralPath $authoringPath | ConvertFrom-Json
        $authoring.Budgets.MaxEnvelopesPerFrame = 1
        $utf8NoBom = New-Object System.Text.UTF8Encoding $false
        [System.IO.File]::WriteAllText($authoringPath, (($authoring | ConvertTo-Json -Depth 16) + [System.Environment]::NewLine), $utf8NoBom)

        $graphPath = Join-Path $probeFull 'Graphs\main.h8graph.json'
        $graph = Get-Content -Raw -LiteralPath $graphPath | ConvertFrom-Json
        $graph.MaxEnvelopesPerFrame = 1
        $graph.Nodes = @([pscustomobject]@{
            Id = 'bad_opcode'
            Opcode = 'DefinitelyNotAllowed'
        })
        [System.IO.File]::WriteAllText($graphPath, (($graph | ConvertTo-Json -Depth 16) + [System.Environment]::NewLine), $utf8NoBom)

        $toolPath = Join-Path $probeFull 'Tools\validate_structure.ps1'
        $output = & powershell -NoProfile -ExecutionPolicy Bypass -File $toolPath -Root $probeFull
        return [pscustomobject]@{
            ExitCode = $LASTEXITCODE
            Output = @($output)
        }
    } finally {
        if ($null -ne $probeFull) {
            $tempRootFull = [System.IO.Path]::GetFullPath($tempRoot)
            $probeFullPath = [System.IO.Path]::GetFullPath($probeFull)
            if ($probeFullPath.StartsWith($tempRootFull, [System.StringComparison]::OrdinalIgnoreCase) -and
                (Test-Path -LiteralPath $probeFullPath -PathType Container)) {
                Remove-Item -LiteralPath $probeFullPath -Recurse -Force
            }
        }
    }
}

function Invoke-StarterAllowedOpcodeListProbe([string]$TemplatePath) {
    $toolPath = Join-Path $TemplatePath 'Tools\list_allowed_opcodes.ps1'
    $textOutput = & powershell -NoProfile -ExecutionPolicy Bypass -File $toolPath -Root $TemplatePath
    $textExitCode = $LASTEXITCODE
    $jsonOutput = & powershell -NoProfile -ExecutionPolicy Bypass -File $toolPath -Root $TemplatePath -Json
    $jsonExitCode = $LASTEXITCODE
    $jsonText = (@($jsonOutput) -join "`n")
    $payload = $null
    try {
        $payload = $jsonText | ConvertFrom-Json
    } catch {
        $payload = $null
    }

    $opcodeRows = @()
    if ($null -ne $payload) {
        $opcodeRows = @($payload.Opcodes)
    }

    return [pscustomobject]@{
        TextExitCode = $textExitCode
        JsonExitCode = $jsonExitCode
        TextOutput = @($textOutput)
        JsonSchema = if ($null -ne $payload) { [string]$payload.Schema } else { '' }
        JsonRuntime = if ($null -ne $payload) { [string]$payload.Runtime } else { '' }
        JsonCount = if ($null -ne $payload) { [int]$payload.Count } else { 0 }
        HasSpawnItemAlias = (@($opcodeRows | Where-Object { [string]$_.Alias -eq 'SpawnItem' -and [string]$_.Hex -eq '0x3A3DA9C4' }).Count -eq 1)
        HasTextSpawnItem = (@($textOutput | Where-Object { ([string]$_).Contains('SpawnItem') -and ([string]$_).Contains('0x3A3DA9C4') }).Count -gt 0)
    }
}

function Invoke-StarterGraphNodeSnippetProbe([string]$TemplatePath) {
    $tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'Hecton8ModApiStaticValidation'
    $probeRoot = Join-Path $tempRoot ([System.Guid]::NewGuid().ToString('N'))
    $probeFull = $null
    try {
        [void](New-Item -ItemType Directory -Path $probeRoot -Force)
        Get-ChildItem -LiteralPath $TemplatePath -Force | Copy-Item -Destination $probeRoot -Recurse -Force
        $probeFull = (Resolve-Path -LiteralPath $probeRoot).Path
        $toolPath = Join-Path $probeFull 'Tools\create_graph_node_snippet.ps1'
        $textOutput = & powershell -NoProfile -ExecutionPolicy Bypass -File $toolPath -Root $probeFull -Id 'node.spawn_item' -Opcode 'SpawnItem' -ParametersJson '{"Quantity":3,"Item":"demo"}' -Disabled
        $textExitCode = $LASTEXITCODE
        $snippetPath = Join-Path $probeFull 'Generated\graph_node_snippet.json'
        $snippet = $null
        if (Test-Path -LiteralPath $snippetPath -PathType Leaf) {
            $snippet = Get-Content -Raw -LiteralPath $snippetPath | ConvertFrom-Json
        }

        $jsonOutput = & powershell -NoProfile -ExecutionPolicy Bypass -File $toolPath -Root $probeFull -Id 'node.spawn_item_hex' -Opcode '0x3A3DA9C4' -ParametersJson '{"Mode":"hex"}' -Json
        $jsonExitCode = $LASTEXITCODE
        $payload = $null
        try {
            $payload = (@($jsonOutput) -join "`n") | ConvertFrom-Json
        } catch {
            $payload = $null
        }

        $badOutput = & powershell -NoProfile -ExecutionPolicy Bypass -File $toolPath -Root $probeFull -Id 'node.bad' -Opcode 'DefinitelyNotAllowed'
        $badExitCode = $LASTEXITCODE
        $badParametersOutput = & powershell -NoProfile -ExecutionPolicy Bypass -File $toolPath -Root $probeFull -Id 'node.bad_parameters' -Opcode 'SpawnItem' -ParametersJson '[]'
        $badParametersExitCode = $LASTEXITCODE
        $oversizedParameters = 'x' * 8193
        $oversizedParametersOutput = & powershell -NoProfile -ExecutionPolicy Bypass -File $toolPath -Root $probeFull -Id 'node.too_many_parameters' -Opcode 'SpawnItem' -ParametersJson $oversizedParameters
        $oversizedParametersExitCode = $LASTEXITCODE
        $badOutputPathOutput = & powershell -NoProfile -ExecutionPolicy Bypass -File $toolPath -Root $probeFull -Id 'node.bad_output' -Opcode 'SpawnItem' -Output 'Generated/bad.txt'
        $badOutputPathExitCode = $LASTEXITCODE
        $relaxedOutput = & powershell -NoProfile -ExecutionPolicy Bypass -File $toolPath -Root $probeFull -Id 'node.relaxed_parameters' -Opcode 'SpawnItem' -ParametersJson '{Quantity:7,Mode:relaxed}' -Output 'Generated/relaxed_graph_node_snippet.json' -Json
        $relaxedExitCode = $LASTEXITCODE
        $relaxedPayload = $null
        try {
            $relaxedPayload = (@($relaxedOutput) -join "`n") | ConvertFrom-Json
        } catch {
            $relaxedPayload = $null
        }
        $applyToolPath = Join-Path $probeFull 'Tools\apply_graph_node_snippet.ps1'
        $applyOutput = & powershell -NoProfile -ExecutionPolicy Bypass -File $applyToolPath -Root $probeFull -Snippet 'Generated/graph_node_snippet.json' -Json
        $applyExitCode = $LASTEXITCODE
        $applyPayload = $null
        try {
            $applyPayload = (@($applyOutput) -join "`n") | ConvertFrom-Json
        } catch {
            $applyPayload = $null
        }
        $graphAfterApply = Get-Content -Raw -LiteralPath (Join-Path $probeFull 'Graphs\main.h8graph.json') | ConvertFrom-Json
        $authoringAfterApply = Get-Content -Raw -LiteralPath (Join-Path $probeFull 'mod.h8manifest.json') | ConvertFrom-Json
        $duplicateApplyOutput = & powershell -NoProfile -ExecutionPolicy Bypass -File $applyToolPath -Root $probeFull -Snippet 'Generated/graph_node_snippet.json'
        $duplicateApplyExitCode = $LASTEXITCODE

        return [pscustomobject]@{
            TextExitCode = $textExitCode
            TextOutput = @($textOutput)
            SnippetPathExists = Test-Path -LiteralPath $snippetPath -PathType Leaf
            SnippetId = if ($null -ne $snippet) { [string]$snippet.Id } else { '' }
            SnippetOpcode = if ($null -ne $snippet) { [string]$snippet.Opcode } else { '' }
            SnippetEnabled = if ($null -ne $snippet) { [bool]$snippet.Enabled } else { $true }
            SnippetHasParameters = $null -ne $snippet -and $null -ne $snippet.Parameters
            SnippetParameterCount = if ($null -ne $snippet -and $null -ne $snippet.Parameters) { @($snippet.Parameters.PSObject.Properties).Count } else { 0 }
            SnippetParameterQuantity = if ($null -ne $snippet -and $null -ne $snippet.Parameters) { [int]$snippet.Parameters.Quantity } else { 0 }
            JsonExitCode = $jsonExitCode
            JsonSchema = if ($null -ne $payload) { [string]$payload.Schema } else { '' }
            JsonRuntime = if ($null -ne $payload) { [string]$payload.Runtime } else { '' }
            JsonOutput = if ($null -ne $payload) { [string]$payload.Output } else { '' }
            JsonNodeOpcode = if ($null -ne $payload -and $null -ne $payload.Node) { [string]$payload.Node.Opcode } else { '' }
            JsonNodeParameterMode = if ($null -ne $payload -and $null -ne $payload.Node -and $null -ne $payload.Node.Parameters) { [string]$payload.Node.Parameters.Mode } else { '' }
            ApplyExitCode = $applyExitCode
            ApplySchema = if ($null -ne $applyPayload) { [string]$applyPayload.Schema } else { '' }
            ApplyRuntime = if ($null -ne $applyPayload) { [string]$applyPayload.Runtime } else { '' }
            ApplyTarget = if ($null -ne $applyPayload) { [string]$applyPayload.Target } else { '' }
            ApplyManifest = if ($null -ne $applyPayload) { [string]$applyPayload.Manifest } else { '' }
            ApplyNodeId = if ($null -ne $applyPayload) { [string]$applyPayload.NodeId } else { '' }
            ApplyOpcode = if ($null -ne $applyPayload) { [string]$applyPayload.Opcode } else { '' }
            ApplyReplaced = if ($null -ne $applyPayload) { [bool]$applyPayload.Replaced } else { $false }
            ApplyGraphBudget = if ($null -ne $applyPayload) { [int]$applyPayload.GraphBudget } else { 0 }
            ApplyManifestBudget = if ($null -ne $applyPayload) { [int]$applyPayload.ManifestBudget } else { 0 }
            GraphContainsAppliedNode = (@($graphAfterApply.Nodes | Where-Object { [string]$_.Id -eq 'node.spawn_item_hex' -and [string]$_.Opcode -eq '0x3A3DA9C4' -and $null -ne $_.Parameters -and [string]$_.Parameters.Mode -eq 'hex' }).Count -eq 1)
            GraphBudgetAfterApply = [int]$graphAfterApply.MaxEnvelopesPerFrame
            ManifestBudgetAfterApply = [int]$authoringAfterApply.Budgets.MaxEnvelopesPerFrame
            DuplicateApplyExitCode = $duplicateApplyExitCode
            DuplicateApplyOutput = @($duplicateApplyOutput)
            InvalidOpcodeExitCode = $badExitCode
            InvalidOpcodeOutput = @($badOutput)
            InvalidParametersExitCode = $badParametersExitCode
            InvalidParametersOutput = @($badParametersOutput)
            OversizedParametersExitCode = $oversizedParametersExitCode
            OversizedParametersOutput = @($oversizedParametersOutput)
            InvalidOutputPathExitCode = $badOutputPathExitCode
            InvalidOutputPathOutput = @($badOutputPathOutput)
            RelaxedParametersExitCode = $relaxedExitCode
            RelaxedParametersNodeId = if ($null -ne $relaxedPayload -and $null -ne $relaxedPayload.Node) { [string]$relaxedPayload.Node.Id } else { '' }
            RelaxedParametersQuantity = if ($null -ne $relaxedPayload -and $null -ne $relaxedPayload.Node -and $null -ne $relaxedPayload.Node.Parameters) { [int]$relaxedPayload.Node.Parameters.Quantity } else { 0 }
            RelaxedParametersMode = if ($null -ne $relaxedPayload -and $null -ne $relaxedPayload.Node -and $null -ne $relaxedPayload.Node.Parameters) { [string]$relaxedPayload.Node.Parameters.Mode } else { '' }
        }
    } finally {
        if ($null -ne $probeFull) {
            $tempRootFull = [System.IO.Path]::GetFullPath($tempRoot)
            $probeFullPath = [System.IO.Path]::GetFullPath($probeFull)
            if ($probeFullPath.StartsWith($tempRootFull, [System.StringComparison]::OrdinalIgnoreCase) -and
                (Test-Path -LiteralPath $probeFullPath -PathType Container)) {
                Remove-Item -LiteralPath $probeFullPath -Recurse -Force
            }
        }
    }
}

function Invoke-StarterSettingsRowSnippetProbe([string]$TemplatePath) {
    $tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'Hecton8ModApiStaticValidation'
    $probeRoot = Join-Path $tempRoot ([System.Guid]::NewGuid().ToString('N'))
    $probeFull = $null
    try {
        [void](New-Item -ItemType Directory -Path $probeRoot -Force)
        Get-ChildItem -LiteralPath $TemplatePath -Force | Copy-Item -Destination $probeRoot -Recurse -Force
        $probeFull = (Resolve-Path -LiteralPath $probeRoot).Path
        $toolPath = Join-Path $probeFull 'Tools\create_settings_row_snippet.ps1'
        $textOutput = & powershell -NoProfile -ExecutionPolicy Bypass -File $toolPath -Root $probeFull -Id 'setting.validation_toggle' -Kind 'bool' -Default 'true'
        $textExitCode = $LASTEXITCODE
        $snippetPath = Join-Path $probeFull 'Generated\settings_row_snippet.json'
        $snippet = $null
        if (Test-Path -LiteralPath $snippetPath -PathType Leaf) {
            $snippet = Get-Content -Raw -LiteralPath $snippetPath | ConvertFrom-Json
        }

        $jsonOutput = & powershell -NoProfile -ExecutionPolicy Bypass -File $toolPath -Root $probeFull -Id 'setting.validation_float' -Kind 'float' -Default '1.25' -Json
        $jsonExitCode = $LASTEXITCODE
        $payload = $null
        try {
            $payload = (@($jsonOutput) -join "`n") | ConvertFrom-Json
        } catch {
            $payload = $null
        }

        $badKindOutput = & powershell -NoProfile -ExecutionPolicy Bypass -File $toolPath -Root $probeFull -Id 'setting.validation_bad_kind' -Kind 'boolean' -Default 'true'
        $badKindExitCode = $LASTEXITCODE
        $badDefaultOutput = & powershell -NoProfile -ExecutionPolicy Bypass -File $toolPath -Root $probeFull -Id 'setting.validation_bad_default' -Kind 'int' -Default 'abc'
        $badDefaultExitCode = $LASTEXITCODE
        $badOutputPathOutput = & powershell -NoProfile -ExecutionPolicy Bypass -File $toolPath -Root $probeFull -Id 'setting.bad_output_path' -Kind 'bool' -Default 'true' -Output 'Generated//bad.json'
        $badOutputPathExitCode = $LASTEXITCODE
        $applyToolPath = Join-Path $probeFull 'Tools\apply_settings_row_snippet.ps1'
        $applyOutput = & powershell -NoProfile -ExecutionPolicy Bypass -File $applyToolPath -Root $probeFull -Snippet 'Generated/settings_row_snippet.json' -Json
        $applyExitCode = $LASTEXITCODE
        $applyPayload = $null
        try {
            $applyPayload = (@($applyOutput) -join "`n") | ConvertFrom-Json
        } catch {
            $applyPayload = $null
        }

        $settingsTablePath = Join-Path $probeFull 'Tables\settings.h8table.json'
        $settingsTable = $null
        if (Test-Path -LiteralPath $settingsTablePath -PathType Leaf) {
            $settingsTable = Get-Content -Raw -LiteralPath $settingsTablePath | ConvertFrom-Json
        }

        $appliedRows = @()
        if ($null -ne $settingsTable -and $null -ne $settingsTable.Rows) {
            $appliedRows = @($settingsTable.Rows | Where-Object { [string]$_.Id -eq 'setting.validation_float' })
        }

        $duplicateApplyOutput = & powershell -NoProfile -ExecutionPolicy Bypass -File $applyToolPath -Root $probeFull -Snippet 'Generated/settings_row_snippet.json'
        $duplicateApplyExitCode = $LASTEXITCODE
        $invalidSnippetPathOutput = & powershell -NoProfile -ExecutionPolicy Bypass -File $applyToolPath -Root $probeFull -Snippet 'Generated/bad.txt'
        $invalidSnippetPathExitCode = $LASTEXITCODE
        $oversizedSnippetPath = Join-Path $probeFull 'Generated\oversized_settings_row_snippet.json'
        [System.IO.File]::WriteAllText($oversizedSnippetPath, (' ' * 65537), [System.Text.Encoding]::UTF8)
        $oversizedApplyOutput = & powershell -NoProfile -ExecutionPolicy Bypass -File $applyToolPath -Root $probeFull -Snippet 'Generated/oversized_settings_row_snippet.json'
        $oversizedApplyExitCode = $LASTEXITCODE

        return [pscustomobject]@{
            TextExitCode = $textExitCode
            TextOutput = @($textOutput)
            SnippetPathExists = Test-Path -LiteralPath $snippetPath -PathType Leaf
            SnippetId = if ($null -ne $snippet) { [string]$snippet.Id } else { '' }
            SnippetKind = if ($null -ne $snippet) { [string]$snippet.Kind } else { '' }
            SnippetDefault = if ($null -ne $snippet) { $snippet.Default } else { $null }
            JsonExitCode = $jsonExitCode
            JsonSchema = if ($null -ne $payload) { [string]$payload.Schema } else { '' }
            JsonRuntime = if ($null -ne $payload) { [string]$payload.Runtime } else { '' }
            JsonOutput = if ($null -ne $payload) { [string]$payload.Output } else { '' }
            JsonRowKind = if ($null -ne $payload -and $null -ne $payload.Row) { [string]$payload.Row.Kind } else { '' }
            JsonRowDefault = if ($null -ne $payload -and $null -ne $payload.Row) { $payload.Row.Default } else { $null }
            InvalidKindExitCode = $badKindExitCode
            InvalidKindOutput = @($badKindOutput)
            InvalidDefaultExitCode = $badDefaultExitCode
            InvalidDefaultOutput = @($badDefaultOutput)
            InvalidOutputPathExitCode = $badOutputPathExitCode
            InvalidOutputPathOutput = @($badOutputPathOutput)
            ApplyExitCode = $applyExitCode
            ApplySchema = if ($null -ne $applyPayload) { [string]$applyPayload.Schema } else { '' }
            ApplyRuntime = if ($null -ne $applyPayload) { [string]$applyPayload.Runtime } else { '' }
            ApplyTarget = if ($null -ne $applyPayload) { [string]$applyPayload.Target } else { '' }
            ApplySettingId = if ($null -ne $applyPayload) { [string]$applyPayload.SettingId } else { '' }
            ApplyReplaced = if ($null -ne $applyPayload) { [bool]$applyPayload.Replaced } else { $false }
            AppliedRowExists = $appliedRows.Count -eq 1
            DuplicateApplyExitCode = $duplicateApplyExitCode
            DuplicateApplyOutput = @($duplicateApplyOutput)
            InvalidSnippetPathExitCode = $invalidSnippetPathExitCode
            InvalidSnippetPathOutput = @($invalidSnippetPathOutput)
            OversizedApplyExitCode = $oversizedApplyExitCode
            OversizedApplyOutput = @($oversizedApplyOutput)
        }
    } finally {
        if ($null -ne $probeFull) {
            $tempRootFull = [System.IO.Path]::GetFullPath($tempRoot)
            $probeFullPath = [System.IO.Path]::GetFullPath($probeFull)
            if ($probeFullPath.StartsWith($tempRootFull, [System.StringComparison]::OrdinalIgnoreCase) -and
                (Test-Path -LiteralPath $probeFullPath -PathType Container)) {
                Remove-Item -LiteralPath $probeFullPath -Recurse -Force
            }
        }
    }
}

function Invoke-StarterLocaleEntrySnippetProbe([string]$TemplatePath) {
    $tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'Hecton8ModApiStaticValidation'
    $probeRoot = Join-Path $tempRoot ([System.Guid]::NewGuid().ToString('N'))
    $probeFull = $null
    try {
        [void](New-Item -ItemType Directory -Path $probeRoot -Force)
        Get-ChildItem -LiteralPath $TemplatePath -Force | Copy-Item -Destination $probeRoot -Recurse -Force
        $probeFull = (Resolve-Path -LiteralPath $probeRoot).Path
        $toolPath = Join-Path $probeFull 'Tools\create_locale_entry_snippet.ps1'
        $textOutput = & powershell -NoProfile -ExecutionPolicy Bypass -File $toolPath -Root $probeFull -Key 'text.validation_line' -Value 'Validation line'
        $textExitCode = $LASTEXITCODE
        $snippetPath = Join-Path $probeFull 'Generated\locale_entry_snippet.json'
        $snippet = $null
        if (Test-Path -LiteralPath $snippetPath -PathType Leaf) {
            $snippet = Get-Content -Raw -LiteralPath $snippetPath | ConvertFrom-Json
        }

        $jsonOutput = & powershell -NoProfile -ExecutionPolicy Bypass -File $toolPath -Root $probeFull -Key 'text.validation_json' -Value 'Validation JSON line' -Json
        $jsonExitCode = $LASTEXITCODE
        $payload = $null
        try {
            $payload = (@($jsonOutput) -join "`n") | ConvertFrom-Json
        } catch {
            $payload = $null
        }

        $badKeyOutput = & powershell -NoProfile -ExecutionPolicy Bypass -File $toolPath -Root $probeFull -Key 'Text.Bad Key' -Value 'Bad key'
        $badKeyExitCode = $LASTEXITCODE
        $badValueOutput = & powershell -NoProfile -ExecutionPolicy Bypass -File $toolPath -Root $probeFull -Key 'text.bad_value' -Value ' '
        $badValueExitCode = $LASTEXITCODE
        $badOutputPathOutput = & powershell -NoProfile -ExecutionPolicy Bypass -File $toolPath -Root $probeFull -Key 'text.bad_output' -Value 'Bad output' -Output 'Generated/bad.json:ads'
        $badOutputPathExitCode = $LASTEXITCODE
        $applyToolPath = Join-Path $probeFull 'Tools\apply_locale_entry_snippet.ps1'
        $applyOutput = & powershell -NoProfile -ExecutionPolicy Bypass -File $applyToolPath -Root $probeFull -Snippet 'Generated/locale_entry_snippet.json' -Json
        $applyExitCode = $LASTEXITCODE
        $applyPayload = $null
        try {
            $applyPayload = (@($applyOutput) -join "`n") | ConvertFrom-Json
        } catch {
            $applyPayload = $null
        }

        $localePath = Join-Path $probeFull 'Locales\en.h8loc.json'
        $locale = $null
        if (Test-Path -LiteralPath $localePath -PathType Leaf) {
            $locale = Get-Content -Raw -LiteralPath $localePath | ConvertFrom-Json
        }

        $appliedEntryExists = $false
        if ($null -ne $locale -and $null -ne $locale.Strings) {
            $appliedEntryExists = [string]$locale.Strings.'text.validation_json' -eq 'Validation JSON line'
        }

        $duplicateApplyOutput = & powershell -NoProfile -ExecutionPolicy Bypass -File $applyToolPath -Root $probeFull -Snippet 'Generated/locale_entry_snippet.json'
        $duplicateApplyExitCode = $LASTEXITCODE

        return [pscustomobject]@{
            TextExitCode = $textExitCode
            TextOutput = @($textOutput)
            SnippetPathExists = Test-Path -LiteralPath $snippetPath -PathType Leaf
            SnippetKey = if ($null -ne $snippet) { [string]$snippet.Key } else { '' }
            SnippetValue = if ($null -ne $snippet) { [string]$snippet.Value } else { '' }
            JsonExitCode = $jsonExitCode
            JsonSchema = if ($null -ne $payload) { [string]$payload.Schema } else { '' }
            JsonRuntime = if ($null -ne $payload) { [string]$payload.Runtime } else { '' }
            JsonOutput = if ($null -ne $payload) { [string]$payload.Output } else { '' }
            JsonEntryKey = if ($null -ne $payload -and $null -ne $payload.Entry) { [string]$payload.Entry.Key } else { '' }
            InvalidKeyExitCode = $badKeyExitCode
            InvalidKeyOutput = @($badKeyOutput)
            InvalidValueExitCode = $badValueExitCode
            InvalidValueOutput = @($badValueOutput)
            InvalidOutputPathExitCode = $badOutputPathExitCode
            InvalidOutputPathOutput = @($badOutputPathOutput)
            ApplyExitCode = $applyExitCode
            ApplySchema = if ($null -ne $applyPayload) { [string]$applyPayload.Schema } else { '' }
            ApplyRuntime = if ($null -ne $applyPayload) { [string]$applyPayload.Runtime } else { '' }
            ApplyTarget = if ($null -ne $applyPayload) { [string]$applyPayload.Target } else { '' }
            ApplyKey = if ($null -ne $applyPayload) { [string]$applyPayload.Key } else { '' }
            ApplyReplaced = if ($null -ne $applyPayload) { [bool]$applyPayload.Replaced } else { $false }
            AppliedEntryExists = $appliedEntryExists
            DuplicateApplyExitCode = $duplicateApplyExitCode
            DuplicateApplyOutput = @($duplicateApplyOutput)
        }
    } finally {
        if ($null -ne $probeFull) {
            $tempRootFull = [System.IO.Path]::GetFullPath($tempRoot)
            $probeFullPath = [System.IO.Path]::GetFullPath($probeFull)
            if ($probeFullPath.StartsWith($tempRootFull, [System.StringComparison]::OrdinalIgnoreCase) -and
                (Test-Path -LiteralPath $probeFullPath -PathType Container)) {
                Remove-Item -LiteralPath $probeFullPath -Recurse -Force
            }
        }
    }
}

function Invoke-StarterAssetEntrySnippetProbe([string]$TemplatePath) {
    $tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'Hecton8ModApiStaticValidation'
    $probeRoot = Join-Path $tempRoot ([System.Guid]::NewGuid().ToString('N'))
    $probeFull = $null
    try {
        [void](New-Item -ItemType Directory -Path $probeRoot -Force)
        Get-ChildItem -LiteralPath $TemplatePath -Force | Copy-Item -Destination $probeRoot -Recurse -Force
        $probeFull = (Resolve-Path -LiteralPath $probeRoot).Path
        $assetFilePath = Join-Path $probeFull 'Content\Assets\probe.bytes'
        [System.IO.File]::WriteAllBytes($assetFilePath, [byte[]](1, 2, 3, 4, 5))
        $toolPath = Join-Path $probeFull 'Tools\create_asset_entry_snippet.ps1'
        $textOutput = & powershell -NoProfile -ExecutionPolicy Bypass -File $toolPath -Root $probeFull -Id 'asset.validation_blob' -Kind 'data_blob' -Path 'Content/Assets/probe.bytes' -Crc32 'auto' -Bytes -1
        $textExitCode = $LASTEXITCODE
        $snippetPath = Join-Path $probeFull 'Generated\asset_entry_snippet.json'
        $snippet = $null
        if (Test-Path -LiteralPath $snippetPath -PathType Leaf) {
            $snippet = Get-Content -Raw -LiteralPath $snippetPath | ConvertFrom-Json
        }

        $jsonOutput = & powershell -NoProfile -ExecutionPolicy Bypass -File $toolPath -Root $probeFull -Id 'asset.validation_json_blob' -Kind 'data_blob' -Path 'Content/Assets/probe.bytes' -Crc32 'auto' -Bytes -1 -Json
        $jsonExitCode = $LASTEXITCODE
        $payload = $null
        try {
            $payload = (@($jsonOutput) -join "`n") | ConvertFrom-Json
        } catch {
            $payload = $null
        }

        $badKindOutput = & powershell -NoProfile -ExecutionPolicy Bypass -File $toolPath -Root $probeFull -Id 'asset.bad_kind' -Kind 'mesh' -Path 'Content/Assets/probe.bytes' -Crc32 'auto' -Bytes -1
        $badKindExitCode = $LASTEXITCODE
        $badPathOutput = & powershell -NoProfile -ExecutionPolicy Bypass -File $toolPath -Root $probeFull -Id 'asset.bad_path' -Kind 'data_blob' -Path '../probe.bytes' -Crc32 'auto' -Bytes -1
        $badPathExitCode = $LASTEXITCODE
        $badPortablePathOutput = & powershell -NoProfile -ExecutionPolicy Bypass -File $toolPath -Root $probeFull -Id 'asset.bad_portable_path' -Kind 'data_blob' -Path 'Content/Assets/probe.bytes:ads' -Crc32 'auto' -Bytes -1
        $badPortablePathExitCode = $LASTEXITCODE
        $badCrcOutput = & powershell -NoProfile -ExecutionPolicy Bypass -File $toolPath -Root $probeFull -Id 'asset.bad_crc' -Kind 'data_blob' -Path 'Content/Assets/probe.bytes' -Crc32 '00000000' -Bytes -1
        $badCrcExitCode = $LASTEXITCODE
        $badOutputPathOutput = & powershell -NoProfile -ExecutionPolicy Bypass -File $toolPath -Root $probeFull -Id 'asset.bad_output' -Kind 'data_blob' -Path 'Content/Assets/probe.bytes' -Crc32 'auto' -Bytes -1 -Output 'Generated/bad.txt'
        $badOutputPathExitCode = $LASTEXITCODE
        [void](& powershell -NoProfile -ExecutionPolicy Bypass -File $toolPath -Root $probeFull -Id 'asset.validation_blob' -Kind 'data_blob' -Path 'Content/Assets/probe.bytes' -Crc32 'auto' -Bytes -1)
        $applyToolPath = Join-Path $probeFull 'Tools\apply_asset_entry_snippet.ps1'
        $applyOutput = & powershell -NoProfile -ExecutionPolicy Bypass -File $applyToolPath -Root $probeFull -Snippet 'Generated/asset_entry_snippet.json' -Json
        $applyExitCode = $LASTEXITCODE
        $applyPayload = $null
        try {
            $applyPayload = (@($applyOutput) -join "`n") | ConvertFrom-Json
        } catch {
            $applyPayload = $null
        }

        $assetManifestPath = Join-Path $probeFull 'Content\assets.h8manifest.json'
        $assetManifest = $null
        if (Test-Path -LiteralPath $assetManifestPath -PathType Leaf) {
            $assetManifest = Get-Content -Raw -LiteralPath $assetManifestPath | ConvertFrom-Json
        }
        $authoringManifest = Get-Content -Raw -LiteralPath (Join-Path $probeFull 'mod.h8manifest.json') | ConvertFrom-Json
        $appliedEntries = @()
        if ($null -ne $assetManifest -and $null -ne $assetManifest.Assets) {
            $appliedEntries = @($assetManifest.Assets | Where-Object { [string]$_.Id -eq 'asset.validation_blob' })
        }

        $duplicateApplyOutput = & powershell -NoProfile -ExecutionPolicy Bypass -File $applyToolPath -Root $probeFull -Snippet 'Generated/asset_entry_snippet.json'
        $duplicateApplyExitCode = $LASTEXITCODE
        $validatorOutput = & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $probeFull 'Tools\validate_structure.ps1') -Root $probeFull
        $validatorExitCode = $LASTEXITCODE

        return [pscustomobject]@{
            TextExitCode = $textExitCode
            TextOutput = @($textOutput)
            SnippetPathExists = Test-Path -LiteralPath $snippetPath -PathType Leaf
            SnippetId = if ($null -ne $snippet) { [string]$snippet.Id } else { '' }
            SnippetKind = if ($null -ne $snippet) { [string]$snippet.Kind } else { '' }
            SnippetPath = if ($null -ne $snippet) { [string]$snippet.Path } else { '' }
            SnippetBytes = if ($null -ne $snippet) { [long]$snippet.Bytes } else { 0 }
            SnippetCrc32 = if ($null -ne $snippet) { [string]$snippet.Crc32 } else { '' }
            JsonExitCode = $jsonExitCode
            JsonSchema = if ($null -ne $payload) { [string]$payload.Schema } else { '' }
            JsonRuntime = if ($null -ne $payload) { [string]$payload.Runtime } else { '' }
            JsonOutput = if ($null -ne $payload) { [string]$payload.Output } else { '' }
            JsonAssetId = if ($null -ne $payload -and $null -ne $payload.Asset) { [string]$payload.Asset.Id } else { '' }
            InvalidKindExitCode = $badKindExitCode
            InvalidKindOutput = @($badKindOutput)
            InvalidPathExitCode = $badPathExitCode
            InvalidPathOutput = @($badPathOutput)
            InvalidPortablePathExitCode = $badPortablePathExitCode
            InvalidPortablePathOutput = @($badPortablePathOutput)
            InvalidCrcExitCode = $badCrcExitCode
            InvalidCrcOutput = @($badCrcOutput)
            InvalidOutputPathExitCode = $badOutputPathExitCode
            InvalidOutputPathOutput = @($badOutputPathOutput)
            ApplyExitCode = $applyExitCode
            ApplySchema = if ($null -ne $applyPayload) { [string]$applyPayload.Schema } else { '' }
            ApplyRuntime = if ($null -ne $applyPayload) { [string]$applyPayload.Runtime } else { '' }
            ApplyTarget = if ($null -ne $applyPayload) { [string]$applyPayload.Target } else { '' }
            ApplyManifest = if ($null -ne $applyPayload) { [string]$applyPayload.Manifest } else { '' }
            ApplyAssetId = if ($null -ne $applyPayload) { [string]$applyPayload.AssetId } else { '' }
            ApplyKind = if ($null -ne $applyPayload) { [string]$applyPayload.Kind } else { '' }
            ApplyPath = if ($null -ne $applyPayload) { [string]$applyPayload.Path } else { '' }
            ApplyBytes = if ($null -ne $applyPayload) { [long]$applyPayload.Bytes } else { 0 }
            ApplyCrc32 = if ($null -ne $applyPayload) { [string]$applyPayload.Crc32 } else { '' }
            ApplyReplaced = if ($null -ne $applyPayload) { [bool]$applyPayload.Replaced } else { $false }
            ApplyManifestMaxAssetBytes = if ($null -ne $applyPayload) { [long]$applyPayload.ManifestMaxAssetBytes } else { 0 }
            AppliedEntryExists = $appliedEntries.Count -eq 1
            ManifestBudgetAfterApply = [long]$authoringManifest.Budgets.MaxAssetBytes
            DuplicateApplyExitCode = $duplicateApplyExitCode
            DuplicateApplyOutput = @($duplicateApplyOutput)
            ValidatorExitCode = $validatorExitCode
            ValidatorOutput = @($validatorOutput)
        }
    } finally {
        if ($null -ne $probeFull) {
            $tempRootFull = [System.IO.Path]::GetFullPath($tempRoot)
            $probeFullPath = [System.IO.Path]::GetFullPath($probeFull)
            if ($probeFullPath.StartsWith($tempRootFull, [System.StringComparison]::OrdinalIgnoreCase) -and
                (Test-Path -LiteralPath $probeFullPath -PathType Container)) {
                Remove-Item -LiteralPath $probeFullPath -Recurse -Force
            }
        }
    }
}

function Invoke-StarterManifestContractProbe([string]$TemplatePath) {
    $tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'Hecton8ModApiStaticValidation'
    $probeRoot = Join-Path $tempRoot ([System.Guid]::NewGuid().ToString('N'))
    $probeFull = $null
    try {
        [void](New-Item -ItemType Directory -Path $probeRoot -Force)
        Get-ChildItem -LiteralPath $TemplatePath -Force | Copy-Item -Destination $probeRoot -Recurse -Force
        $probeFull = (Resolve-Path -LiteralPath $probeRoot).Path

        $toolPath = Join-Path $probeFull 'Tools\configure_manifest_contract.ps1'
        $jsonOutput = & powershell -NoProfile -ExecutionPolicy Bypass -File $toolPath -Root $probeFull -Capability 'cap.graph.command_draft' -CapabilityState 'enable' -MaxEnvelopesPerFrame 1 -MaxAssetBytes 0 -Json
        $jsonExitCode = $LASTEXITCODE
        $payload = $null
        try {
            $payload = (@($jsonOutput) -join "`n") | ConvertFrom-Json
        } catch {
            $payload = $null
        }

        $manifestPath = Join-Path $probeFull 'mod.h8manifest.json'
        $manifest = $null
        if (Test-Path -LiteralPath $manifestPath -PathType Leaf) {
            $manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json
        }

        $badCapabilityOutput = & powershell -NoProfile -ExecutionPolicy Bypass -File $toolPath -Root $probeFull -Capability 'cap.bad.runtime' -CapabilityState 'enable'
        $badCapabilityExitCode = $LASTEXITCODE

        $launcherPath = Join-Path $probeFull 'h8mod.ps1'
        $launcherOutput = & powershell -NoProfile -ExecutionPolicy Bypass -File $launcherPath -Action manifest-contract -Capability 'cap.settings.table' -CapabilityState 'enable' -MaxEnvelopesPerFrame 1 -MaxAssetBytes 0
        $launcherExitCode = $LASTEXITCODE

        $manifestAfterLauncher = $null
        if (Test-Path -LiteralPath $manifestPath -PathType Leaf) {
            $manifestAfterLauncher = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json
        }

        $validatorOutput = & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $probeFull 'Tools\validate_structure.ps1') -Root $probeFull
        $validatorExitCode = $LASTEXITCODE

        $capabilitiesAfterJson = @()
        if ($null -ne $manifest -and $null -ne $manifest.Capabilities) {
            $capabilitiesAfterJson = @($manifest.Capabilities | ForEach-Object { [string]$_ })
        }

        $capabilitiesAfterLauncher = @()
        if ($null -ne $manifestAfterLauncher -and $null -ne $manifestAfterLauncher.Capabilities) {
            $capabilitiesAfterLauncher = @($manifestAfterLauncher.Capabilities | ForEach-Object { [string]$_ })
        }

        return [pscustomobject]@{
            JsonExitCode = $jsonExitCode
            JsonSchema = if ($null -ne $payload) { [string]$payload.Schema } else { '' }
            JsonRuntime = if ($null -ne $payload) { [string]$payload.Runtime } else { '' }
            JsonCapability = if ($null -ne $payload) { [string]$payload.Capability } else { '' }
            JsonCapabilityState = if ($null -ne $payload) { [string]$payload.CapabilityState } else { '' }
            JsonMaxEnvelopesPerFrame = if ($null -ne $payload) { [int]$payload.MaxEnvelopesPerFrame } else { -1 }
            JsonMaxAssetBytes = if ($null -ne $payload) { [long]$payload.MaxAssetBytes } else { -1 }
            ManifestHasGraphCapability = $capabilitiesAfterJson -contains 'cap.graph.command_draft'
            ManifestMaxEnvelopesPerFrame = if ($null -ne $manifest) { [int]$manifest.Budgets.MaxEnvelopesPerFrame } else { -1 }
            ManifestMaxAssetBytes = if ($null -ne $manifest) { [long]$manifest.Budgets.MaxAssetBytes } else { -1 }
            BadCapabilityExitCode = $badCapabilityExitCode
            BadCapabilityOutput = @($badCapabilityOutput)
            LauncherExitCode = $launcherExitCode
            LauncherOutput = @($launcherOutput)
            LauncherHasSettingsCapability = $capabilitiesAfterLauncher -contains 'cap.settings.table'
            ValidatorExitCode = $validatorExitCode
            ValidatorOutput = @($validatorOutput)
        }
    } finally {
        if ($null -ne $probeFull) {
            $tempRootFull = [System.IO.Path]::GetFullPath($tempRoot)
            $probeFullPath = [System.IO.Path]::GetFullPath($probeFull)
            if ($probeFullPath.StartsWith($tempRootFull, [System.StringComparison]::OrdinalIgnoreCase) -and
                (Test-Path -LiteralPath $probeFullPath -PathType Container)) {
                Remove-Item -LiteralPath $probeFullPath -Recurse -Force
            }
        }
    }
}

function Invoke-StarterSubmissionPackageProbe([string]$TemplatePath) {
    $tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'Hecton8ModApiStaticValidation'
    $probeRoot = Join-Path $tempRoot ([System.Guid]::NewGuid().ToString('N'))
    $probeFull = $null
    try {
        [void](New-Item -ItemType Directory -Path $probeRoot -Force)
        Get-ChildItem -LiteralPath $TemplatePath -Force | Copy-Item -Destination $probeRoot -Recurse -Force
        $probeFull = (Resolve-Path -LiteralPath $probeRoot).Path
        $toolPath = Join-Path $probeFull 'Tools\build_submission_package.ps1'
        $output = & powershell -NoProfile -ExecutionPolicy Bypass -File $toolPath -Root $probeFull
        $exitCode = $LASTEXITCODE
        $zipPath = Join-Path $probeFull 'Generated\com.example.starter_submission.zip'
        $reviewPath = Join-Path $probeFull 'Reports\review_manifest.json'
        $entries = @()
        $zipIsNotOlderThanReview = $false
        if (Test-Path -LiteralPath $zipPath -PathType Leaf) {
            try {
                Add-Type -AssemblyName System.IO.Compression -ErrorAction Stop
                Add-Type -AssemblyName System.IO.Compression.FileSystem -ErrorAction Stop
                $zip = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
                try {
                    $entries = @($zip.Entries | ForEach-Object { [string]$_.FullName })
                } finally {
                    $zip.Dispose()
                }
            } catch {
                $entries = @()
            }
        }
        if ((Test-Path -LiteralPath $zipPath -PathType Leaf) -and (Test-Path -LiteralPath $reviewPath -PathType Leaf)) {
            $zipIsNotOlderThanReview = ((Get-Item -LiteralPath $zipPath).LastWriteTimeUtc -ge (Get-Item -LiteralPath $reviewPath).LastWriteTimeUtc)
        }

        return [pscustomobject]@{
            ExitCode = $exitCode
            Output = @($output)
            ZipExists = Test-Path -LiteralPath $zipPath -PathType Leaf
            Entries = $entries
            HasRuntimeManifest = $entries -contains 'mod.json'
            HasAuthoringManifest = $entries -contains 'mod.h8manifest.json'
            HasReviewManifest = $entries -contains 'Reports/review_manifest.json'
            HasSubmissionTool = $entries -contains 'Tools/build_submission_package.ps1'
            HasGeneratedOutputEntry = (@($entries | Where-Object { $_ -like 'Generated/*' }).Count -gt 0)
            HasReportsSourceEntry = (@($entries | Where-Object { $_ -like 'Reports/*' -and $_ -ne 'Reports/review_manifest.json' }).Count -gt 0)
            ZipIsNotOlderThanReview = $zipIsNotOlderThanReview
        }
    } finally {
        if ($null -ne $probeFull) {
            $tempRootFull = [System.IO.Path]::GetFullPath($tempRoot)
            $probeFullPath = [System.IO.Path]::GetFullPath($probeFull)
            if ($probeFullPath.StartsWith($tempRootFull, [System.StringComparison]::OrdinalIgnoreCase) -and
                (Test-Path -LiteralPath $probeFullPath -PathType Container)) {
                Remove-Item -LiteralPath $probeFullPath -Recurse -Force
            }
        }
    }
}

function Invoke-StarterAuthoringDataValidationProbe([string]$TemplatePath) {
    $tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'Hecton8ModApiStaticValidation'
    $probeRoot = Join-Path $tempRoot ([System.Guid]::NewGuid().ToString('N'))
    $probeFull = $null
    try {
        [void](New-Item -ItemType Directory -Path $probeRoot -Force)
        Get-ChildItem -LiteralPath $TemplatePath -Force | Copy-Item -Destination $probeRoot -Recurse -Force
        $probeFull = (Resolve-Path -LiteralPath $probeRoot).Path
        $validatorPath = Join-Path $probeFull 'Tools\validate_structure.ps1'
        $settingsPath = Join-Path $probeFull 'Tables\settings.h8table.json'
        $localePath = Join-Path $probeFull 'Locales\en.h8loc.json'

        Set-Content -LiteralPath $settingsPath -Encoding UTF8 -Value @'
{
  "Schema": "hecton8.settings_table.draft.v1",
  "Rows": [
    { "Id": "Bad Row", "Kind": "boolean", "Default": true }
  ]
}
'@
        $badSettingsOutput = & powershell -NoProfile -ExecutionPolicy Bypass -File $validatorPath -Root $probeFull
        $badSettingsExitCode = $LASTEXITCODE

        Set-Content -LiteralPath $settingsPath -Encoding UTF8 -Value @'
{
  "Schema": "hecton8.settings_table.draft.v1",
  "Rows": []
}
'@
        Set-Content -LiteralPath $localePath -Encoding UTF8 -Value @'
{
  "Schema": "hecton8.locale.draft.v1",
  "Locale": "english",
  "Strings": {
    "Bad Key": " "
  }
}
'@
        $badLocaleOutput = & powershell -NoProfile -ExecutionPolicy Bypass -File $validatorPath -Root $probeFull
        $badLocaleExitCode = $LASTEXITCODE

        return [pscustomobject]@{
            InvalidSettingsExitCode = $badSettingsExitCode
            InvalidSettingsOutput = @($badSettingsOutput)
            InvalidLocaleExitCode = $badLocaleExitCode
            InvalidLocaleOutput = @($badLocaleOutput)
        }
    } finally {
        if ($null -ne $probeFull) {
            $tempRootFull = [System.IO.Path]::GetFullPath($tempRoot)
            $probeFullPath = [System.IO.Path]::GetFullPath($probeFull)
            if ($probeFullPath.StartsWith($tempRootFull, [System.StringComparison]::OrdinalIgnoreCase) -and
                (Test-Path -LiteralPath $probeFullPath -PathType Container)) {
                Remove-Item -LiteralPath $probeFullPath -Recurse -Force
            }
        }
    }
}

function Get-EnumNames([string]$Source, [string]$EnumName) {
    $match = [regex]::Match($Source, "enum\s+$EnumName\s*:\s*[A-Za-z0-9_]+\s*\{(?<body>.*?)\n\s*\}", 'Singleline')
    if (-not $match.Success) {
        Fail "Missing enum: $EnumName"
    }

    $names = New-Object 'System.Collections.Generic.List[string]'
    foreach ($line in ($match.Groups['body'].Value -split "`n")) {
        if ($line -match '^\s*([A-Za-z0-9_]+)\s*=') {
            [void]$names.Add($Matches[1])
        }
    }

    return @($names)
}

function Get-StructPublicFieldNames([string]$Source, [string]$StructName) {
    $match = [regex]::Match($Source, "struct\s+$StructName\s*\{(?<body>.*?)\n\s*\}", 'Singleline')
    if (-not $match.Success) {
        Fail "Missing struct: $StructName"
    }

    return @([regex]::Matches($match.Groups['body'].Value, '(?m)^\s*public\s+[A-Za-z0-9_<>,\[\]\.?]+\s+([A-Za-z0-9_]+);') | ForEach-Object { $_.Groups[1].Value } | Sort-Object)
}

function Get-StructDeclaredFieldNames([string]$Source, [string]$StructName) {
    $match = [regex]::Match($Source, "struct\s+$StructName\s*\{(?<body>.*?)\n\s*\}", 'Singleline')
    if (-not $match.Success) {
        Fail "Missing struct: $StructName"
    }

    return @([regex]::Matches($match.Groups['body'].Value, '(?m)^\s*(?:public|internal)\s+[A-Za-z0-9_<>,\[\]\.?]+\s+([A-Za-z0-9_]+);') | ForEach-Object { $_.Groups[1].Value } | Sort-Object)
}

function Get-ClassPublicMethodNames([string]$Source, [string]$ClassName) {
    $match = [regex]::Match($Source, "public\s+static\s+class\s+$ClassName\s*\{(?<body>.*?)\n\s*\}", 'Singleline')
    if (-not $match.Success) {
        Fail "Missing public static class: $ClassName"
    }

    return @([regex]::Matches($match.Groups['body'].Value, '(?m)^\s*public\s+static\s+(?!class\b)(?:[A-Za-z0-9_<>,\[\]\.?]+\s+)+([A-Za-z0-9_]+)(?:<[^>]+>)?\s*\(') | ForEach-Object { $_.Groups[1].Value } | Sort-Object)
}

function Resolve-StructLayoutSize([string]$Source, [string]$StructName, [string]$LayoutKind) {
    $match = [regex]::Match($Source, "\[[^\]]*StructLayout\([^\)]*LayoutKind\.$LayoutKind,\s*Size\s*=\s*([A-Za-z0-9_\.]+|\d+)[^\)]*\)\]\s*public\s+(?:readonly\s+)?struct\s+$StructName\b", 'Singleline')
    if (-not $match.Success) {
        Fail "Missing $StructName $LayoutKind layout size declaration."
    }

    $sizeToken = $match.Groups[1].Value
    if ($sizeToken -match '^\d+$') {
        return [int]$sizeToken
    }

    $sizeConstName = ($sizeToken -split '\.')[-1]
    $sizeConstMatch = [regex]::Match($Source, "const\s+int\s+$sizeConstName\s*=\s*(\d+);")
    if (-not $sizeConstMatch.Success) {
        Fail "Missing $StructName size constant: $sizeToken"
    }

    return [int]$sizeConstMatch.Groups[1].Value
}

function Assert-StructFieldOffset([string]$Source, [string]$StructName, [string]$FieldName, [int]$Offset) {
    $match = [regex]::Match($Source, "struct\s+$StructName\b(?<body>.*?)(?:\n\s*\}\s*\n\s*///|\n\s*\}\s*\n\s*(?:internal|public|private)\s)", 'Singleline')
    if (-not $match.Success) {
        Fail "Missing struct body for field offset audit: $StructName"
    }

    Assert-True ([regex]::IsMatch($match.Groups['body'].Value, "FieldOffset\($Offset\)\]\s+(?:public|private|internal)\s+(?:readonly\s+)?[A-Za-z0-9_<>,\[\]\.?]+\s+$FieldName\s*;", 'Singleline')) "$StructName.$FieldName missing FieldOffset($Offset)."
}

$signalMatches = [regex]::Matches($signalSource, 'public\s+(?:readonly\s+|partial\s+)*struct\s+([A-Za-z0-9_]+)\s*:\s*ISignal(?![A-Za-z0-9_])')
$signals = New-Object 'System.Collections.Generic.List[string]'
foreach ($match in $signalMatches) {
    [void]$signals.Add($match.Groups[1].Value)
}

$uniqueSignals = $signals | Sort-Object -Unique
$bridgeMatches = [regex]::Matches($projectionSource, 'SignalBus<([A-Za-z0-9_]+)>')
$bridgeSignals = New-Object 'System.Collections.Generic.List[string]'
foreach ($match in $bridgeMatches) {
    [void]$bridgeSignals.Add($match.Groups[1].Value)
}

$uniqueBridgeSignals = $bridgeSignals | Sort-Object -Unique
$allowedSignals = @()
foreach ($lane in $schema.allowedSignalBuses) {
    if ($lane.bus -match '^SignalBus<([A-Za-z0-9_]+)>$') {
        $allowedSignals += $Matches[1]
    }
    else {
        Fail "Invalid allowed lane format: $($lane.bus)"
    }
}

$allowedSignals = $allowedSignals | Sort-Object -Unique
$opcodeNames = Get-EnumNames $commandDispatcherSource 'ModCommandOpcode'
$acceptedOpcodes = @($opcodeNames | Where-Object { $_ -ne 'None' })
$targetNames = Get-EnumNames $commandDispatcherSource 'ModCommandTargetSystem'
$rejectReasonNames = Get-EnumNames $commandDispatcherSource 'ModCommandRejectReason'
$schemaOpcodes = @($schema.commandApi.acceptedOpcodes | Sort-Object -Unique)
$apiSurfaceNames = @([regex]::Matches($hectonApiSource, 'public\s+static\s+class\s+([A-Za-z0-9_]+)') | ForEach-Object { $_.Groups[1].Value } | Where-Object { $_ -ne 'HectonAPI' } | Sort-Object)
$publicApiMethods = @([regex]::Matches($hectonApiSource, '(?m)^\s*public\s+static\s+(?!class\b)(?:[A-Za-z0-9_<>,\[\]\.?]+\s+)+([A-Za-z0-9_]+)(?:<[^>]+>)?\s*\(') | ForEach-Object { $_.Groups[1].Value } | Sort-Object)
$publicApiProperties = @([regex]::Matches($hectonApiSource, '(?m)^\s*public\s+static\s+[A-Za-z0-9_<>,\[\]\.?]+\s+([A-Za-z0-9_]+)\s*=>') | ForEach-Object { $_.Groups[1].Value } | Sort-Object)
$internalApiInfrastructureMethods = @(
    'BindRegistryServicesCold',
    'OnGlobalRegistryServiceReplaced',
    'ResetRegistryCacheCold'
)
$internalApiMethods = @([regex]::Matches($hectonApiSource, '(?m)^\s*internal\s+static\s+(?!class\b)(?:[A-Za-z0-9_<>,\[\]\.?]+\s+)+([A-Za-z0-9_]+)(?:<[^>]+>)?\s*\(') | ForEach-Object { $_.Groups[1].Value } | Where-Object { $internalApiInfrastructureMethods -notcontains $_ } | Sort-Object)
$modEventDtoSizeMatch = [regex]::Match($eventContractsSource, 'StructLayout\(.*?LayoutKind\.Explicit,\s*Size\s*=\s*(\d+).*?public\s+struct\s+ModEventDto', 'Singleline')
Assert-True $modEventDtoSizeMatch.Success 'Missing ModEventDto explicit size declaration.'
$modEventDtoSize = [int]$modEventDtoSizeMatch.Groups[1].Value
$modEventDtoOffsets = @([regex]::Matches($eventContractsSource, 'FieldOffset\((\d+)\)\]\s+public\s+([A-Za-z0-9_<>]+)\s+([A-Za-z0-9_]+);') | ForEach-Object {
    [pscustomobject]@{
        Offset = [int]$_.Groups[1].Value
        Type = $_.Groups[2].Value
        Name = $_.Groups[3].Value
    }
})
$modCommandSizeMatch = [regex]::Match($commandDispatcherSource, 'StructLayout\(LayoutKind\.(?:Sequential|Explicit),\s*Size\s*=\s*(\d+)\)\]\s*public\s+struct\s+ModCommand', 'Singleline')
Assert-True $modCommandSizeMatch.Success 'Missing ModCommand 64-byte layout size declaration.'
$modCommandSize = [int]$modCommandSizeMatch.Groups[1].Value
$modPlayerSpawnedEventSize = Resolve-StructLayoutSize $eventContractsSource 'ModPlayerSpawnedEvent' 'Explicit'
$modBiomeChangedEventSize = Resolve-StructLayoutSize $eventContractsSource 'ModBiomeChangedEvent' 'Explicit'
$modAupCommandSize = Resolve-StructLayoutSize $spatialContractsSource 'ModAupCommand' 'Explicit'
$modAupResponseSize = Resolve-StructLayoutSize $spatialContractsSource 'ModAupResponse' 'Explicit'
$modRenderInstanceCommandSize = Resolve-StructLayoutSize $spatialContractsSource 'ModRenderInstanceCommand' 'Explicit'
$modRaycastResultPayloadSize = Resolve-StructLayoutSize $spatialContractsSource 'ModRaycastResultPayload' 'Explicit'
$modInteractionRejectedPayloadSize = Resolve-StructLayoutSize $spatialContractsSource 'ModInteractionRejectedPayload' 'Explicit'
$modCriticalMemoryEvictionPayloadSize = Resolve-StructLayoutSize $spatialContractsSource 'ModCriticalMemoryEvictionPayload' 'Explicit'
$interactionEventPayloadSize = Resolve-StructLayoutSize $interactionEventsSource 'InteractionEventPayload' 'Explicit'
$craftingEventPayloadSize = Resolve-StructLayoutSize $craftingEventsSource 'CraftingEventPayload' 'Explicit'
Assert-StructFieldOffset $eventContractsSource 'ModPlayerSpawnedEvent' 'PlayerId' 0
Assert-StructFieldOffset $eventContractsSource 'ModPlayerSpawnedEvent' 'AbsoluteUniversePosition' 8
Assert-StructFieldOffset $eventContractsSource 'ModPlayerSpawnedEvent' 'BiomeId' 20
Assert-StructFieldOffset $eventContractsSource 'ModBiomeChangedEvent' 'PreviousBiomeId' 0
Assert-StructFieldOffset $eventContractsSource 'ModBiomeChangedEvent' 'CurrentBiomeId' 4
Assert-StructFieldOffset $eventContractsSource 'ModBiomeChangedEvent' 'AbsoluteUniversePosition' 8
Assert-StructFieldOffset $eventContractsSource 'ModBiomeChangedEvent' '_pad0' 20
Assert-StructFieldOffset $interactionEventsSource 'InteractionEventPayload' 'ItemHashId' 0
Assert-StructFieldOffset $interactionEventsSource 'InteractionEventPayload' 'TargetHashId' 4
Assert-StructFieldOffset $interactionEventsSource 'InteractionEventPayload' 'InteractorHashId' 8
Assert-StructFieldOffset $interactionEventsSource 'InteractionEventPayload' 'ReferenceSlot' 12
Assert-StructFieldOffset $interactionEventsSource 'InteractionEventPayload' 'Quantity' 16
Assert-StructFieldOffset $interactionEventsSource 'InteractionEventPayload' 'EventType' 20
Assert-StructFieldOffset $interactionEventsSource 'InteractionEventPayload' 'Reserved' 22
Assert-StructFieldOffset $interactionEventsSource 'InteractionEventPayload' '_pad0' 24
Assert-StructFieldOffset $craftingEventsSource 'CraftingEventPayload' 'SpawnPosition' 0
Assert-StructFieldOffset $craftingEventsSource 'CraftingEventPayload' 'VelocityChange' 12
Assert-StructFieldOffset $craftingEventsSource 'CraftingEventPayload' 'FabricatorHashId' 24
Assert-StructFieldOffset $craftingEventsSource 'CraftingEventPayload' 'RecipeHashId' 28
Assert-StructFieldOffset $craftingEventsSource 'CraftingEventPayload' 'ResultItemHashId' 32
Assert-StructFieldOffset $craftingEventsSource 'CraftingEventPayload' 'Progress01' 36
Assert-StructFieldOffset $craftingEventsSource 'CraftingEventPayload' 'Quantity' 40
Assert-StructFieldOffset $craftingEventsSource 'CraftingEventPayload' 'ReferenceSlot' 44
Assert-StructFieldOffset $craftingEventsSource 'CraftingEventPayload' 'EventType' 48
Assert-StructFieldOffset $craftingEventsSource 'CraftingEventPayload' 'Reserved' 50
Assert-StructFieldOffset $craftingEventsSource 'CraftingEventPayload' '_pad0' 52
Assert-StructFieldOffset $craftingEventsSource 'CraftingEventPayload' '_pad1' 56
$currentApiVersionMatch = [regex]::Match($modLoaderSource, 'internal\s+const\s+int\s+CurrentAPIVersion\s*=\s*(\d+);')
Assert-True $currentApiVersionMatch.Success 'Missing ModLoader.CurrentAPIVersion.'
$currentApiVersion = [int]$currentApiVersionMatch.Groups[1].Value
$manifestFileNameMatch = [regex]::Match($modLoaderSource, 'private\s+const\s+string\s+ManifestFileName\s*=\s*"([^"]+)";')
Assert-True $manifestFileNameMatch.Success 'Missing ModLoader manifest file name constant.'
$manifestFileName = $manifestFileNameMatch.Groups[1].Value
$manifestMaxBytesMatch = [regex]::Match($modLoaderSource, 'private\s+const\s+long\s+MaxManifestBytes\s*=\s*(\d+)L\s*\*\s*(\d+)L;')
Assert-True $manifestMaxBytesMatch.Success 'Missing ModLoader.MaxManifestBytes.'
$manifestMaxBytes = [long]$manifestMaxBytesMatch.Groups[1].Value * [long]$manifestMaxBytesMatch.Groups[2].Value
$manifestByteCapCheckIndex = $modLoaderSource.IndexOf('if (!TryValidateManifestFileSize(manifestPath))', [System.StringComparison]::Ordinal)
$manifestReadAllTextIndex = $modLoaderSource.IndexOf('string json = File.ReadAllText(manifestPath);', [System.StringComparison]::Ordinal)
$manifestByteCapEnforcedBeforeRead =
    $modLoaderSource.Contains('private static bool TryValidateManifestFileSize') -and
    $modLoaderSource.Contains('FileInfo fileInfo = new FileInfo(manifestPath);') -and
    $modLoaderSource.Contains('fileInfo.Length > MaxManifestBytes') -and
    $modLoaderSource.Contains('manifest exceeds ", MaxManifestBytesLabel, " byte cap') -and
    ($manifestByteCapCheckIndex -ge 0) -and
    ($manifestReadAllTextIndex -gt $manifestByteCapCheckIndex)
Assert-True $manifestByteCapEnforcedBeforeRead 'ModLoader must reject missing, empty, or oversized manifests before File.ReadAllText.'
$manifestDiscoveryMaxCountMatch = [regex]::Match($modLoaderSource, 'private\s+const\s+int\s+MaxDiscoveredManifestCount\s*=\s*(\d+);')
Assert-True $manifestDiscoveryMaxCountMatch.Success 'Missing ModLoader.MaxDiscoveredManifestCount.'
$manifestDiscoveryMaxCount = [int]$manifestDiscoveryMaxCountMatch.Groups[1].Value
$manifestDiscoveryGetFilesAllDirectoriesForbidden =
    -not $modLoaderSource.Contains('Directory.GetFiles(modsRoot, ManifestFileName, SearchOption.AllDirectories)')
$manifestDiscoveryCollectIndex = $modLoaderSource.IndexOf('CollectManifestPaths(modsRoot, manifestPaths);', [System.StringComparison]::Ordinal)
$manifestDiscoveryCandidateListIndex = $modLoaderSource.IndexOf('List<ModCandidate> candidates = new List<ModCandidate>(manifestPaths.Count);', [System.StringComparison]::Ordinal)
$manifestDiscoveryUsesBoundedEnumeration =
    $modLoaderSource.Contains('private static void CollectManifestPaths') -and
    $modLoaderSource.Contains('Directory.EnumerateFiles(modsRoot, ManifestFileName, SearchOption.AllDirectories)') -and
    $modLoaderSource.Contains('new List<string>(MaxDiscoveredManifestCount)') -and
    $modLoaderSource.Contains('manifestPaths.Count >= MaxDiscoveredManifestCount') -and
    $modLoaderSource.Contains('Manifest discovery capped at ') -and
    $manifestDiscoveryGetFilesAllDirectoriesForbidden -and
    ($manifestDiscoveryCollectIndex -ge 0) -and
    ($manifestDiscoveryCandidateListIndex -gt $manifestDiscoveryCollectIndex)
Assert-True $manifestDiscoveryUsesBoundedEnumeration 'ModLoader must use bounded lazy manifest discovery before candidate allocation.'
$maxTopLevelManagedAssemblyCountMatch = [regex]::Match($modLoaderSource, 'private\s+const\s+int\s+MaxTopLevelManagedAssemblyCount\s*=\s*(\d+);')
Assert-True $maxTopLevelManagedAssemblyCountMatch.Success 'Missing ModLoader.MaxTopLevelManagedAssemblyCount.'
$maxTopLevelManagedAssemblyCount = [int]$maxTopLevelManagedAssemblyCountMatch.Groups[1].Value
$maxTopLevelBundleCountMatch = [regex]::Match($modLoaderSource, 'private\s+const\s+int\s+MaxTopLevelBundleCount\s*=\s*(\d+);')
Assert-True $maxTopLevelBundleCountMatch.Success 'Missing ModLoader.MaxTopLevelBundleCount.'
$maxTopLevelBundleCount = [int]$maxTopLevelBundleCountMatch.Groups[1].Value
$maxLocalizationFileCountMatch = [regex]::Match($modLoaderSource, 'private\s+const\s+int\s+MaxLocalizationFileCount\s*=\s*(\d+);')
Assert-True $maxLocalizationFileCountMatch.Success 'Missing ModLoader.MaxLocalizationFileCount.'
$maxLocalizationFileCount = [int]$maxLocalizationFileCountMatch.Groups[1].Value
$oldTopLevelDllGetFilesForbidden =
    -not $modLoaderSource.Contains('Directory.GetFiles(modDirectory, "*" + DefaultAssemblyExtension, SearchOption.TopDirectoryOnly)')
$oldTopLevelBundleGetFilesForbidden =
    -not $modLoaderSource.Contains('Directory.GetFiles(modDirectory, "*" + DefaultBundleExtension, SearchOption.TopDirectoryOnly)')
$oldTopLevelLocalizationGetFilesForbidden =
    -not $modLoaderSource.Contains('Directory.GetFiles(modDirectory, "lang_*.json", SearchOption.TopDirectoryOnly)')
$topLevelPackageFileDiscoveryUsesBoundedEnumeration =
    $modLoaderSource.Contains('private static string[] CollectTopLevelFiles') -and
    $modLoaderSource.Contains('Directory.EnumerateFiles(directory, searchPattern, SearchOption.TopDirectoryOnly)') -and
    $modLoaderSource.Contains('new List<string>(maxCount)') -and
    $modLoaderSource.Contains('files.Count >= maxCount') -and
    $modLoaderSource.Contains('Top-level ", fileKind, " discovery capped at ') -and
    $modLoaderSource.Contains('files.Sort(StringComparer.OrdinalIgnoreCase)') -and
    $oldTopLevelDllGetFilesForbidden -and
    $oldTopLevelBundleGetFilesForbidden -and
    $oldTopLevelLocalizationGetFilesForbidden
Assert-True $topLevelPackageFileDiscoveryUsesBoundedEnumeration 'ModLoader must use bounded top-level package file discovery for DLL, bundle, and localization files.'
$managedAssemblyIdentityScanUsesBoundedEnumeration =
    $modLoaderSource.Contains('ResolveManagedAssemblyIdentityScanPaths(') -and
    $modLoaderSource.Contains('out string disabledReason') -and
    $modLoaderSource.Contains('MaxTopLevelManagedAssemblyCount') -and
    $modLoaderSource.Contains('Package contains more than ", MaxTopLevelManagedAssemblyCountLabel, " top-level managed assemblies.') -and
    $modLoaderSource.Contains('Package top-level managed assembly discovery failed.')
Assert-True $managedAssemblyIdentityScanUsesBoundedEnumeration 'ModLoader managed assembly identity scan must be bounded and fail closed on over-cap or discovery failure.'
$excessTopLevelManagedAssembliesDisablePackage =
    $modLoaderSource.Contains('out string managedAssemblyDiscoveryError') -and
    $modLoaderSource.Contains('manifestContractError = managedAssemblyDiscoveryError') -and
    $managedAssemblyIdentityScanUsesBoundedEnumeration
Assert-True $excessTopLevelManagedAssembliesDisablePackage 'ModLoader must disable packages when top-level managed assembly discovery exceeds cap or fails.'
$manifestStructMatch = [regex]::Match($modLoaderSource, 'private\s+struct\s+ModManifest\s*\{(?<body>.*?)public\s+ModManifest\s*\(', 'Singleline')
Assert-True $manifestStructMatch.Success 'Missing ModManifest field block.'
$manifestFields = @([regex]::Matches($manifestStructMatch.Groups['body'].Value, '(?m)^\s*public\s+[A-Za-z0-9_<>,\[\]\.?]+\s+([A-Za-z0-9_]+);') | ForEach-Object { $_.Groups[1].Value } | Sort-Object)
$builderApiVersionMatch = [regex]::Match($modBuilderWindowSource, 'private\s+const\s+int\s+CurrentRequiredApiVersion\s*=\s*(\d+);')
Assert-True $builderApiVersionMatch.Success 'Missing ModBuilderWindow.CurrentRequiredApiVersion.'
$builderApiVersion = [int]$builderApiVersionMatch.Groups[1].Value
$modBuilderManifestStructMatch = [regex]::Match($modBuilderWindowSource, 'private\s+struct\s+ModManifestData\s*\{(?<body>.*?)\n\s*\}', 'Singleline')
Assert-True $modBuilderManifestStructMatch.Success 'Missing ModBuilderWindow.ModManifestData field block.'
$modBuilderManifestFields = @([regex]::Matches($modBuilderManifestStructMatch.Groups['body'].Value, '(?m)^\s*public\s+[A-Za-z0-9_<>,\[\]\.?]+\s+([A-Za-z0-9_]+);') | ForEach-Object { $_.Groups[1].Value } | Sort-Object)
$missingBuilderManifestFields = @($manifestFields | Where-Object { $modBuilderManifestFields -notcontains $_ })
$extraBuilderManifestFields = @($modBuilderManifestFields | Where-Object { $manifestFields -notcontains $_ })
$modMetadataFields = Get-StructPublicFieldNames $modMetadataSource 'ModMetadata'
$modRuntimeInfoFields = Get-StructDeclaredFieldNames $modRuntimeInfoSource 'ModRuntimeInfo'
$lifecycleMethods = @([regex]::Matches($iHectonModSource, '(?m)^\s*void\s+(On[A-Za-z0-9_]+)\s*\(\s*\);') | ForEach-Object { $_.Groups[1].Value } | Sort-Object)
$versionedProperties = @([regex]::Matches($iHectonModSource, '(?m)^\s*int\s+RequiredAPIVersion\s*\{\s*get;\s*\}') | ForEach-Object { 'RequiredAPIVersion' })
$bundleBuildAssetCountMatch = [regex]::Match($modBuilderWindowSource, 'private\s+const\s+int\s+MaxBundleBuildAssetCount\s*=\s*(\d+);')
Assert-True $bundleBuildAssetCountMatch.Success 'Missing ModBuilderWindow.MaxBundleBuildAssetCount.'
$maxBundleBuildAssetCount = [int]$bundleBuildAssetCountMatch.Groups[1].Value
$builderManagedAssemblyInputCountMatch = [regex]::Match($modBuilderWindowSource, 'private\s+const\s+int\s+MaxManagedAssemblyInputCount\s*=\s*(\d+);')
Assert-True $builderManagedAssemblyInputCountMatch.Success 'Missing ModBuilderWindow.MaxManagedAssemblyInputCount.'
$maxManagedAssemblyInputCount = [int]$builderManagedAssemblyInputCountMatch.Groups[1].Value
$builderStaleAssemblyCleanupScanCountMatch = [regex]::Match($modBuilderWindowSource, 'private\s+const\s+int\s+MaxStaleAssemblyCleanupScanCount\s*=\s*(\d+);')
Assert-True $builderStaleAssemblyCleanupScanCountMatch.Success 'Missing ModBuilderWindow.MaxStaleAssemblyCleanupScanCount.'
$maxStaleAssemblyCleanupScanCount = [int]$builderStaleAssemblyCleanupScanCountMatch.Groups[1].Value
$bundleBuildAssetDiscoveryUsesBoundedEnumeration =
    $modBuilderWindowSource.Contains('Directory.EnumerateFiles(assetFolderAbsolutePath, "*", SearchOption.AllDirectories)') -and
    $modBuilderWindowSource.Contains('new List<string>(MaxBundleBuildAssetCount)') -and
    $modBuilderWindowSource.Contains('assetPaths.Count >= MaxBundleBuildAssetCount') -and
    $modBuilderWindowSource.Contains('assetPaths.Sort(StringComparer.OrdinalIgnoreCase)') -and
    -not $modBuilderWindowSource.Contains('AssetDatabase.FindAssets(string.Empty')
Assert-True $bundleBuildAssetDiscoveryUsesBoundedEnumeration 'ModBuilderWindow must use bounded deterministic filesystem enumeration for bundle asset collection.'
$builderManagedAssemblyInputCapMatchesLoader =
    $maxManagedAssemblyInputCount -eq $maxTopLevelManagedAssemblyCount -and
    $modBuilderWindowSource.Contains('_dllPaths.Count >= MaxManagedAssemblyInputCount') -and
    $modBuilderWindowSource.Contains('assemblyPaths.Length > MaxManagedAssemblyInputCount') -and
    $modBuilderWindowSource.Contains('Managed assembly selection exceeds')
Assert-True $builderManagedAssemblyInputCapMatchesLoader 'ModBuilderWindow managed assembly input cap must match ModLoader top-level DLL cap.'
$builderSkipsExpensiveValidationDuringOnGUI =
    $modBuilderWindowSource.Contains('TryValidateConfiguration(false, out string validationError)') -and
    $modBuilderWindowSource.Contains('TryValidateConfiguration(false, out _)') -and
    $modBuilderWindowSource.Contains('TryValidateConfiguration(true, out string validationError)') -and
    $modBuilderWindowSource.Contains('includeExpensiveFileContentValidation &&') -and
    $modBuilderWindowSource.Contains('!TryValidateManagedAssemblyIdentity(path') -and
    $modBuilderWindowSource.Contains('Asset folder does not contain any bundle-eligible assets. Leave Asset Folder empty') -and
    -not $modBuilderWindowSource.Contains('HasBundleEligibleAssets')
Assert-True $builderSkipsExpensiveValidationDuringOnGUI 'ModBuilderWindow OnGUI validation must skip deep asset scans and DLL identity reads.'
$builderStaleDllCleanupUsesBoundedEnumeration =
    $modBuilderWindowSource.Contains('Directory.EnumerateFiles(outputDirectory, "*.dll", SearchOption.TopDirectoryOnly)') -and
    $modBuilderWindowSource.Contains('MaxStaleAssemblyCleanupScanCount') -and
    $modBuilderWindowSource.Contains('scannedCount >= MaxStaleAssemblyCleanupScanCount') -and
    -not $modBuilderWindowSource.Contains('Directory.GetFiles(outputDirectory, "*.dll", SearchOption.TopDirectoryOnly)')
Assert-True $builderStaleDllCleanupUsesBoundedEnumeration 'ModBuilderWindow stale DLL cleanup must use bounded enumeration, not an unbounded path array.'
$builderRejectsDuplicateManagedAssemblyFileNames =
    $modBuilderWindowSource.Contains('Managed assembly file name is selected more than once')
Assert-True $builderRejectsDuplicateManagedAssemblyFileNames 'ModBuilderWindow must reject duplicate selected DLL file names before copy.'
$moddingSdkHubPresent = $moddingSdkHubSource.Contains('[MenuItem("Hecton/Modding/SDK Hub")]')
$moddingSdkHubOpensBuilder = $moddingSdkHubSource.Contains('ModBuilderWindow.ShowWindow()')
$moddingSdkHubOpensStarterWorkbench =
    $moddingSdkHubSource.Contains('Open Starter Kit Workbench') -and
    $moddingSdkHubSource.Contains('ExternalStarterKitWorkbenchWindow.ShowWindow()')
$starterKitButtonIndex = $moddingSdkHubSource.IndexOf('Create External Starter Kit')
$starterWorkbenchButtonIndex = $moddingSdkHubSource.IndexOf('Open Starter Kit Workbench')
$legacyBuilderButtonIndex = $moddingSdkHubSource.IndexOf('Open Internal Legacy Mod Builder')
$moddingSdkHubPrioritizesExternalStarterKit =
    $starterKitButtonIndex -ge 0 -and
    $starterWorkbenchButtonIndex -ge 0 -and
    $legacyBuilderButtonIndex -ge 0 -and
    $starterKitButtonIndex -lt $starterWorkbenchButtonIndex -and
    $starterWorkbenchButtonIndex -lt $legacyBuilderButtonIndex
$moddingSdkHubGatesLegacyBuilder =
    $moddingSdkHubSource.Contains('OpenLegacyModBuilder') -and
    $moddingSdkHubSource.Contains('EditorUtility.DisplayDialog') -and
    $moddingSdkHubSource.Contains('Internal Legacy Mod Builder') -and
    $moddingSdkHubSource.Contains('Public authors should use External Starter Kit')
$modBuilderMenuIsInternalLegacy =
    $modBuilderWindowSource.Contains('[MenuItem("Hecton/Modding/Internal/Legacy Mod Builder")]') -and
    $modBuilderWindowSource.Contains('HECTON-8 Legacy Mod Builder') -and
    $modBuilderWindowSource.Contains('Build Internal Legacy Package') -and
    $modBuilderWindowSource.Contains('Internal legacy package builder')
$moddingSdkHubLinksCoreDocs =
    $moddingSdkHubSource.Contains('Docs/Modding/README.md') -and
    $moddingSdkHubSource.Contains('Docs/Modding/Mod_API_Specification.md') -and
    $moddingSdkHubSource.Contains('Docs/Modding/SDK_Authoring_Interface_Plan.md') -and
    $moddingSdkHubSource.Contains('Docs/Modding/SDK_Product_Blueprint.md') -and
    $moddingSdkHubSource.Contains('Docs/Modding/Sample_InfiniteO2_Mod.md') -and
    $moddingSdkHubSource.Contains('Docs/Modding/Runtime_Verification_Playbook.md')
$moddingSdkHubRunsStaticValidator =
    $moddingSdkHubSource.Contains('RunStaticValidator') -and
    $moddingSdkHubSource.Contains('Docs/Modding/Validate_Mod_API_Static.ps1')
$moddingSdkHubRunsStaticValidatorAsync =
    $moddingSdkHubSource.Contains('BeginOutputReadLine') -and
    $moddingSdkHubSource.Contains('BeginErrorReadLine') -and
    $moddingSdkHubSource.Contains('EditorApplication.update') -and
    $moddingSdkHubSource.Contains('PollRunningValidator') -and
    -not $moddingSdkHubSource.Contains('StandardOutput.ReadToEnd') -and
    -not $moddingSdkHubSource.Contains('WaitForExit()')
$moddingSdkHubShowsValidatorFailuresAsErrors =
    $moddingSdkHubSource.Contains('_lastValidatorFailed') -and
    $moddingSdkHubSource.Contains('_lastValidatorFailed = exitCode != 0') -and
    $moddingSdkHubSource.Contains('_lastValidatorFailed ? MessageType.Error : MessageType.Info')
$moddingSdkHubShowsEnvelopeOnlyBoundary =
    $moddingSdkHubSource.Contains('Runtime API: envelope-only') -and
    $moddingSdkHubSource.Contains('Managed DLL entries are legacy/internal')
$externalStarterKitWorkbenchPresent =
    $externalStarterKitWorkbenchSource.Contains('[MenuItem("Hecton/Modding/External Starter Kit Workbench")]') -and
    $externalStarterKitWorkbenchSource.Contains('HECTON-8 External Starter Kit Workbench')
$externalStarterKitWorkbenchUsesIdentityTool =
    $externalStarterKitWorkbenchSource.Contains('Apply Identity + Validate') -and
    $externalStarterKitWorkbenchSource.Contains('Tools/set_mod_identity.ps1')
$externalStarterKitWorkbenchUsesPrepareTool =
    $externalStarterKitWorkbenchSource.Contains('Validate + Build Review') -and
    $externalStarterKitWorkbenchSource.Contains('Tools/prepare_mod.ps1')
$externalStarterKitWorkbenchCanRefreshStarterKit =
    $externalStarterKitWorkbenchSource.Contains('Create/Refresh Starter Kit') -and
    $externalStarterKitWorkbenchSource.Contains('ModdingSdkHubWindow.CreateExternalStarterKit()')
$externalStarterKitWorkbenchListsOpcodes =
    $externalStarterKitWorkbenchSource.Contains('List Graph Opcodes') -and
    $externalStarterKitWorkbenchSource.Contains('Tools/list_allowed_opcodes.ps1')
$externalStarterKitWorkbenchShowsReviewSummary =
    $externalStarterKitWorkbenchSource.Contains('Review Summary') -and
    $externalStarterKitWorkbenchSource.Contains('Reports/review_manifest.json') -and
    $externalStarterKitWorkbenchSource.Contains('FileCount') -and
    $externalStarterKitWorkbenchSource.Contains('TotalBytes')
$externalStarterKitWorkbenchShowsEnvelopeBoundary =
    $externalStarterKitWorkbenchSource.Contains('Runtime API: envelope-only') -and
    $externalStarterKitWorkbenchSource.Contains('does not enable managed DLL') -and
    $externalStarterKitWorkbenchSource.Contains('loose AssetBundle')
$externalStarterKitWorkbenchShowsStarterHealth =
    $externalStarterKitWorkbenchSource.Contains('Starter Health') -and
    $externalStarterKitWorkbenchSource.Contains('RequiredStarterFiles') -and
    $externalStarterKitWorkbenchSource.Contains('Missing required files')
$externalStarterKitWorkbenchShowsCapabilityMatrix =
    $externalStarterKitWorkbenchSource.Contains('Capability Matrix') -and
    $externalStarterKitWorkbenchSource.Contains('LoadCapabilityMatrix') -and
    $externalStarterKitWorkbenchSource.Contains('Open Capabilities Guide') -and
    $externalStarterKitWorkbenchSource.Contains('Docs/capabilities.md') -and
    $externalStarterKitWorkbenchSource.Contains('Supported authoring surfaces') -and
    $externalStarterKitWorkbenchSource.Contains('NOT PUBLIC RIGHTS') -and
    $externalStarterKitContractText.Contains('Capability Matrix')
$externalStarterKitWorkbenchUsesCurrentSchemaHealthList =
    $externalStarterKitWorkbenchSource.Contains('"Schemas/settings_table.schema.json"') -and
    $externalStarterKitWorkbenchSource.Contains('"Schemas/locale.schema.json"') -and
    $externalStarterKitWorkbenchSource.Contains('"Schemas/assets.schema.json"') -and
    $externalStarterKitWorkbenchSource.Contains('"Content/README.md"') -and
    $externalStarterKitWorkbenchSource.Contains('"Content/Assets/README.md"') -and
    $externalStarterKitWorkbenchSource.Contains('"Docs/capabilities.md"') -and
    $externalStarterKitWorkbenchSource.Contains('"Generated/README.md"') -and
    $externalStarterKitWorkbenchSource.Contains('"Reports/README.md"') -and
    $externalStarterKitWorkbenchSource.Contains('"Reference/README.md"') -and
    $externalStarterKitWorkbenchSource.Contains('"Tools/build_submission_package.ps1"') -and
    $externalStarterKitWorkbenchSource.Contains('"Tools/create_settings_row_snippet.ps1"') -and
    $externalStarterKitWorkbenchSource.Contains('"Tools/create_locale_entry_snippet.ps1"') -and
    $externalStarterKitWorkbenchSource.Contains('"Tools/create_asset_entry_snippet.ps1"') -and
    $externalStarterKitWorkbenchSource.Contains('"Tools/apply_asset_entry_snippet.ps1"') -and
    $externalStarterKitWorkbenchSource.Contains('"Tools/configure_manifest_contract.ps1"') -and
    $externalStarterKitWorkbenchSource.Contains('".vscode/tasks.json"') -and
    -not $externalStarterKitWorkbenchSource.Contains('"Schemas/h8table.schema.json"') -and
    -not $externalStarterKitWorkbenchSource.Contains('"Schemas/h8loc.schema.json"')
$externalStarterKitWorkbenchRunsStructureValidator =
    $externalStarterKitWorkbenchSource.Contains('Validate Structure Only') -and
    $externalStarterKitWorkbenchSource.Contains('Tools/validate_structure.ps1')
$externalStarterKitWorkbenchLinksCoreDocs =
    $externalStarterKitWorkbenchSource.Contains('File Contract') -and
    $externalStarterKitWorkbenchSource.Contains('Docs/Modding/External_Starter_Kit_File_Contract.md') -and
    $externalStarterKitWorkbenchSource.Contains('Docs/Modding/Mod_API_Specification.md') -and
    $externalStarterKitWorkbenchSource.Contains('Docs/Modding/SDK_Authoring_Interface_Plan.md') -and
    $externalStarterKitWorkbenchSource.Contains('Docs/Modding/Runtime_Verification_Playbook.md')
$externalStarterKitWorkbenchOpensVsCodeTasks =
    $externalStarterKitWorkbenchSource.Contains('VS Code Tasks') -and
    $externalStarterKitWorkbenchSource.Contains('ModdingSDK/ExternalStarterKit/.vscode/tasks.json') -and
    $externalStarterKitWorkbenchSource.Contains('VS Code Settings') -and
    $externalStarterKitWorkbenchSource.Contains('ModdingSDK/ExternalStarterKit/.vscode/settings.json')
$externalStarterKitWorkbenchRunsToolsAsync =
    $externalStarterKitWorkbenchSource.Contains('BeginOutputReadLine') -and
    $externalStarterKitWorkbenchSource.Contains('BeginErrorReadLine') -and
    $externalStarterKitWorkbenchSource.Contains('EditorApplication.update') -and
    $externalStarterKitWorkbenchSource.Contains('PollRunningTool') -and
    -not $externalStarterKitWorkbenchSource.Contains('StandardOutput.ReadToEnd') -and
    -not $externalStarterKitWorkbenchSource.Contains('WaitForExit()')
$externalStarterKitWorkbenchShowsToolFailuresAsErrors =
    $externalStarterKitWorkbenchSource.Contains('_toolSummaryIsError') -and
    $externalStarterKitWorkbenchSource.Contains('_toolSummaryIsError = exitCode != 0') -and
    $externalStarterKitWorkbenchSource.Contains('_toolSummaryIsError ? MessageType.Error : MessageType.Info')
$externalStarterKitWorkbenchShowsReviewFreshness =
    $externalStarterKitWorkbenchSource.Contains('Review Freshness') -and
    $externalStarterKitWorkbenchSource.Contains('MaxFreshnessScanFiles') -and
    $externalStarterKitWorkbenchSource.Contains('Report is stale') -and
    $externalStarterKitWorkbenchSource.Contains('Generated/') -and
    $externalStarterKitWorkbenchSource.Contains('Reports/')
$externalStarterKitWorkbenchShowsGraphContractPreview =
    $externalStarterKitWorkbenchSource.Contains('Graph Contract Preview') -and
    $externalStarterKitWorkbenchSource.Contains('LoadGraphContractPreview') -and
    $externalStarterKitWorkbenchSource.Contains('MaxGraphPreviewNodes') -and
    $externalStarterKitWorkbenchSource.Contains('MaxGraphPreviewBytes') -and
    $externalStarterKitWorkbenchSource.Contains('MaxAllowedOpcodePreviewRows') -and
    $externalStarterKitWorkbenchSource.Contains('Reference/allowed_opcodes.csv contains invalid opcode token') -and
    $externalStarterKitWorkbenchSource.Contains('Graphs/main.h8graph.json') -and
    $externalStarterKitWorkbenchSource.Contains('Invalid opcodes') -and
    $externalStarterKitWorkbenchSource.Contains('Duplicate node IDs') -and
    $externalStarterKitWorkbenchSource.Contains('MaxEnvelopesPerFrame exceeds authoring budget')
$externalStarterKitWorkbenchGeneratesGraphNodeSnippet =
    $externalStarterKitWorkbenchSource.Contains('Graph Node Snippet') -and
    $externalStarterKitWorkbenchSource.Contains('GenerateGraphNodeSnippet') -and
    $externalStarterKitWorkbenchSource.Contains('Tools/create_graph_node_snippet.ps1') -and
    $externalStarterKitWorkbenchSource.Contains('Generated/graph_node_snippet.json') -and
    $externalStarterKitWorkbenchSource.Contains('bounded offline tool')
$externalStarterKitWorkbenchHasGraphOpcodePicker =
    $externalStarterKitWorkbenchSource.Contains('Graph Opcode Picker') -and
    $externalStarterKitWorkbenchSource.Contains('LoadGraphOpcodePicker') -and
    $externalStarterKitWorkbenchSource.Contains('EditorGUILayout.Popup') -and
    $externalStarterKitWorkbenchSource.Contains('_graphOpcodePopupLabels') -and
    $externalStarterKitWorkbenchSource.Contains('_graphOpcodePopupValues')
$externalStarterKitWorkbenchEditsGraphNodeParameters =
    $externalStarterKitWorkbenchSource.Contains('Parameters JSON') -and
    $externalStarterKitWorkbenchSource.Contains('_graphNodeParametersJson') -and
    $externalStarterKitWorkbenchSource.Contains('-ParametersJson') -and
    $externalStarterKitWorkbenchSource.Contains('Create Disabled Node') -and
    $externalStarterKitWorkbenchSource.Contains('Replace Existing On Apply')
$externalStarterKitWorkbenchAppliesGraphNodeSnippet =
    $externalStarterKitWorkbenchSource.Contains('Apply Node Snippet') -and
    $externalStarterKitWorkbenchSource.Contains('ApplyGraphNodeSnippet') -and
    $externalStarterKitWorkbenchSource.Contains('Tools/apply_graph_node_snippet.ps1') -and
    $externalStarterKitWorkbenchSource.Contains('raises the starter graph/manifest budget to one envelope if needed') -and
    $externalStarterKitWorkbenchSource.Contains('restores the previous files on failure')
$externalStarterKitWorkbenchGeneratesAuthoringSnippets =
    $externalStarterKitWorkbenchSource.Contains('Authoring Snippets') -and
    $externalStarterKitWorkbenchSource.Contains('GenerateSettingsRowSnippet') -and
    $externalStarterKitWorkbenchSource.Contains('GenerateLocaleEntrySnippet') -and
    $externalStarterKitWorkbenchSource.Contains('Tools/create_settings_row_snippet.ps1') -and
    $externalStarterKitWorkbenchSource.Contains('Tools/create_locale_entry_snippet.ps1') -and
    $externalStarterKitWorkbenchSource.Contains('Generated/settings_row_snippet.json') -and
    $externalStarterKitWorkbenchSource.Contains('Generated/locale_entry_snippet.json') -and
    $externalStarterKitWorkbenchSource.Contains('bounded offline tools')
$externalStarterKitWorkbenchAppliesAuthoringSnippets =
    $externalStarterKitWorkbenchSource.Contains('Apply Setting Snippet') -and
    $externalStarterKitWorkbenchSource.Contains('Apply Locale Snippet') -and
    $externalStarterKitWorkbenchSource.Contains('ApplySettingsRowSnippet') -and
    $externalStarterKitWorkbenchSource.Contains('ApplyLocaleEntrySnippet') -and
    $externalStarterKitWorkbenchSource.Contains('Tools/apply_settings_row_snippet.ps1') -and
    $externalStarterKitWorkbenchSource.Contains('Tools/apply_locale_entry_snippet.ps1') -and
    $externalStarterKitWorkbenchSource.Contains('rejects duplicates unless the CLI -Replace switch is explicit') -and
    $externalStarterKitWorkbenchSource.Contains('restores the previous file on failure')
$externalStarterKitWorkbenchBuildsSubmissionPackage =
    $externalStarterKitWorkbenchSource.Contains('Build Submission Package') -and
    $externalStarterKitWorkbenchSource.Contains('Tools/build_submission_package.ps1')
$externalStarterKitWorkbenchShowsSubmissionPackageStatus =
    $externalStarterKitWorkbenchSource.Contains('Submission Package') -and
    $externalStarterKitWorkbenchSource.Contains('LoadSubmissionSummary') -and
    $externalStarterKitWorkbenchSource.Contains('OpenSubmissionPackage') -and
    $externalStarterKitWorkbenchSource.Contains('Package freshness: current against review manifest') -and
    $externalStarterKitWorkbenchSource.Contains('Generated Folder') -and
    $externalStarterKitContractText.Contains('shows current submission package path/freshness')
$externalStarterKitWorkbenchShowsSubmissionZipIntegrity =
    $externalStarterKitWorkbenchSource.Contains('TryBuildSubmissionZipIntegritySummary') -and
    $externalStarterKitWorkbenchSource.Contains('Zip integrity: verified') -and
    $externalStarterKitWorkbenchSource.Contains('MaxSubmissionIntegrityEntries') -and
    $externalStarterKitWorkbenchSource.Contains('MaxSubmissionIntegrityEntryBytes') -and
    $externalStarterKitWorkbenchSource.Contains('Reports/review_manifest.json') -and
    $externalStarterKitWorkbenchSource.Contains('IsSafeSubmissionZipEntry') -and
    $externalStarterKitContractText.Contains('shows current submission package path/freshness plus case-exact zip path integrity')
$externalStarterKitWorkbenchUsesCaseExactSubmissionZipIntegrity =
    ($externalStarterKitWorkbenchSource -match 'new\s+Dictionary<string,\s*SubmissionExpectedEntry>\(review\.Files\.Length \+ 1,\s*StringComparer\.Ordinal\)') -and
    ($externalStarterKitWorkbenchSource -match 'new\s+Dictionary<string,\s*ZipArchiveEntry>\(Math\.Max\(1,\s*expectedEntries\.Count\),\s*StringComparer\.Ordinal\)') -and
    $externalStarterKitWorkbenchSource.Contains('MaxSubmissionIntegrityReviewManifestBytes') -and
    $externalStarterKitWorkbenchSource.Contains('expectedCaseFoldPaths') -and
    $externalStarterKitWorkbenchSource.Contains('zipCaseFoldPaths') -and
    $externalStarterKitWorkbenchSource.Contains('IsSha256Hex') -and
    $externalStarterKitWorkbenchSource.Contains('Path match: case-exact') -and
    $externalStarterKitContractText.Contains('case-exact zip path integrity')
$externalStarterKitWorkbenchReportsReservedFolderCaseVariants =
    $externalStarterKitWorkbenchSource.Contains('ReservedStarterTopLevelFolders') -and
    $externalStarterKitWorkbenchSource.Contains('CountReservedTopLevelCaseVariants') -and
    $externalStarterKitWorkbenchSource.Contains('Reserved folder casing mismatches') -and
    $externalStarterKitWorkbenchSource.Contains('CASE_MISMATCH') -and
    $externalStarterKitWorkbenchSource.Contains('Rename to exact starter contract casing') -and
    $externalStarterKitContractText.Contains('reserved top-level folder case variants')
$externalStarterKitPackageProofRequiresLowercaseSha256 =
    $externalStarterKitWorkbenchSource.Contains("bool lower = c >= 'a' && c <= 'f';") -and
    $externalStarterKitWorkbenchSource.Contains('StringComparison.Ordinal)') -and
    $externalStarterKitTemplateDoctorToolSource.Contains('$isLowerHex = $ch -ge ''a'' -and $ch -le ''f''') -and
    $externalStarterKitTemplateSubmissionPackageToolSource.Contains("^[0-9a-f]{64}$") -and
    $externalStarterKitContractText.Contains('lowercase SHA-256')
$externalStarterKitWorkbenchRunsDoctor =
    $externalStarterKitWorkbenchSource.Contains('Run Package Doctor') -and
    $externalStarterKitWorkbenchSource.Contains('RunPackageDoctor') -and
    $externalStarterKitWorkbenchSource.Contains('Open Doctor Tool') -and
    $externalStarterKitWorkbenchSource.Contains('Tools/run_doctor.ps1') -and
    $externalStarterKitContractText.Contains('run the package doctor')
$externalStarterKitWorkbenchShowsAuthoringDataPreview =
    $externalStarterKitWorkbenchSource.Contains('Authoring Data Preview') -and
    $externalStarterKitWorkbenchSource.Contains('LoadAuthoringDataPreview') -and
    $externalStarterKitWorkbenchSource.Contains('MaxSettingsPreviewRows') -and
    $externalStarterKitWorkbenchSource.Contains('MaxLocalePreviewStrings') -and
    $externalStarterKitWorkbenchSource.Contains('Invalid settings rows') -and
    $externalStarterKitWorkbenchSource.Contains('Invalid locale keys') -and
    $externalStarterKitWorkbenchSource.Contains('ValidateLocaleStringsPreview') -and
    $externalStarterKitContractText.Contains('authoring data preview')
$externalStarterKitWorkbenchShowsContentAssetPreview =
    $externalStarterKitWorkbenchSource.Contains('MaxAssetPreviewEntries') -and
    $externalStarterKitWorkbenchSource.Contains('AssetManifestDocument') -and
    $externalStarterKitWorkbenchSource.Contains('Content/assets.h8manifest.json') -and
    $externalStarterKitWorkbenchSource.Contains('Content assets:') -and
    $externalStarterKitWorkbenchSource.Contains('Missing content files:') -and
    $externalStarterKitWorkbenchSource.Contains('Invalid content entries:') -and
    $externalStarterKitWorkbenchSource.Contains('Content bytes:')
$externalStarterKitWorkbenchGeneratesAssetEntrySnippet =
    $externalStarterKitWorkbenchSource.Contains('Content Asset Snippet') -and
    $externalStarterKitWorkbenchSource.Contains('GenerateAssetEntrySnippet') -and
    $externalStarterKitWorkbenchSource.Contains('DrawAssetKindPicker') -and
    $externalStarterKitWorkbenchSource.Contains('Tools/create_asset_entry_snippet.ps1') -and
    $externalStarterKitWorkbenchSource.Contains('Generated/asset_entry_snippet.json')
$externalStarterKitWorkbenchAppliesAssetEntrySnippet =
    $externalStarterKitWorkbenchSource.Contains('Apply Asset Snippet') -and
    $externalStarterKitWorkbenchSource.Contains('ApplyAssetEntrySnippet') -and
    $externalStarterKitWorkbenchSource.Contains('Tools/apply_asset_entry_snippet.ps1') -and
    $externalStarterKitWorkbenchSource.Contains('raises MaxAssetBytes') -and
    $externalStarterKitWorkbenchSource.Contains('restores previous files on failure')
$externalStarterKitWorkbenchConfiguresManifestContract =
    $externalStarterKitWorkbenchSource.Contains('Manifest Contract') -and
    $externalStarterKitWorkbenchSource.Contains('ConfigureManifestContract') -and
    $externalStarterKitWorkbenchSource.Contains('Tools/configure_manifest_contract.ps1') -and
    $externalStarterKitWorkbenchSource.Contains('review metadata from a public allowlist, not runtime rights') -and
    $externalStarterKitWorkbenchSource.Contains('budgets are capped and cannot be lowered below the current graph or asset manifest requirements') -and
    $externalStarterKitWorkbenchSource.Contains('Apply Manifest Contract + Validate')
$externalStarterKitWorkbenchConfiguresDependencies =
    $externalStarterKitWorkbenchSource.Contains('Dependency Contract') -and
    $externalStarterKitWorkbenchSource.Contains('ConfigureDependencies') -and
    $externalStarterKitWorkbenchSource.Contains('Tools/configure_dependencies.ps1') -and
    $externalStarterKitWorkbenchSource.Contains('Add Dependency + Validate') -and
    $externalStarterKitWorkbenchSource.Contains('Remove Dependency + Validate') -and
    $externalStarterKitWorkbenchSource.Contains('Dependency order is loader metadata only; it does not grant runtime code execution rights.')
$externalStarterKitWorkbenchChecksRootLauncher =
    $externalStarterKitWorkbenchSource.Contains('"h8mod.ps1"') -and
    $externalStarterKitWorkbenchSource.Contains('Open Root Launcher') -and
    $externalStarterKitWorkbenchSource.Contains('ModdingSDK/ExternalStarterKit/h8mod.ps1')
$externalStarterKitWorkbenchReadAllTextCount =
    ([regex]::Matches($externalStarterKitWorkbenchSource, 'File\.ReadAllText\(')).Count
$externalStarterKitWorkbenchUsesStreamingCappedPreviewReads =
    $externalStarterKitWorkbenchSource.Contains('File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)') -and
    $externalStarterKitWorkbenchSource.Contains('const int ChunkBytes = 8192') -and
    $externalStarterKitWorkbenchSource.Contains('totalBytes > byteLimit') -and
    $externalStarterKitWorkbenchSource.Contains('StrictUtf8NoBom.GetString(bytes, 0, totalBytes)') -and
    $externalStarterKitWorkbenchReadAllTextCount -eq 0 -and
    $externalStarterKitContractText.Contains('Workbench previews stream starter JSON/text reads with hard byte caps') -and
    $runtimePlaybookText.Contains('ExternalStarterKitWorkbenchUsesStreamingCappedPreviewReads = True') -and
    $specText.Contains('schema revision `123`')
$externalStarterKitWorkbenchRejectsInvalidUtf8PreviewReads =
    $externalStarterKitWorkbenchUsesStreamingCappedPreviewReads -and
    $externalStarterKitWorkbenchSource.Contains('new UTF8Encoding(false, true)') -and
    $externalStarterKitWorkbenchSource.Contains('DecoderFallbackException') -and
    $externalStarterKitWorkbenchSource.Contains('is not strict UTF-8') -and
    $externalStarterKitContractText.Contains('Workbench preview reads reject invalid UTF-8 before JSON parsing') -and
    $runtimePlaybookText.Contains('ExternalStarterKitWorkbenchRejectsInvalidUtf8PreviewReads = True') -and
    $specText.Contains('schema revision `123`')
$externalStarterKitWorkbenchUsesBoundedPreviewReads =
    $externalStarterKitWorkbenchSource.Contains('MaxAuthoringManifestPreviewBytes = 65536L') -and
    $externalStarterKitWorkbenchSource.Contains('MaxGraphPreviewBytes = 262144L') -and
    $externalStarterKitWorkbenchSource.Contains('MaxSettingsPreviewBytes = 262144L') -and
    $externalStarterKitWorkbenchSource.Contains('MaxLocalePreviewBytes = 2097152L') -and
    $externalStarterKitWorkbenchSource.Contains('MaxAssetManifestPreviewBytes = 262144L') -and
    $externalStarterKitWorkbenchSource.Contains('MaxReviewManifestPreviewBytes = 1048576L') -and
    $externalStarterKitWorkbenchSource.Contains('ReadTextFileCapped') -and
    $externalStarterKitWorkbenchSource.Contains('ReadJsonFileCapped<AuthoringManifest>') -and
    $externalStarterKitWorkbenchSource.Contains('ReadJsonFileCapped<GraphDocument>') -and
    $externalStarterKitWorkbenchSource.Contains('ReadJsonFileCapped<SettingsTableDocument>') -and
    $externalStarterKitWorkbenchSource.Contains('ReadJsonFileCapped<LocaleDocument>') -and
    $externalStarterKitWorkbenchSource.Contains('ReadJsonFileCapped<AssetManifestDocument>') -and
    $externalStarterKitWorkbenchSource.Contains('ReadJsonFileCapped<ReviewManifest>') -and
    $externalStarterKitWorkbenchUsesStreamingCappedPreviewReads -and
    $externalStarterKitContractText.Contains('Workbench previews cap starter JSON/text reads before parsing') -and
    $runtimePlaybookText.Contains('ExternalStarterKitWorkbenchUsesBoundedPreviewReads = True') -and
    $specText.Contains('schema revision `123`')
Assert-True $moddingSdkHubPresent 'ModdingSdkHubWindow must expose Hecton/Modding/SDK Hub.'
Assert-True $moddingSdkHubOpensBuilder 'ModdingSdkHubWindow must open ModBuilderWindow.'
Assert-True $moddingSdkHubOpensStarterWorkbench 'ModdingSdkHubWindow must open the External Starter Kit Workbench from public authoring actions.'
Assert-True $moddingSdkHubPrioritizesExternalStarterKit 'ModdingSdkHubWindow must present External Starter Kit before the legacy builder.'
Assert-True $moddingSdkHubGatesLegacyBuilder 'ModdingSdkHubWindow must gate the legacy builder behind an explicit internal warning.'
Assert-True $modBuilderMenuIsInternalLegacy 'ModBuilderWindow menu and UI must be marked internal legacy.'
Assert-True $moddingSdkHubLinksCoreDocs 'ModdingSdkHubWindow must link core SDK docs.'
Assert-True $moddingSdkHubRunsStaticValidator 'ModdingSdkHubWindow must launch Validate_Mod_API_Static.ps1.'
Assert-True $moddingSdkHubRunsStaticValidatorAsync 'ModdingSdkHubWindow must run the static validator asynchronously without blocking ReadToEnd/WaitForExit.'
Assert-True $moddingSdkHubShowsValidatorFailuresAsErrors 'ModdingSdkHubWindow must show failed static validator runs as Editor error UI.'
Assert-True $moddingSdkHubShowsEnvelopeOnlyBoundary 'ModdingSdkHubWindow must show the envelope-only runtime boundary.'
Assert-True $externalStarterKitWorkbenchPresent 'ExternalStarterKitWorkbenchWindow must expose a dedicated starter kit workbench menu.'
Assert-True $externalStarterKitWorkbenchUsesIdentityTool 'External Starter Kit Workbench must route identity edits through set_mod_identity.ps1.'
Assert-True $externalStarterKitWorkbenchUsesPrepareTool 'External Starter Kit Workbench must route validation/review through prepare_mod.ps1.'
Assert-True $externalStarterKitWorkbenchCanRefreshStarterKit 'External Starter Kit Workbench must reuse the SDK Hub starter kit generator.'
Assert-True $externalStarterKitWorkbenchListsOpcodes 'External Starter Kit Workbench must expose graph opcode discovery.'
Assert-True $externalStarterKitWorkbenchShowsReviewSummary 'External Starter Kit Workbench must show review manifest identity/file summary.'
Assert-True $externalStarterKitWorkbenchShowsEnvelopeBoundary 'External Starter Kit Workbench must show the envelope-only boundary.'
Assert-True $externalStarterKitWorkbenchShowsStarterHealth 'External Starter Kit Workbench must show required starter-file health.'
Assert-True $externalStarterKitWorkbenchShowsCapabilityMatrix 'External Starter Kit Workbench must show a capability matrix for supported and forbidden mod authoring rights.'
Assert-True $externalStarterKitWorkbenchUsesCurrentSchemaHealthList 'External Starter Kit Workbench required-file health list must match the current starter validator schema paths.'
Assert-True $externalStarterKitWorkbenchRunsStructureValidator 'External Starter Kit Workbench must run the local structure validator directly.'
Assert-True $externalStarterKitWorkbenchLinksCoreDocs 'External Starter Kit Workbench must link core starter/API docs.'
Assert-True $externalStarterKitWorkbenchOpensVsCodeTasks 'External Starter Kit Workbench must open VS Code settings and task contract files.'
Assert-True $externalStarterKitWorkbenchRunsToolsAsync 'External Starter Kit Workbench must run starter tools asynchronously without blocking stdout/stderr reads.'
Assert-True $externalStarterKitWorkbenchShowsToolFailuresAsErrors 'External Starter Kit Workbench must show failed starter tool runs as Editor error UI.'
Assert-True $externalStarterKitWorkbenchShowsReviewFreshness 'External Starter Kit Workbench must show bounded review manifest freshness against starter sources.'
Assert-True $externalStarterKitWorkbenchShowsGraphContractPreview 'External Starter Kit Workbench must show a graph contract preview for graph budget, duplicate IDs, and invalid opcodes.'
Assert-True $externalStarterKitWorkbenchGeneratesGraphNodeSnippet 'External Starter Kit Workbench must generate graph node snippets through the no-Unity snippet helper.'
Assert-True $externalStarterKitWorkbenchHasGraphOpcodePicker 'External Starter Kit Workbench must expose a graph opcode picker from Reference/allowed_opcodes.csv.'
Assert-True $externalStarterKitWorkbenchEditsGraphNodeParameters 'External Starter Kit Workbench must expose graph node Parameters JSON, disabled-node, and replace-on-apply controls.'
Assert-True $externalStarterKitWorkbenchAppliesGraphNodeSnippet 'External Starter Kit Workbench must apply graph node snippets through the bounded no-Unity helper with duplicate rejection and rollback.'
Assert-True $externalStarterKitWorkbenchGeneratesAuthoringSnippets 'External Starter Kit Workbench must generate settings and locale snippets through no-Unity helpers without mutating authoring data files.'
Assert-True $externalStarterKitWorkbenchAppliesAuthoringSnippets 'External Starter Kit Workbench must apply settings and locale snippets through bounded no-Unity helpers with duplicate rejection and rollback.'
Assert-True $externalStarterKitWorkbenchBuildsSubmissionPackage 'External Starter Kit Workbench must build a no-Unity submission package from the reviewed starter kit.'
Assert-True $externalStarterKitWorkbenchShowsSubmissionPackageStatus 'External Starter Kit Workbench must show current submission zip path/freshness and expose the Generated handoff folder.'
Assert-True $externalStarterKitWorkbenchShowsSubmissionZipIntegrity 'External Starter Kit Workbench must show submission zip integrity against Reports/review_manifest.json with bounded entry/hash checks.'
Assert-True $externalStarterKitWorkbenchUsesCaseExactSubmissionZipIntegrity 'External Starter Kit Workbench submission zip integrity must use exact path casing, reject case-fold duplicates, and cap review manifest bytes.'
Assert-True $externalStarterKitWorkbenchReportsReservedFolderCaseVariants 'External Starter Kit Workbench must report reserved top-level folder case variants in starter health.'
Assert-True $externalStarterKitPackageProofRequiresLowercaseSha256 'External starter kit package proof must require lowercase SHA-256 rows and exact hash comparison.'
Assert-True $externalStarterKitWorkbenchRunsDoctor 'External Starter Kit Workbench must run the no-Unity package doctor through the bounded starter tool.'
Assert-True $externalStarterKitWorkbenchShowsAuthoringDataPreview 'External Starter Kit Workbench must show settings and locale authoring data preview before submission handoff.'
Assert-True $externalStarterKitWorkbenchShowsContentAssetPreview 'External Starter Kit Workbench must show content asset manifest state, missing files, invalid entries, and byte totals.'
Assert-True $externalStarterKitWorkbenchGeneratesAssetEntrySnippet 'External Starter Kit Workbench must generate content asset snippets through the no-Unity helper.'
Assert-True $externalStarterKitWorkbenchAppliesAssetEntrySnippet 'External Starter Kit Workbench must apply content asset snippets through the bounded no-Unity helper with duplicate rejection, budget repair, and rollback.'
Assert-True $externalStarterKitWorkbenchConfiguresManifestContract 'External Starter Kit Workbench must configure manifest capabilities/budgets through the bounded no-Unity helper.'
Assert-True $externalStarterKitWorkbenchConfiguresDependencies 'External Starter Kit Workbench must configure dependency metadata through the bounded no-Unity helper.'
Assert-True $externalStarterKitWorkbenchChecksRootLauncher 'External Starter Kit Workbench must include the root no-Unity h8mod.ps1 launcher in health and file access.'
Assert-True $externalStarterKitWorkbenchUsesBoundedPreviewReads 'External Starter Kit Workbench must cap starter JSON/text preview reads before parsing.'
Assert-True $externalStarterKitWorkbenchUsesStreamingCappedPreviewReads 'External Starter Kit Workbench must stream capped starter JSON/text preview reads without raw File.ReadAllText.'
Assert-True $externalStarterKitWorkbenchRejectsInvalidUtf8PreviewReads 'External Starter Kit Workbench must reject invalid UTF-8 starter previews before JSON parsing.'
$externalStarterKitGeneratorPresent =
    $moddingSdkHubSource.Contains('ExternalStarterKitRoot = "ModdingSDK/ExternalStarterKit"') -and
    $moddingSdkHubSource.Contains('CreateExternalStarterKit') -and
    $moddingSdkHubSource.Contains('Create External Starter Kit') -and
    $moddingSdkHubSource.Contains('Open External Starter Kit')
$externalStarterKitWritesRootLauncher =
    $moddingSdkHubSource.Contains('"h8mod.ps1"') -and
    $moddingSdkHubSource.Contains('BuildStarterKitToolFromTemplate("h8mod.ps1")') -and
    $externalStarterKitTemplateLauncherSource.Contains("ValidateSet('menu','first-mod','install-local','diagnose-local','doctor','dependencies','setup','validate','review','prepare','submission','opcodes','opcodes-json','node-snippet','apply-node-snippet','setting-snippet','locale-snippet','apply-setting-snippet','apply-locale-snippet','asset-snippet','apply-asset-snippet','manifest-contract','capabilities')") -and
    $externalStarterKitTemplateLauncherSource.Contains("Resolve-StarterTool 'Tools/prepare_mod.ps1'") -and
    $externalStarterKitTemplateLauncherSource.Contains("Resolve-StarterTool 'Tools/validate_structure.ps1'") -and
    $externalStarterKitTemplateLauncherSource.Contains("Resolve-StarterTool 'Tools/build_review_manifest.ps1'") -and
    $externalStarterKitTemplateLauncherSource.Contains("Resolve-StarterTool 'Tools/build_submission_package.ps1'") -and
    $externalStarterKitTemplateLauncherSource.Contains("Resolve-StarterTool 'Tools/install_local_mod.ps1'") -and
    $externalStarterKitTemplateLauncherSource.Contains("Resolve-StarterTool 'Tools/diagnose_local_mods.ps1'") -and
    $externalStarterKitTemplateLauncherSource.Contains("Resolve-StarterTool 'Tools/run_doctor.ps1'") -and
    $externalStarterKitTemplateLauncherSource.Contains("Resolve-StarterTool 'Tools/list_allowed_opcodes.ps1'") -and
    $externalStarterKitTemplateLauncherSource.Contains("Resolve-StarterTool 'Tools/create_graph_node_snippet.ps1'") -and
    $externalStarterKitTemplateLauncherSource.Contains("Resolve-StarterTool 'Tools/apply_graph_node_snippet.ps1'") -and
    $externalStarterKitTemplateLauncherSource.Contains("Resolve-StarterTool 'Tools/create_settings_row_snippet.ps1'") -and
    $externalStarterKitTemplateLauncherSource.Contains("Resolve-StarterTool 'Tools/create_locale_entry_snippet.ps1'") -and
    $externalStarterKitTemplateLauncherSource.Contains("Resolve-StarterTool 'Tools/apply_settings_row_snippet.ps1'") -and
    $externalStarterKitTemplateLauncherSource.Contains("Resolve-StarterTool 'Tools/apply_locale_entry_snippet.ps1'") -and
    $externalStarterKitTemplateLauncherSource.Contains("Resolve-StarterTool 'Tools/create_asset_entry_snippet.ps1'") -and
    $externalStarterKitTemplateLauncherSource.Contains("Resolve-StarterTool 'Tools/apply_asset_entry_snippet.ps1'") -and
    $externalStarterKitTemplateLauncherSource.Contains("Resolve-StarterTool 'Tools/configure_manifest_contract.ps1'") -and
    $externalStarterKitTemplateLauncherSource.Contains('Invoke-Capabilities')
$externalStarterKitWritesAuthoringManifest =
    $moddingSdkHubSource.Contains('mod.h8manifest.json') -and
    $moddingSdkHubSource.Contains('BuildAuthoringManifestTemplate') -and
    $moddingSdkHubSource.Contains('hecton8.h8mod.authoring.v1')
$externalStarterKitWritesRuntimeManifest =
    $moddingSdkHubSource.Contains('mod.json') -and
    $moddingSdkHubSource.Contains('BuildRuntimeManifestTemplate') -and
    $moddingSdkHubSource.Contains('EntryAssembly') -and
    $moddingSdkHubSource.Contains('EntryType')
$externalStarterKitWritesFolderReadmes =
    $moddingSdkHubSource.Contains('BuildContentReadme') -and
    $moddingSdkHubSource.Contains('BuildContentAssetsReadme') -and
    $moddingSdkHubSource.Contains('BuildGeneratedReadme') -and
    $moddingSdkHubSource.Contains('BuildReportsReadme') -and
    $moddingSdkHubSource.Contains('BuildReferenceReadme')
$externalStarterKitWritesCapabilityGuide =
    (Test-Path -LiteralPath (Join-Path $externalStarterKitTemplatePath 'Docs\capabilities.md') -PathType Leaf) -and
    $moddingSdkHubSource.Contains('BuildCapabilitiesGuide') -and
    $moddingSdkHubSource.Contains('Mod Capability Matrix') -and
    (Get-Content -Raw -LiteralPath (Join-Path $externalStarterKitTemplatePath 'README.md')).Contains('Docs/capabilities.md') -and
    $externalStarterKitContractText.Contains('Docs/capabilities.md')
$externalStarterKitCopiesOpcodeReferences =
    $moddingSdkHubSource.Contains('AllowedOpcodesReferencePath') -and
    $moddingSdkHubSource.Contains('KernelTuningProfilesReferencePath') -and
    $moddingSdkHubSource.Contains('CopyReferenceFileIfMissing')
$externalStarterKitDocumentsNoUnityProjectRequirement =
    $moddingSdkHubSource.Contains('No Unity project is required') -and
    $externalStarterKitContractText.Contains('ordinary mod authors do not need the full HECTON-8 Unity project') -and
    $externalStarterKitContractText.Contains('Unity is optional')
$externalStarterKitDocumentsEnvelopeOnlyBoundary =
    $moddingSdkHubSource.Contains('Current runtime UGC ingress is envelope-only') -and
    $externalStarterKitContractText.Contains('runtime gameplay authority is validated 64-byte') -and
    $externalStarterKitContractText.Contains('Runtime stays envelope-only')
$externalStarterKitWritesLocalStructureValidator =
    $moddingSdkHubSource.Contains('Tools", "validate_structure.ps1"') -and
    $moddingSdkHubSource.Contains('BuildStarterKitValidatorScript') -and
    $moddingSdkHubSource.Contains('BuildStarterKitToolFromTemplate("Tools/validate_structure.ps1")') -and
    $externalStarterKitTemplateValidatorSource.Contains('H8MOD_STARTER_VALIDATION') -and
    $externalStarterKitContractText.Contains('Tools/validate_structure.ps1')
$externalStarterKitValidatorChecksRequiredFiles =
    $externalStarterKitTemplateValidatorSource.Contains('function Require-File') -and
    $externalStarterKitTemplateValidatorSource.Contains('function Require-Directory') -and
    $externalStarterKitTemplateValidatorSource.Contains("'Docs/capabilities.md'") -and
    $moddingSdkHubSource.Contains('"Content", "Assets", "README.md"') -and
    $externalStarterKitTemplateValidatorSource.Contains("'Tools/build_submission_package.ps1'") -and
    $externalStarterKitTemplateValidatorSource.Contains("'Content/Assets/README.md'") -and
    $externalStarterKitTemplateValidatorSource.Contains("'Tools/apply_graph_node_snippet.ps1'") -and
    $externalStarterKitTemplateValidatorSource.Contains("'Tools/create_settings_row_snippet.ps1'") -and
    $externalStarterKitTemplateValidatorSource.Contains("'Tools/create_locale_entry_snippet.ps1'") -and
    $externalStarterKitTemplateValidatorSource.Contains("'Tools/apply_settings_row_snippet.ps1'") -and
    $externalStarterKitTemplateValidatorSource.Contains("'Tools/apply_locale_entry_snippet.ps1'") -and
    $externalStarterKitTemplateValidatorSource.Contains("'Tools/create_asset_entry_snippet.ps1'") -and
    $externalStarterKitTemplateValidatorSource.Contains("'Tools/apply_asset_entry_snippet.ps1'") -and
    $externalStarterKitTemplateValidatorSource.Contains("'Tools/configure_dependencies.ps1'") -and
    $externalStarterKitTemplateValidatorSource.Contains("'Tools/configure_manifest_contract.ps1'") -and
    $externalStarterKitTemplateValidatorSource.Contains("'Tools/install_local_mod.ps1'") -and
    $externalStarterKitTemplateValidatorSource.Contains("'Tools/diagnose_local_mods.ps1'") -and
    $externalStarterKitTemplateValidatorSource.Contains("'Tools/run_doctor.ps1'") -and
    $externalStarterKitTemplateValidatorSource.Contains("'.vscode/tasks.json'") -and
    $externalStarterKitTemplateValidatorSource.Contains('[switch]$ThrowInsteadOfExit') -and
    $externalStarterKitTemplateValidatorSource.Contains("'Reference/allowed_opcodes.csv'") -and
    $externalStarterKitTemplateValidatorSource.Contains("'Reference/kernel_tuning_profiles.csv'")
$externalStarterKitValidatorChecksCapabilityGuide =
    $externalStarterKitTemplateValidatorSource.Contains('Docs/capabilities.md missing required capability text') -and
    $externalStarterKitTemplateValidatorSource.Contains('h8mod.ps1 -Action capabilities') -and
    $externalStarterKitTemplateValidatorSource.Contains('h8mod.ps1 -Action setting-snippet') -and
    $externalStarterKitTemplateValidatorSource.Contains('h8mod.ps1 -Action locale-snippet') -and
    $externalStarterKitTemplateValidatorSource.Contains('h8mod.ps1 -Action asset-snippet') -and
    $externalStarterKitTemplateValidatorSource.Contains('h8mod.ps1 -Action apply-asset-snippet') -and
    $externalStarterKitTemplateValidatorSource.Contains('h8mod.ps1 -Action manifest-contract') -and
    $externalStarterKitTemplateValidatorSource.Contains('h8mod.ps1 -Action dependencies') -and
    $externalStarterKitTemplateValidatorSource.Contains('h8mod.ps1 -Action install-local') -and
    $externalStarterKitTemplateValidatorSource.Contains('install_local_mod.ps1') -and
    $externalStarterKitTemplateValidatorSource.Contains('h8mod.ps1 -Action diagnose-local') -and
    $externalStarterKitTemplateValidatorSource.Contains('diagnose_local_mods.ps1') -and
    $externalStarterKitTemplateValidatorSource.Contains('h8mod.ps1 -Action doctor') -and
    $externalStarterKitTemplateValidatorSource.Contains('run_doctor.ps1') -and
    $externalStarterKitTemplateValidatorSource.Contains('review manifest freshness') -and
    $externalStarterKitTemplateValidatorSource.Contains('submission zip freshness') -and
    $externalStarterKitTemplateValidatorSource.Contains('case-exact zip path integrity') -and
    $externalStarterKitTemplateValidatorSource.Contains('configure_dependencies.ps1') -and
    $externalStarterKitTemplateValidatorSource.Contains('configure_manifest_contract.ps1') -and
    $externalStarterKitContractText.Contains('rejects missing capability guide text')
$externalStarterKitValidatorChecksEnvelopeOnly =
    $externalStarterKitTemplateValidatorSource.Contains('Compatibility.Runtime must be envelope-only') -and
    $externalStarterKitTemplateValidatorSource.Contains('Runtime must be envelope-only')
$externalStarterKitValidatorChecksManagedEntryDisabled =
    $externalStarterKitTemplateValidatorSource.Contains('EntryAssembly must stay empty in envelope-only starter kits') -and
    $externalStarterKitTemplateValidatorSource.Contains('EntryType must stay empty in envelope-only starter kits')
$externalStarterKitValidatorChecksCanonicalIds =
    $externalStarterKitTemplateValidatorSource.Contains('function Validate-ModId') -and
    $externalStarterKitTemplateValidatorSource.Contains('reserved filesystem device segment') -and
    $externalStarterKitTemplateValidatorSource.Contains('^[a-z0-9]+([._-][a-z0-9]+)*$')
$externalStarterKitValidatorChecksManifestIdParity =
    $externalStarterKitTemplateValidatorSource.Contains('mod.h8manifest.json Id must match mod.json Id')
$externalStarterKitValidatorChecksDependencyIds =
    $externalStarterKitTemplateValidatorSource.Contains('function Validate-DependencyList') -and
    $externalStarterKitTemplateValidatorSource.Contains('mod.h8manifest.json Dependencies must match mod.json Dependencies in the same order') -and
    $externalStarterKitTemplateValidatorSource.Contains('must not contain self dependency') -and
    $externalStarterKitTemplateValidatorSource.Contains('contains duplicate dependency')
$externalStarterKitInvalidGraphOpcodeProbe = Invoke-StarterInvalidGraphOpcodeProbe $externalStarterKitTemplatePath
$externalStarterKitValidatorRejectsInvalidGraphOpcode =
    $externalStarterKitInvalidGraphOpcodeProbe.ExitCode -ne 0
$externalStarterKitValidatorChecksGraphOpcodes =
    $externalStarterKitTemplateValidatorSource.Contains('function Read-AllowedGraphOpcodeTokens') -and
    $externalStarterKitTemplateValidatorSource.Contains('Reference/allowed_opcodes.csv contains invalid opcode token') -and
    $externalStarterKitTemplateValidatorSource.Contains('node Opcode is not in Reference/allowed_opcodes.csv') -and
    $externalStarterKitTemplateValidatorSource.Contains('duplicate node Id') -and
    $externalStarterKitTemplateValidatorSource.Contains('Nodes exceeds 256 entries') -and
    $externalStarterKitTemplateValidatorSource.Contains('node Opcode is required') -and
    $externalStarterKitContractText.Contains('graph opcode allowlist') -and
    $externalStarterKitValidatorRejectsInvalidGraphOpcode
$externalStarterKitValidatorChecksGraphBudget =
    $externalStarterKitTemplateValidatorSource.Contains('MaxEnvelopesPerFrame must not exceed mod.h8manifest.json Budgets.MaxEnvelopesPerFrame') -and
    $externalStarterKitTemplateValidatorSource.Contains('MaxEnvelopesPerFrame must be >= 1 when opcode nodes exist') -and
    $externalStarterKitContractText.Contains('graph budget parity')
$externalStarterKitWritesReviewManifestBuilder =
    $moddingSdkHubSource.Contains('Tools", "build_review_manifest.ps1"') -and
    $moddingSdkHubSource.Contains('BuildStarterKitReviewManifestScript') -and
    $moddingSdkHubSource.Contains('BuildStarterKitToolFromTemplate("Tools/build_review_manifest.ps1")') -and
    $externalStarterKitTemplateReviewManifestBuilderSource.Contains('H8MOD_REVIEW_MANIFEST') -and
    $externalStarterKitContractText.Contains('Tools/build_review_manifest.ps1')
$externalStarterKitWritesSubmissionPackageTool =
    $moddingSdkHubSource.Contains('Tools", "build_submission_package.ps1"') -and
    $moddingSdkHubSource.Contains('BuildStarterKitSubmissionPackageScript') -and
    $moddingSdkHubSource.Contains('BuildStarterKitToolFromTemplate("Tools/build_submission_package.ps1")') -and
    $externalStarterKitTemplateSubmissionPackageToolSource.Contains('H8MOD_SUBMISSION_PACKAGE') -and
    $externalStarterKitTemplateSubmissionPackageToolSource.Contains('hecton8.external_review_manifest.v1') -and
    $externalStarterKitTemplateSubmissionPackageToolSource.Contains('Add-Type -AssemblyName System.IO.Compression ') -and
    $externalStarterKitTemplateSubmissionPackageToolSource.Contains('Add-Type -AssemblyName System.IO.Compression.FileSystem') -and
    $externalStarterKitTemplateSubmissionPackageToolSource.Contains("Generated/' + `$packageId + '_submission.zip'") -and
    $externalStarterKitContractText.Contains('Tools/build_submission_package.ps1')
$externalStarterKitSubmissionPackagePreservesPreviousOutputUntilSuccess =
    $externalStarterKitTemplateSubmissionPackageToolSource.Contains('Submission package zip write failed') -and
    $externalStarterKitTemplateSubmissionPackageToolSource.Contains('Submission package zip replace failed') -and
    $externalStarterKitContractText.Contains('restores the previous submission zip') -and
    $externalStarterKitTemplateSubmissionPackageToolSource.Contains('New-TempArtifactPath') -and
    $externalStarterKitTemplateSubmissionPackageToolSource.Contains('previousCopiedToBackup') -and
    $externalStarterKitTemplateSubmissionPackageToolSource.Contains("Copy-Item -LiteralPath `$outputPath -Destination `$backupOutputPath -Force") -and
    $externalStarterKitTemplateSubmissionPackageToolSource.Contains("Copy-Item -LiteralPath `$tempOutputPath -Destination `$outputPath -Force") -and
    $externalStarterKitTemplateSubmissionPackageToolSource.Contains("Copy-Item -LiteralPath `$backupOutputPath -Destination `$outputPath -Force -ErrorAction SilentlyContinue")
$externalStarterKitSubmissionPackageRefreshesTimestampAfterReplace =
    $externalStarterKitTemplateSubmissionPackageToolSource.Contains('$reviewTimestampUtc = (Get-Item -LiteralPath $reviewPath).LastWriteTimeUtc') -and
    $externalStarterKitTemplateSubmissionPackageToolSource.Contains('$outputItem.LastWriteTimeUtc = $reviewTimestampUtc.AddSeconds(1)')
$externalStarterKitReviewManifestRejectsCaseFoldSourceDuplicates =
    $externalStarterKitTemplateReviewManifestBuilderSource.Contains('Assert-StandardReviewOutput') -and
    $externalStarterKitTemplateReviewManifestBuilderSource.Contains('Reports/review_manifest.json') -and
    $externalStarterKitTemplateReviewManifestBuilderSource.Contains('Get-H8SafeSourceFiles') -and
    $externalStarterKitTemplateReviewManifestBuilderSource.Contains('Assert-NoFilesystemLinks') -and
    $externalStarterKitTemplateReviewManifestBuilderSource.Contains('Assert-NoCaseFoldDuplicates') -and
    $externalStarterKitTemplateReviewManifestBuilderSource.Contains('reviewRelativePaths') -and
    $externalStarterKitContractText.Contains('case-fold duplicate source paths')
$externalStarterKitSubmissionPackageUsesCaseExactSourceEntries =
    $externalStarterKitTemplateSubmissionPackageToolSource.Contains('$MaxSubmissionPackageEntryBytes = 4194304') -and
    $externalStarterKitTemplateSubmissionPackageToolSource.Contains('Assert-StandardReviewOutput') -and
    $externalStarterKitTemplateSubmissionPackageToolSource.Contains('ReviewOutput path must be exactly Reports/review_manifest.json') -and
    $externalStarterKitTemplateSubmissionPackageToolSource.Contains('Test-Sha256Hex') -and
    $externalStarterKitTemplateSubmissionPackageToolSource.Contains('Get-NumericLong') -and
    $externalStarterKitTemplateSubmissionPackageToolSource.Contains('Assert-H8PathExactCase') -and
    $externalStarterKitTemplateSubmissionPackageToolSource.Contains('Assert-NoFilesystemLinks') -and
    $externalStarterKitTemplateSubmissionPackageToolSource.Contains('Assert-NoCaseFoldDuplicates') -and
    $externalStarterKitTemplateSubmissionPackageToolSource.Contains('Review file SHA-256 does not match current source file') -and
    $externalStarterKitTemplateSubmissionPackageToolSource.Contains(".EndsWith('.zip', [System.StringComparison]::Ordinal)") -and
    $externalStarterKitContractText.Contains('case-exact submission package builder')
$externalStarterKitValidatorRequiresExactPathCasing =
    $externalStarterKitTemplateValidatorSource.Contains('function Test-StarterPathExactCase') -and
    $externalStarterKitTemplateValidatorSource.Contains('Assert-NoReservedTopLevelCaseVariants') -and
    $externalStarterKitTemplateValidatorSource.Contains('Reserved starter top-level folder casing mismatch') -and
    $externalStarterKitTemplateValidatorSource.Contains('[System.StringComparison]::Ordinal') -and
    $externalStarterKitContractText.Contains('required starter paths are exact-case')
$externalStarterKitRejectsReservedTopLevelCaseVariants =
    $externalStarterKitTemplateValidatorSource.Contains('Assert-NoReservedTopLevelCaseVariants') -and
    $externalStarterKitTemplateReviewManifestBuilderSource.Contains('Test-ReservedTopLevelCaseVariant') -and
    $externalStarterKitTemplateSubmissionPackageToolSource.Contains('Test-ReservedTopLevelCaseVariant') -and
    $externalStarterKitTemplateDoctorToolSource.Contains('Test-ReservedTopLevelCaseVariant') -and
    $externalStarterKitContractText.Contains('reserved top-level folder case variants')
$externalStarterKitReviewManifestUsesExactReservedOutputFolders =
    $externalStarterKitTemplateReviewManifestBuilderSource.Contains('$ReservedTopLevelFolders') -and
    $externalStarterKitTemplateReviewManifestBuilderSource.Contains('Test-ReviewOutputPath') -and
    $externalStarterKitTemplateReviewManifestBuilderSource.Contains("StartsWith('Generated/', [System.StringComparison]::Ordinal)") -and
    $externalStarterKitTemplateReviewManifestBuilderSource.Contains("StartsWith('Reports/', [System.StringComparison]::Ordinal)") -and
    $externalStarterKitTemplateReviewManifestBuilderSource.Contains('Output path must be exactly Reports/review_manifest.json') -and
    $externalStarterKitTemplateReviewManifestBuilderSource.Contains('Test-H8GeneratedOrTransientPath') -and
    $externalStarterKitTemplateStrictJsonIoSource.Contains('$script:H8GeneratedOrTransientPathNames') -and
    $externalStarterKitTemplateStrictJsonIoSource.Contains("'node_modules'") -and
    $externalStarterKitTemplateStrictJsonIoSource.Contains("'System Volume Information'") -and
    $externalStarterKitTemplateSubmissionPackageToolSource.Contains('Review manifest must not package generated or transient path') -and
    $externalStarterKitContractText.Contains('review and submission builders exclude only exact `Generated/` and `Reports/` output folders')
$externalStarterKitWritesIdentityTool =
    $moddingSdkHubSource.Contains('Tools", "set_mod_identity.ps1"') -and
    $moddingSdkHubSource.Contains('BuildStarterKitIdentityScript') -and
    $moddingSdkHubSource.Contains('BuildStarterKitToolFromTemplate("Tools/set_mod_identity.ps1")') -and
    $externalStarterKitTemplateIdentityToolSource.Contains('H8MOD_SET_IDENTITY') -and
    $externalStarterKitContractText.Contains('Tools/set_mod_identity.ps1')
$externalStarterKitWritesPrepareTool =
    $moddingSdkHubSource.Contains('Tools", "prepare_mod.ps1"') -and
    $moddingSdkHubSource.Contains('BuildStarterKitPrepareScript') -and
    $moddingSdkHubSource.Contains('BuildStarterKitToolFromTemplate("Tools/prepare_mod.ps1")') -and
    $externalStarterKitTemplatePrepareToolSource.Contains('H8MOD_PREPARE') -and
    $externalStarterKitContractText.Contains('Tools/prepare_mod.ps1')
$externalStarterKitWritesAllowedOpcodeListTool =
    $moddingSdkHubSource.Contains('Tools", "list_allowed_opcodes.ps1"') -and
    $moddingSdkHubSource.Contains('BuildStarterKitAllowedOpcodesScript') -and
    $moddingSdkHubSource.Contains('BuildStarterKitToolFromTemplate("Tools/list_allowed_opcodes.ps1")') -and
    $externalStarterKitTemplateAllowedOpcodeListToolSource.Contains('H8MOD_OPCODE_LIST') -and
    $externalStarterKitContractText.Contains('Tools/list_allowed_opcodes.ps1')
$externalStarterKitAllowedOpcodeListProbe = Invoke-StarterAllowedOpcodeListProbe $externalStarterKitTemplatePath
$externalStarterKitAllowedOpcodeListToolPasses =
    $externalStarterKitAllowedOpcodeListProbe.TextExitCode -eq 0 -and
    $externalStarterKitAllowedOpcodeListProbe.HasTextSpawnItem -eq $true
$externalStarterKitAllowedOpcodeListToolSupportsJson =
    $externalStarterKitAllowedOpcodeListProbe.JsonExitCode -eq 0 -and
    $externalStarterKitAllowedOpcodeListProbe.JsonSchema -eq 'hecton8.allowed_graph_opcodes.v1' -and
    $externalStarterKitAllowedOpcodeListProbe.JsonRuntime -eq 'envelope-only' -and
    $externalStarterKitAllowedOpcodeListProbe.JsonCount -gt 0 -and
    $externalStarterKitAllowedOpcodeListProbe.HasSpawnItemAlias -eq $true
$externalStarterKitWritesGraphNodeSnippetTool =
    $moddingSdkHubSource.Contains('Tools", "create_graph_node_snippet.ps1"') -and
    $moddingSdkHubSource.Contains('BuildStarterKitGraphNodeSnippetScript') -and
    $moddingSdkHubSource.Contains('BuildStarterKitToolFromTemplate("Tools/create_graph_node_snippet.ps1")') -and
    $externalStarterKitTemplateGraphNodeSnippetToolSource.Contains('H8MOD_GRAPH_SNIPPET') -and
    $externalStarterKitContractText.Contains('Tools/create_graph_node_snippet.ps1')
$externalStarterKitGraphNodeSnippetSupportsParameters =
    $externalStarterKitTemplateGraphNodeSnippetToolSource.Contains('[string]$ParametersJson') -and
    $externalStarterKitTemplateGraphNodeSnippetToolSource.Contains('[switch]$Disabled') -and
    $externalStarterKitTemplateGraphNodeSnippetToolSource.Contains('Read-ParametersJson') -and
    $externalStarterKitTemplateGraphNodeSnippetToolSource.Contains('Read-RelaxedParametersObject') -and
    $externalStarterKitTemplateGraphNodeSnippetToolSource.Contains('ParametersJson must be a JSON object') -and
    $externalStarterKitTemplateGraphNodeSnippetToolSource.Contains('Enabled = (-not $Disabled)') -and
    $externalStarterKitTemplateLauncherSource.Contains('NodeParametersJson') -and
    $externalStarterKitTemplateLauncherSource.Contains('NodeDisabled')
$externalStarterKitSnippetCreateToolSources = @(
    $externalStarterKitTemplateGraphNodeSnippetToolSource,
    $externalStarterKitTemplateSettingsRowSnippetToolSource,
    $externalStarterKitTemplateLocaleEntrySnippetToolSource,
    $externalStarterKitTemplateAssetEntrySnippetToolSource
)
$externalStarterKitSnippetApplyToolSources = @(
    $externalStarterKitTemplateGraphNodeApplyToolSource,
    $externalStarterKitTemplateSettingsRowApplyToolSource,
    $externalStarterKitTemplateLocaleEntryApplyToolSource,
    $externalStarterKitTemplateAssetEntryApplyToolSource
)
$externalStarterKitSnippetSourceRequiresStrictJsonRelativePaths =
    (@($externalStarterKitSnippetCreateToolSources | Where-Object {
        $_.Contains('function Test-StrictJsonRelativePath') -and
        $_.Contains(".EndsWith('.json', [System.StringComparison]::Ordinal)") -and
        $_.Contains(".Contains(':')") -and
        $_.Contains('must not contain empty, dot, or dot-dot path segments.')
    }).Count -eq $externalStarterKitSnippetCreateToolSources.Count) -and
    (@($externalStarterKitSnippetApplyToolSources | Where-Object {
        $_.Contains('function Resolve-StarterRelativePath') -and
        $_.Contains(".EndsWith('.json', [System.StringComparison]::Ordinal)") -and
        $_.Contains(".Contains(':')") -and
        $_.Contains('must not contain empty, dot, or dot-dot path segments.')
    }).Count -eq $externalStarterKitSnippetApplyToolSources.Count)
$externalStarterKitSnippetApplySourceUsesBoundedJsonReads =
    (@($externalStarterKitSnippetApplyToolSources | Where-Object {
        $_.Contains('$MaxSnippetJsonBytes = 65536') -and
        $_.Contains('function Read-JsonFile([string]$Path, [string]$Label, [long]$MaxBytes)') -and
        $_.Contains("strict_json_io.ps1") -and
        $_.Contains('Read-H8JsonFileCapped')
    }).Count -eq $externalStarterKitSnippetApplyToolSources.Count) -and
    $externalStarterKitTemplateStrictJsonIoToolSource.Contains('exceeds byte cap') -and
    $externalStarterKitTemplateGraphNodeApplyToolSource.Contains('$MaxGraphJsonBytes = 262144') -and
    $externalStarterKitTemplateSettingsRowApplyToolSource.Contains('$MaxSettingsTableJsonBytes = 262144') -and
    $externalStarterKitTemplateLocaleEntryApplyToolSource.Contains('$MaxLocaleJsonBytes = 2097152') -and
    $externalStarterKitTemplateAssetEntryApplyToolSource.Contains('$MaxAssetManifestJsonBytes = 262144') -and
    $externalStarterKitTemplateAssetEntryApplyToolSource.Contains('$MaxManifestJsonBytes = 65536')
$externalStarterKitAssetSnippetSourceRejectsNonPortableAssetPaths =
    $externalStarterKitTemplateAssetEntrySnippetToolSource.Contains("StartsWith('/')") -and
    $externalStarterKitTemplateAssetEntrySnippetToolSource.Contains(".Contains(':')") -and
    $externalStarterKitTemplateAssetEntrySnippetToolSource.Contains('Path must not contain empty, dot, or dot-dot path segments.') -and
    $externalStarterKitTemplateAssetEntryApplyToolSource.Contains('Resolve-StarterRelativePath $RelativePath ''Content/Assets/'' ''Asset Path'' $false') -and
    $externalStarterKitTemplateAssetEntryApplyToolSource.Contains(".Contains(':')")
$externalStarterKitWritesGraphNodeApplyTool =
    $moddingSdkHubSource.Contains('Tools", "apply_graph_node_snippet.ps1"') -and
    $moddingSdkHubSource.Contains('BuildStarterKitGraphNodeApplyScript') -and
    $externalStarterKitTemplateGraphNodeApplyToolSource.Contains('hecton8.graph_node_apply.v1') -and
    $externalStarterKitContractText.Contains('Tools/apply_graph_node_snippet.ps1')
$externalStarterKitGraphNodeSnippetProbe = Invoke-StarterGraphNodeSnippetProbe $externalStarterKitTemplatePath
$externalStarterKitGraphNodeSnippetToolPasses =
    $externalStarterKitGraphNodeSnippetProbe.TextExitCode -eq 0 -and
    $externalStarterKitGraphNodeSnippetProbe.SnippetPathExists -eq $true -and
    $externalStarterKitGraphNodeSnippetProbe.SnippetId -eq 'node.spawn_item' -and
    $externalStarterKitGraphNodeSnippetProbe.SnippetOpcode -eq 'SpawnItem' -and
    $externalStarterKitGraphNodeSnippetProbe.SnippetEnabled -eq $false -and
    $externalStarterKitGraphNodeSnippetProbe.SnippetHasParameters -eq $true -and
    $externalStarterKitGraphNodeSnippetProbe.SnippetParameterCount -eq 2 -and
    $externalStarterKitGraphNodeSnippetProbe.SnippetParameterQuantity -eq 3 -and
    $externalStarterKitGraphNodeSnippetProbe.InvalidOpcodeExitCode -ne 0 -and
    $externalStarterKitGraphNodeSnippetProbe.InvalidParametersExitCode -ne 0 -and
    $externalStarterKitGraphNodeSnippetProbe.RelaxedParametersExitCode -eq 0 -and
    $externalStarterKitGraphNodeSnippetProbe.RelaxedParametersNodeId -eq 'node.relaxed_parameters' -and
    $externalStarterKitGraphNodeSnippetProbe.RelaxedParametersQuantity -eq 7 -and
    $externalStarterKitGraphNodeSnippetProbe.RelaxedParametersMode -eq 'relaxed'
$externalStarterKitGraphNodeSnippetToolSupportsJson =
    $externalStarterKitGraphNodeSnippetProbe.JsonExitCode -eq 0 -and
    $externalStarterKitGraphNodeSnippetProbe.JsonSchema -eq 'hecton8.graph_node_snippet.v1' -and
    $externalStarterKitGraphNodeSnippetProbe.JsonRuntime -eq 'envelope-only' -and
    $externalStarterKitGraphNodeSnippetProbe.JsonOutput -eq 'Generated/graph_node_snippet.json' -and
    $externalStarterKitGraphNodeSnippetProbe.JsonNodeOpcode -eq '0x3A3DA9C4' -and
    $externalStarterKitGraphNodeSnippetProbe.JsonNodeParameterMode -eq 'hex'
$externalStarterKitGraphNodeApplyToolPasses =
    $externalStarterKitGraphNodeSnippetProbe.ApplyExitCode -eq 0 -and
    $externalStarterKitGraphNodeSnippetProbe.ApplySchema -eq 'hecton8.graph_node_apply.v1' -and
    $externalStarterKitGraphNodeSnippetProbe.ApplyRuntime -eq 'envelope-only' -and
    $externalStarterKitGraphNodeSnippetProbe.ApplyTarget -eq 'Graphs/main.h8graph.json' -and
    $externalStarterKitGraphNodeSnippetProbe.ApplyManifest -eq 'mod.h8manifest.json' -and
    $externalStarterKitGraphNodeSnippetProbe.ApplyNodeId -eq 'node.spawn_item_hex' -and
    $externalStarterKitGraphNodeSnippetProbe.ApplyOpcode -eq '0x3A3DA9C4' -and
    $externalStarterKitGraphNodeSnippetProbe.ApplyReplaced -eq $false -and
    $externalStarterKitGraphNodeSnippetProbe.GraphContainsAppliedNode -eq $true -and
    $externalStarterKitGraphNodeSnippetProbe.ApplyGraphBudget -eq 1 -and
    $externalStarterKitGraphNodeSnippetProbe.ApplyManifestBudget -eq 1 -and
    $externalStarterKitGraphNodeSnippetProbe.GraphBudgetAfterApply -eq 1 -and
    $externalStarterKitGraphNodeSnippetProbe.ManifestBudgetAfterApply -eq 1
$externalStarterKitGraphNodeApplyToolRejectsDuplicateWithoutReplace =
    $externalStarterKitGraphNodeSnippetProbe.DuplicateApplyExitCode -ne 0
$externalStarterKitWritesSettingsRowSnippetTool =
    $moddingSdkHubSource.Contains('Tools", "create_settings_row_snippet.ps1"') -and
    $moddingSdkHubSource.Contains('BuildStarterKitSettingsRowSnippetScript') -and
    $moddingSdkHubSource.Contains('BuildStarterKitToolFromTemplate("Tools/create_settings_row_snippet.ps1")') -and
    $externalStarterKitTemplateSettingsRowSnippetToolSource.Contains('H8MOD_SETTINGS_SNIPPET') -and
    $externalStarterKitContractText.Contains('Tools/create_settings_row_snippet.ps1')
$externalStarterKitWritesSettingsRowApplyTool =
    $moddingSdkHubSource.Contains('Tools", "apply_settings_row_snippet.ps1"') -and
    $moddingSdkHubSource.Contains('BuildStarterKitSettingsRowApplyScript') -and
    $externalStarterKitTemplateSettingsRowApplyToolSource.Contains('hecton8.settings_row_apply.v1') -and
    $externalStarterKitContractText.Contains('Tools/apply_settings_row_snippet.ps1')
$externalStarterKitSettingsRowSnippetProbe = Invoke-StarterSettingsRowSnippetProbe $externalStarterKitTemplatePath
$externalStarterKitSettingsRowSnippetToolPasses =
    $externalStarterKitSettingsRowSnippetProbe.TextExitCode -eq 0 -and
    $externalStarterKitSettingsRowSnippetProbe.SnippetPathExists -eq $true -and
    $externalStarterKitSettingsRowSnippetProbe.SnippetId -eq 'setting.validation_toggle' -and
    $externalStarterKitSettingsRowSnippetProbe.SnippetKind -eq 'bool' -and
    $externalStarterKitSettingsRowSnippetProbe.SnippetDefault -eq $true -and
    $externalStarterKitSettingsRowSnippetProbe.InvalidKindExitCode -ne 0 -and
    $externalStarterKitSettingsRowSnippetProbe.InvalidDefaultExitCode -ne 0
$externalStarterKitSettingsRowSnippetToolSupportsJson =
    $externalStarterKitSettingsRowSnippetProbe.JsonExitCode -eq 0 -and
    $externalStarterKitSettingsRowSnippetProbe.JsonSchema -eq 'hecton8.settings_row_snippet.v1' -and
    $externalStarterKitSettingsRowSnippetProbe.JsonRuntime -eq 'envelope-only' -and
    $externalStarterKitSettingsRowSnippetProbe.JsonOutput -eq 'Generated/settings_row_snippet.json' -and
    $externalStarterKitSettingsRowSnippetProbe.JsonRowKind -eq 'float' -and
    [math]::Abs(([double]$externalStarterKitSettingsRowSnippetProbe.JsonRowDefault) - 1.25) -le 0.000001
$externalStarterKitSettingsRowApplyToolPasses =
    $externalStarterKitSettingsRowSnippetProbe.ApplyExitCode -eq 0 -and
    $externalStarterKitSettingsRowSnippetProbe.ApplySchema -eq 'hecton8.settings_row_apply.v1' -and
    $externalStarterKitSettingsRowSnippetProbe.ApplyRuntime -eq 'envelope-only' -and
    $externalStarterKitSettingsRowSnippetProbe.ApplyTarget -eq 'Tables/settings.h8table.json' -and
    $externalStarterKitSettingsRowSnippetProbe.ApplySettingId -eq 'setting.validation_float' -and
    $externalStarterKitSettingsRowSnippetProbe.ApplyReplaced -eq $false -and
    $externalStarterKitSettingsRowSnippetProbe.AppliedRowExists -eq $true
$externalStarterKitSettingsRowApplyToolRejectsDuplicateWithoutReplace =
    $externalStarterKitSettingsRowSnippetProbe.DuplicateApplyExitCode -ne 0
$externalStarterKitWritesLocaleEntrySnippetTool =
    $moddingSdkHubSource.Contains('Tools", "create_locale_entry_snippet.ps1"') -and
    $moddingSdkHubSource.Contains('BuildStarterKitLocaleEntrySnippetScript') -and
    $moddingSdkHubSource.Contains('BuildStarterKitToolFromTemplate("Tools/create_locale_entry_snippet.ps1")') -and
    $externalStarterKitTemplateLocaleEntrySnippetToolSource.Contains('H8MOD_LOCALE_SNIPPET') -and
    $externalStarterKitContractText.Contains('Tools/create_locale_entry_snippet.ps1')
$externalStarterKitWritesLocaleEntryApplyTool =
    $moddingSdkHubSource.Contains('Tools", "apply_locale_entry_snippet.ps1"') -and
    $moddingSdkHubSource.Contains('BuildStarterKitLocaleEntryApplyScript') -and
    $externalStarterKitTemplateLocaleEntryApplyToolSource.Contains('hecton8.locale_entry_apply.v1') -and
    $externalStarterKitContractText.Contains('Tools/apply_locale_entry_snippet.ps1')
$externalStarterKitLocaleEntrySnippetProbe = Invoke-StarterLocaleEntrySnippetProbe $externalStarterKitTemplatePath
$externalStarterKitLocaleEntrySnippetToolPasses =
    $externalStarterKitLocaleEntrySnippetProbe.TextExitCode -eq 0 -and
    $externalStarterKitLocaleEntrySnippetProbe.SnippetPathExists -eq $true -and
    $externalStarterKitLocaleEntrySnippetProbe.SnippetKey -eq 'text.validation_line' -and
    $externalStarterKitLocaleEntrySnippetProbe.SnippetValue -eq 'Validation line' -and
    $externalStarterKitLocaleEntrySnippetProbe.InvalidKeyExitCode -ne 0 -and
    $externalStarterKitLocaleEntrySnippetProbe.InvalidValueExitCode -ne 0
$externalStarterKitLocaleEntrySnippetToolSupportsJson =
    $externalStarterKitLocaleEntrySnippetProbe.JsonExitCode -eq 0 -and
    $externalStarterKitLocaleEntrySnippetProbe.JsonSchema -eq 'hecton8.locale_entry_snippet.v1' -and
    $externalStarterKitLocaleEntrySnippetProbe.JsonRuntime -eq 'envelope-only' -and
    $externalStarterKitLocaleEntrySnippetProbe.JsonOutput -eq 'Generated/locale_entry_snippet.json' -and
    $externalStarterKitLocaleEntrySnippetProbe.JsonEntryKey -eq 'text.validation_json'
$externalStarterKitLocaleEntryApplyToolPasses =
    $externalStarterKitLocaleEntrySnippetProbe.ApplyExitCode -eq 0 -and
    $externalStarterKitLocaleEntrySnippetProbe.ApplySchema -eq 'hecton8.locale_entry_apply.v1' -and
    $externalStarterKitLocaleEntrySnippetProbe.ApplyRuntime -eq 'envelope-only' -and
    $externalStarterKitLocaleEntrySnippetProbe.ApplyTarget -eq 'Locales/en.h8loc.json' -and
    $externalStarterKitLocaleEntrySnippetProbe.ApplyKey -eq 'text.validation_json' -and
    $externalStarterKitLocaleEntrySnippetProbe.ApplyReplaced -eq $false -and
    $externalStarterKitLocaleEntrySnippetProbe.AppliedEntryExists -eq $true
$externalStarterKitLocaleEntryApplyToolRejectsDuplicateWithoutReplace =
    $externalStarterKitLocaleEntrySnippetProbe.DuplicateApplyExitCode -ne 0
$externalStarterKitWritesAssetEntrySnippetTool =
    $moddingSdkHubSource.Contains('Tools", "create_asset_entry_snippet.ps1"') -and
    $moddingSdkHubSource.Contains('BuildStarterKitAssetEntrySnippetScript') -and
    $externalStarterKitTemplateAssetEntrySnippetToolSource.Contains('hecton8.asset_entry_snippet.v1') -and
    $externalStarterKitContractText.Contains('Tools/create_asset_entry_snippet.ps1')
$externalStarterKitWritesAssetEntryApplyTool =
    $moddingSdkHubSource.Contains('Tools", "apply_asset_entry_snippet.ps1"') -and
    $moddingSdkHubSource.Contains('BuildStarterKitAssetEntryApplyScript') -and
    $externalStarterKitTemplateAssetEntryApplyToolSource.Contains('hecton8.asset_entry_apply.v1') -and
    $externalStarterKitContractText.Contains('Tools/apply_asset_entry_snippet.ps1')
$externalStarterKitAssetEntrySnippetProbe = Invoke-StarterAssetEntrySnippetProbe $externalStarterKitTemplatePath
$externalStarterKitAssetEntrySnippetToolPasses =
    $externalStarterKitAssetEntrySnippetProbe.TextExitCode -eq 0 -and
    $externalStarterKitAssetEntrySnippetProbe.SnippetPathExists -eq $true -and
    $externalStarterKitAssetEntrySnippetProbe.SnippetId -eq 'asset.validation_blob' -and
    $externalStarterKitAssetEntrySnippetProbe.SnippetKind -eq 'data_blob' -and
    $externalStarterKitAssetEntrySnippetProbe.SnippetPath -eq 'Content/Assets/probe.bytes' -and
    $externalStarterKitAssetEntrySnippetProbe.SnippetBytes -eq 5 -and
    $externalStarterKitAssetEntrySnippetProbe.SnippetCrc32 -match '^[0-9a-f]{8}$' -and
    $externalStarterKitAssetEntrySnippetProbe.InvalidKindExitCode -ne 0 -and
    $externalStarterKitAssetEntrySnippetProbe.InvalidPathExitCode -ne 0 -and
    $externalStarterKitAssetEntrySnippetProbe.InvalidCrcExitCode -ne 0
$externalStarterKitAssetEntrySnippetToolSupportsJson =
    $externalStarterKitAssetEntrySnippetProbe.JsonExitCode -eq 0 -and
    $externalStarterKitAssetEntrySnippetProbe.JsonSchema -eq 'hecton8.asset_entry_snippet.v1' -and
    $externalStarterKitAssetEntrySnippetProbe.JsonRuntime -eq 'envelope-only' -and
    $externalStarterKitAssetEntrySnippetProbe.JsonOutput -eq 'Generated/asset_entry_snippet.json' -and
    $externalStarterKitAssetEntrySnippetProbe.JsonAssetId -eq 'asset.validation_json_blob'
$externalStarterKitAssetEntryApplyToolPasses =
    $externalStarterKitAssetEntrySnippetProbe.ApplyExitCode -eq 0 -and
    $externalStarterKitAssetEntrySnippetProbe.ApplySchema -eq 'hecton8.asset_entry_apply.v1' -and
    $externalStarterKitAssetEntrySnippetProbe.ApplyRuntime -eq 'envelope-only' -and
    $externalStarterKitAssetEntrySnippetProbe.ApplyTarget -eq 'Content/assets.h8manifest.json' -and
    $externalStarterKitAssetEntrySnippetProbe.ApplyManifest -eq 'mod.h8manifest.json' -and
    $externalStarterKitAssetEntrySnippetProbe.ApplyAssetId -eq 'asset.validation_blob' -and
    $externalStarterKitAssetEntrySnippetProbe.ApplyKind -eq 'data_blob' -and
    $externalStarterKitAssetEntrySnippetProbe.ApplyPath -eq 'Content/Assets/probe.bytes' -and
    $externalStarterKitAssetEntrySnippetProbe.ApplyBytes -eq 5 -and
    $externalStarterKitAssetEntrySnippetProbe.ApplyCrc32 -eq $externalStarterKitAssetEntrySnippetProbe.SnippetCrc32 -and
    $externalStarterKitAssetEntrySnippetProbe.ApplyReplaced -eq $false -and
    $externalStarterKitAssetEntrySnippetProbe.AppliedEntryExists -eq $true -and
    $externalStarterKitAssetEntrySnippetProbe.ApplyManifestMaxAssetBytes -ge 5 -and
    $externalStarterKitAssetEntrySnippetProbe.ManifestBudgetAfterApply -ge 5 -and
    $externalStarterKitAssetEntrySnippetProbe.ValidatorExitCode -eq 0
$externalStarterKitAssetEntryApplyToolRejectsDuplicateWithoutReplace =
    $externalStarterKitAssetEntrySnippetProbe.DuplicateApplyExitCode -ne 0
$externalStarterKitSnippetPathsRequireStrictJsonRelativePaths =
    $externalStarterKitSnippetSourceRequiresStrictJsonRelativePaths -and
    $externalStarterKitGraphNodeSnippetProbe.InvalidOutputPathExitCode -ne 0 -and
    $externalStarterKitSettingsRowSnippetProbe.InvalidOutputPathExitCode -ne 0 -and
    $externalStarterKitLocaleEntrySnippetProbe.InvalidOutputPathExitCode -ne 0 -and
    $externalStarterKitAssetEntrySnippetProbe.InvalidOutputPathExitCode -ne 0 -and
    $externalStarterKitSettingsRowSnippetProbe.InvalidSnippetPathExitCode -ne 0
$externalStarterKitSnippetApplyUsesBoundedJsonReads =
    $externalStarterKitSnippetApplySourceUsesBoundedJsonReads -and
    $externalStarterKitSettingsRowSnippetProbe.OversizedApplyExitCode -ne 0
$externalStarterKitAssetSnippetRejectsNonPortableAssetPaths =
    $externalStarterKitAssetSnippetSourceRejectsNonPortableAssetPaths -and
    $externalStarterKitAssetEntrySnippetProbe.InvalidPortablePathExitCode -ne 0
$externalStarterKitGraphNodeSnippetCapsParametersJson =
    $externalStarterKitTemplateGraphNodeSnippetToolSource.Contains('ParametersJson must be 8192 characters or shorter.') -and
    $externalStarterKitGraphNodeSnippetProbe.OversizedParametersExitCode -ne 0
$externalStarterKitValidatorHardeningProbe = Invoke-StarterValidatorHardeningProbe $externalStarterKitTemplatePath
$externalStarterKitValidatorCapsRootJsonReads =
    $externalStarterKitTemplateValidatorSource.Contains('$MaxManifestJsonBytes = 65536') -and
    $externalStarterKitTemplateValidatorSource.Contains('$MaxGraphJsonBytes = 262144') -and
    $externalStarterKitTemplateValidatorSource.Contains('$MaxAssetManifestJsonBytes = 262144') -and
    $externalStarterKitTemplateValidatorSource.Contains('$MaxSettingsTableJsonBytes = 262144') -and
    $externalStarterKitTemplateValidatorSource.Contains('$MaxLocaleJsonBytes = 2097152') -and
    $externalStarterKitTemplateValidatorSource.Contains('$MaxCapabilityGuideBytes = 262144') -and
    $externalStarterKitTemplateValidatorSource.Contains('function Read-Json([string]$RelativePath, [long]$MaxBytes)') -and
    $externalStarterKitTemplateValidatorSource.Contains('function Read-TextFile([string]$RelativePath, [long]$MaxBytes)') -and
    $externalStarterKitValidatorHardeningProbe.OversizedManifestExitCode -ne 0
$externalStarterKitValidatorRejectsNonPortableAssetManifestPaths =
    $externalStarterKitTemplateValidatorSource.Contains("Path must not contain colon or stream syntax.") -and
    $externalStarterKitTemplateValidatorSource.Contains('Path must not contain empty, dot, or dot-dot path segments.') -and
    $externalStarterKitValidatorHardeningProbe.InvalidAssetPathExitCode -ne 0
$externalStarterKitCoreToolsUseBoundedJsonReads =
    $externalStarterKitValidatorCapsRootJsonReads -and
    $externalStarterKitTemplateIdentityToolSource.Contains('$MaxManifestJsonBytes = 65536') -and
    $externalStarterKitTemplateIdentityToolSource.Contains('function Read-JsonFile([string]$Path, [string]$Label, [long]$MaxBytes)') -and
    $externalStarterKitTemplateDependenciesToolSource.Contains('$MaxManifestJsonBytes = 65536') -and
    $externalStarterKitTemplateManifestContractToolSource.Contains('$MaxManifestJsonBytes = 65536') -and
    $externalStarterKitTemplateManifestContractToolSource.Contains('$MaxGraphJsonBytes = 262144') -and
    $externalStarterKitTemplateManifestContractToolSource.Contains('$MaxAssetManifestJsonBytes = 262144') -and
    $externalStarterKitTemplateReviewManifestBuilderSource.Contains('$MaxManifestJsonBytes = 65536') -and
    $externalStarterKitTemplatePrepareToolSource.Contains('$MaxReviewManifestBytes = 1048576') -and
    $externalStarterKitTemplateSubmissionPackageToolSource.Contains('$MaxReviewManifestBytes = 1048576') -and
    $externalStarterKitTemplateInstallLocalToolSource.Contains('$MaxReviewManifestBytes = 1048576') -and
    $externalStarterKitTemplateDiagnoseLocalToolSource.Contains('$MaxReviewManifestBytes = 1048576') -and
    $externalStarterKitTemplateDoctorToolSource.Contains('$MaxDoctorManifestBytes = 65536') -and
    $externalStarterKitTemplateDoctorToolSource.Contains('$MaxDoctorGraphBytes = 262144') -and
    $externalStarterKitTemplateDoctorToolSource.Contains('$MaxDoctorAssetManifestBytes = 262144') -and
    $externalStarterKitTemplateDoctorToolSource.Contains('$MaxDoctorSettingsTableBytes = 262144') -and
    $externalStarterKitTemplateDoctorToolSource.Contains('$MaxDoctorLocaleBytes = 2097152') -and
    $externalStarterKitContractText.Contains('cap external JSON/text reads before parsing') -and
    $runtimePlaybookText.Contains('ExternalStarterKitCoreToolsUseBoundedJsonReads = True') -and
    $specText.Contains('schema revision `124`')
$externalStarterKitStrictJsonIoRequiredToolSources = @(
    [pscustomobject]@{ Name = 'validate_structure.ps1'; Source = $externalStarterKitTemplateValidatorSource },
    [pscustomobject]@{ Name = 'build_review_manifest.ps1'; Source = $externalStarterKitTemplateReviewManifestBuilderSource },
    [pscustomobject]@{ Name = 'build_submission_package.ps1'; Source = $externalStarterKitTemplateSubmissionPackageToolSource },
    [pscustomobject]@{ Name = 'list_allowed_opcodes.ps1'; Source = $externalStarterKitTemplateAllowedOpcodeListToolSource },
    [pscustomobject]@{ Name = 'create_graph_node_snippet.ps1'; Source = $externalStarterKitTemplateGraphNodeSnippetToolSource },
    [pscustomobject]@{ Name = 'apply_graph_node_snippet.ps1'; Source = $externalStarterKitTemplateGraphNodeApplyToolSource },
    [pscustomobject]@{ Name = 'apply_settings_row_snippet.ps1'; Source = $externalStarterKitTemplateSettingsRowApplyToolSource },
    [pscustomobject]@{ Name = 'apply_locale_entry_snippet.ps1'; Source = $externalStarterKitTemplateLocaleEntryApplyToolSource },
    [pscustomobject]@{ Name = 'apply_asset_entry_snippet.ps1'; Source = $externalStarterKitTemplateAssetEntryApplyToolSource },
    [pscustomobject]@{ Name = 'configure_manifest_contract.ps1'; Source = $externalStarterKitTemplateManifestContractToolSource },
    [pscustomobject]@{ Name = 'configure_dependencies.ps1'; Source = $externalStarterKitTemplateDependenciesToolSource },
    [pscustomobject]@{ Name = 'install_local_mod.ps1'; Source = $externalStarterKitTemplateInstallLocalToolSource },
    [pscustomobject]@{ Name = 'diagnose_local_mods.ps1'; Source = $externalStarterKitTemplateDiagnoseLocalToolSource },
    [pscustomobject]@{ Name = 'run_doctor.ps1'; Source = $externalStarterKitTemplateDoctorToolSource },
    [pscustomobject]@{ Name = 'set_mod_identity.ps1'; Source = $externalStarterKitTemplateIdentityToolSource },
    [pscustomobject]@{ Name = 'prepare_mod.ps1'; Source = $externalStarterKitTemplatePrepareToolSource }
)
$externalStarterKitStrictJsonIoHelperHasStreamingStrictUtf8 =
    $externalStarterKitTemplateStrictJsonIoToolSource.Contains('$script:H8StrictUtf8NoBom = New-Object System.Text.UTF8Encoding -ArgumentList $false, $true') -and
    $externalStarterKitTemplateStrictJsonIoToolSource.Contains('$script:H8ReadChunkBytes = 8192') -and
    $externalStarterKitTemplateStrictJsonIoToolSource.Contains('function Read-H8TextFileCapped([string]$Path, [string]$Label, [long]$MaxBytes)') -and
    $externalStarterKitTemplateStrictJsonIoToolSource.Contains('function Read-H8JsonFileCapped([string]$Path, [string]$Label, [long]$MaxBytes)') -and
    $externalStarterKitTemplateStrictJsonIoToolSource.Contains('[System.IO.FileShare]::ReadWrite') -and
    $externalStarterKitTemplateStrictJsonIoToolSource.Contains('exceeds byte cap') -and
    $externalStarterKitTemplateStrictJsonIoToolSource.Contains('DecoderFallbackException') -and
    $externalStarterKitTemplateStrictJsonIoToolSource.Contains('is not strict UTF-8') -and
    $externalStarterKitTemplateStrictJsonIoToolSource.Contains('ConvertFrom-Json')
$externalStarterKitStrictJsonIoRequiredToolsDotSourceHelper =
    (($externalStarterKitStrictJsonIoRequiredToolSources | Where-Object { -not $_.Source.Contains("strict_json_io.ps1") }).Count -eq 0)
$externalStarterKitStrictJsonIoRequiredToolsUseCappedHelper =
    (($externalStarterKitStrictJsonIoRequiredToolSources | Where-Object { -not ($_.Source.Contains('Read-H8JsonFileCapped') -or $_.Source.Contains('Read-H8TextFileCapped')) }).Count -eq 0)
$externalStarterKitToolsHaveNoRawOrLineGetContent =
    (($externalStarterKitTemplateToolSources | Where-Object { $_.Source -match 'Get-Content' }).Count -eq 0)
$externalStarterKitCoreToolsUseStreamingStrictUtf8JsonReads =
    $externalStarterKitCoreToolsUseBoundedJsonReads -and
    $externalStarterKitStrictJsonIoHelperHasStreamingStrictUtf8 -and
    $externalStarterKitStrictJsonIoRequiredToolsDotSourceHelper -and
    $externalStarterKitStrictJsonIoRequiredToolsUseCappedHelper -and
    $externalStarterKitToolsHaveNoRawOrLineGetContent -and
    $externalStarterKitTemplateValidatorSource.Contains("'Tools/strict_json_io.ps1'") -and
    $moddingSdkHubSource.Contains('Tools", "strict_json_io.ps1"') -and
    $moddingSdkHubSource.Contains('BuildStarterKitStrictJsonIoScript') -and
    $externalStarterKitContractText.Contains('`Tools/strict_json_io.ps1` is the shared strict JSON/text ingestion helper') -and
    $runtimePlaybookText.Contains('ExternalStarterKitCoreToolsUseStreamingStrictUtf8JsonReads = True') -and
    $specText.Contains('schema revision `124`')
$externalStarterKitWritesManifestContractTool =
    $moddingSdkHubSource.Contains('Tools", "configure_manifest_contract.ps1"') -and
    $moddingSdkHubSource.Contains('BuildStarterKitManifestContractScript') -and
    $externalStarterKitTemplateManifestContractToolSource.Contains('hecton8.manifest_contract_config.v1') -and
    $externalStarterKitTemplateManifestContractToolSource.Contains('cap.graph.command_draft') -and
    $externalStarterKitTemplateManifestContractToolSource.Contains('$MaxEnvelopeBudgetCap = 256') -and
    $externalStarterKitTemplateManifestContractToolSource.Contains('$MaxAssetBudgetCap = 33554432') -and
    $externalStarterKitContractText.Contains('Tools/configure_manifest_contract.ps1')
$externalStarterKitManifestContractProbe = Invoke-StarterManifestContractProbe $externalStarterKitTemplatePath
$externalStarterKitManifestContractToolPasses =
    $externalStarterKitManifestContractProbe.JsonExitCode -eq 0 -and
    $externalStarterKitManifestContractProbe.JsonSchema -eq 'hecton8.manifest_contract_config.v1' -and
    $externalStarterKitManifestContractProbe.JsonRuntime -eq 'envelope-only' -and
    $externalStarterKitManifestContractProbe.JsonCapability -eq 'cap.graph.command_draft' -and
    $externalStarterKitManifestContractProbe.JsonCapabilityState -eq 'enable' -and
    $externalStarterKitManifestContractProbe.JsonMaxEnvelopesPerFrame -eq 1 -and
    $externalStarterKitManifestContractProbe.JsonMaxAssetBytes -eq 0 -and
    $externalStarterKitManifestContractProbe.ManifestHasGraphCapability -eq $true -and
    $externalStarterKitManifestContractProbe.ManifestMaxEnvelopesPerFrame -eq 1 -and
    $externalStarterKitManifestContractProbe.ManifestMaxAssetBytes -eq 0 -and
    $externalStarterKitManifestContractProbe.ValidatorExitCode -eq 0
$externalStarterKitManifestContractRejectsUnknownCapability =
    $externalStarterKitManifestContractProbe.BadCapabilityExitCode -ne 0
$externalStarterKitWritesFirstModTool =
    $moddingSdkHubSource.Contains('Tools", "create_first_mod.ps1"') -and
    $moddingSdkHubSource.Contains('BuildStarterKitFirstModScript') -and
    $moddingSdkHubSource.Contains('BuildStarterKitToolFromTemplate("Tools/create_first_mod.ps1")') -and
    $externalStarterKitTemplateFirstModToolSource.Contains('hecton8.first_mod.v1') -and
    $externalStarterKitTemplateFirstModToolSource.Contains("Resolve-Tool 'Tools/prepare_mod.ps1'") -and
    $externalStarterKitTemplateFirstModToolSource.Contains("Resolve-Tool 'Tools/configure_manifest_contract.ps1'") -and
    $externalStarterKitTemplateFirstModToolSource.Contains("Resolve-Tool 'Tools/apply_graph_node_snippet.ps1'") -and
    $externalStarterKitTemplateFirstModToolSource.Contains("Resolve-Tool 'Tools/apply_settings_row_snippet.ps1'") -and
    $externalStarterKitTemplateFirstModToolSource.Contains("Resolve-Tool 'Tools/apply_locale_entry_snippet.ps1'") -and
    $externalStarterKitTemplateFirstModToolSource.Contains('[switch]$BuildSubmission') -and
    $externalStarterKitContractText.Contains('Tools/create_first_mod.ps1')
$externalStarterKitWritesInstallLocalTool =
    $moddingSdkHubSource.Contains('Tools", "install_local_mod.ps1"') -and
    $moddingSdkHubSource.Contains('BuildStarterKitInstallLocalModScript') -and
    $moddingSdkHubSource.Contains('BuildStarterKitToolFromTemplate("Tools/install_local_mod.ps1")') -and
    $externalStarterKitTemplateInstallLocalToolSource.Contains('hecton8.local_install.v1') -and
    $externalStarterKitTemplateInstallLocalToolSource.Contains("Resolve-Tool 'Tools/prepare_mod.ps1'") -and
    $externalStarterKitTemplateInstallLocalToolSource.Contains('Get-FileHash -LiteralPath $Path -Algorithm SHA256') -and
    $externalStarterKitTemplateInstallLocalToolSource.Contains('Assert-UnderPath $modsRootFull $staging') -and
    $externalStarterKitTemplateInstallLocalToolSource.Contains('Remove-Item -LiteralPath $staging -Recurse -Force') -and
    $externalStarterKitContractText.Contains('Tools/install_local_mod.ps1')
$externalStarterKitWritesDiagnoseLocalTool =
    $moddingSdkHubSource.Contains('Tools", "diagnose_local_mods.ps1"') -and
    $moddingSdkHubSource.Contains('BuildStarterKitDiagnoseLocalModsScript') -and
    $moddingSdkHubSource.Contains('BuildStarterKitToolFromTemplate("Tools/diagnose_local_mods.ps1")') -and
    $externalStarterKitTemplateDiagnoseLocalToolSource.Contains('hecton8.local_mods_diagnosis.v1') -and
    $externalStarterKitTemplateDiagnoseLocalToolSource.Contains('$MaxManifestBytes = 32768') -and
    $externalStarterKitTemplateDiagnoseLocalToolSource.Contains('$MaxDiscoveredManifestCount = 64') -and
    $externalStarterKitTemplateDiagnoseLocalToolSource.Contains('$MaxTopLevelManagedAssemblyCount = 32') -and
    $externalStarterKitTemplateDiagnoseLocalToolSource.Contains('$MaxTopLevelBundleCount = 4') -and
    $externalStarterKitTemplateDiagnoseLocalToolSource.Contains('$MaxLocalizationFileCount = 16') -and
    $externalStarterKitTemplateDiagnoseLocalToolSource.Contains('Managed mod entry disabled. UGC commands must use 64-byte FutureCommandEnvelope packets.') -and
    $externalStarterKitTemplateDiagnoseLocalToolSource.Contains('[System.IO.SearchOption]::AllDirectories') -and
    $externalStarterKitTemplateDiagnoseLocalToolSource.Contains('RecursiveManifestDiscovery') -and
    $externalStarterKitTemplateDiagnoseLocalToolSource.Contains('DependencyGraph') -and
    $externalStarterKitTemplateDiagnoseLocalToolSource.Contains('Resolve-DependencyGraph') -and
    $externalStarterKitTemplateDiagnoseLocalToolSource.Contains('Dependency cycle or unresolved ordering deadlock.') -and
    $externalStarterKitContractText.Contains('Tools/diagnose_local_mods.ps1')
$externalStarterKitLocalInstallRequiresExactReviewProof =
    $externalStarterKitTemplateInstallLocalToolSource.Contains('Assert-StandardReviewOutput') -and
    $externalStarterKitTemplateInstallLocalToolSource.Contains('ReviewOutput path must be exactly Reports/review_manifest.json.') -and
    $externalStarterKitTemplateInstallLocalToolSource.Contains('Test-ReservedTopLevelCaseVariant') -and
    $externalStarterKitTemplateInstallLocalToolSource.Contains('Test-Sha256Hex') -and
    $externalStarterKitTemplateInstallLocalToolSource.Contains("-cmatch '^[0-9a-f]{64}$'") -and
    $externalStarterKitTemplateInstallLocalToolSource.Contains("-cne 'Reports/review_manifest.json'") -and
    $externalStarterKitTemplateInstallLocalToolSource.Contains('$hash -cne [string]$Entry.Sha256') -and
    $externalStarterKitTemplateInstallLocalToolSource.Contains('[System.Collections.Generic.Dictionary[string,bool]]::new([System.StringComparer]::Ordinal)') -and
    $externalStarterKitTemplateInstallLocalToolSource.Contains('[System.Collections.Generic.Dictionary[string,bool]]::new([System.StringComparer]::OrdinalIgnoreCase)') -and
    $externalStarterKitTemplateInstallLocalToolSource.Contains('Review manifest contains duplicate or case-fold duplicate source path') -and
    $externalStarterKitTemplateInstallLocalToolSource.Contains('Review manifest contains invalid lowercase SHA-256') -and
    (-not $externalStarterKitTemplateInstallLocalToolSource.Contains('([string]$Entry.Sha256).ToLowerInvariant()')) -and
    $externalStarterKitContractText.Contains('exact `Reports/review_manifest.json`') -and
    $externalStarterKitContractText.Contains('duplicate or case-fold duplicate review entries')
$externalStarterKitDiagnoseLocalRequiresExactReviewProof =
    $externalStarterKitTemplateDiagnoseLocalToolSource.Contains('Test-ReservedTopLevelCaseVariant') -and
    $externalStarterKitTemplateDiagnoseLocalToolSource.Contains('Test-Sha256Hex') -and
    $externalStarterKitTemplateDiagnoseLocalToolSource.Contains("-cmatch '^[0-9a-f]{64}$'") -and
    $externalStarterKitTemplateDiagnoseLocalToolSource.Contains('$actualSha -cne [string]$file.Sha256') -and
    $externalStarterKitTemplateDiagnoseLocalToolSource.Contains('[System.Collections.Generic.Dictionary[string,bool]]::new([System.StringComparer]::Ordinal)') -and
    $externalStarterKitTemplateDiagnoseLocalToolSource.Contains('[System.Collections.Generic.Dictionary[string,bool]]::new([System.StringComparer]::OrdinalIgnoreCase)') -and
    $externalStarterKitTemplateDiagnoseLocalToolSource.Contains('Review manifest contains duplicate or case-fold duplicate source path') -and
    $externalStarterKitTemplateDiagnoseLocalToolSource.Contains('Review manifest contains invalid lowercase SHA-256') -and
    $externalStarterKitTemplateDiagnoseLocalToolSource.Contains('Local reviewed install proof is missing or invalid.') -and
    (-not $externalStarterKitTemplateDiagnoseLocalToolSource.Contains('([string]$file.Sha256).ToLowerInvariant()')) -and
    $externalStarterKitContractText.Contains('exact lower-case SHA-256') -and
    $externalStarterKitContractText.Contains('duplicate or case-fold duplicate review entries')
$externalStarterKitWritesDependenciesTool =
    $moddingSdkHubSource.Contains('Tools", "configure_dependencies.ps1"') -and
    $moddingSdkHubSource.Contains('BuildStarterKitDependenciesScript') -and
    $moddingSdkHubSource.Contains('BuildStarterKitToolFromTemplate("Tools/configure_dependencies.ps1")') -and
    $externalStarterKitTemplateDependenciesToolSource.Contains('hecton8.dependencies.v1') -and
    $externalStarterKitTemplateDependenciesToolSource.Contains("ValidateSet('list','add','remove','clear')") -and
    $externalStarterKitTemplateDependenciesToolSource.Contains('mod.h8manifest.json Dependencies must match mod.json Dependencies') -and
    $externalStarterKitTemplateDependenciesToolSource.Contains('Mod must not depend on itself') -and
    $externalStarterKitTemplateDependenciesToolSource.Contains('Dependency already exists') -and
    $externalStarterKitTemplateDependenciesToolSource.Contains('Invoke-LocalValidation') -and
    $externalStarterKitTemplateDependenciesToolSource.Contains('Write-ManifestsWithValidation') -and
    $externalStarterKitContractText.Contains('Tools/configure_dependencies.ps1')
$externalStarterKitWritesDoctorTool =
    $moddingSdkHubSource.Contains('Tools", "run_doctor.ps1"') -and
    $moddingSdkHubSource.Contains('BuildStarterKitDoctorScript') -and
    $moddingSdkHubSource.Contains('BuildStarterKitToolFromTemplate("Tools/run_doctor.ps1")') -and
    $externalStarterKitTemplateDoctorToolSource.Contains('hecton8.starter_doctor.v1') -and
    $externalStarterKitTemplateDoctorToolSource.Contains('Tools/validate_structure.ps1') -and
    $externalStarterKitTemplateDoctorToolSource.Contains('Reports/review_manifest.json') -and
    $externalStarterKitTemplateDoctorToolSource.Contains('Generated/') -and
    $externalStarterKitTemplateDoctorToolSource.Contains('submission.zip') -and
    $externalStarterKitTemplateDoctorToolSource.Contains('Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256') -and
    $externalStarterKitTemplateDoctorToolSource.Contains('NextActions') -and
    $externalStarterKitContractText.Contains('Tools/run_doctor.ps1')
$externalStarterKitDoctorToolIsReadOnly =
    (-not ($externalStarterKitTemplateDoctorToolSource -match '(?m)^\s*(Set-Content|Out-File|New-Item|Remove-Item|Copy-Item|Move-Item|Rename-Item|Compress-Archive)\b')) -and
    $externalStarterKitTemplateDoctorToolSource.Contains('$MaxDoctorSourceFiles = 256') -and
    $externalStarterKitTemplateDoctorToolSource.Contains('$MaxDoctorZipEntries = 300') -and
    $externalStarterKitTemplateDoctorToolSource.Contains('$MaxDoctorZipEntryBytes = 4194304') -and
    $externalStarterKitTemplateDoctorToolSource.Contains('Status = $status')
$externalStarterKitDoctorVerifiesSubmissionZipContents =
    $externalStarterKitTemplateDoctorToolSource.Contains('System.IO.Compression.ZipFile]::OpenRead') -and
    $externalStarterKitTemplateDoctorToolSource.Contains('Test-SubmissionZipIntegrity') -and
    $externalStarterKitTemplateDoctorToolSource.Contains('Reports/review_manifest.json') -and
    $externalStarterKitTemplateDoctorToolSource.Contains('CheckedEntryCount') -and
    $externalStarterKitTemplateDoctorToolSource.Contains('ZipEntryCount') -and
    $externalStarterKitTemplateDoctorToolSource.Contains('Submission zip entries differ from review manifest')
$externalStarterKitDoctorRejectsUnsafeZipEntries =
    $externalStarterKitTemplateDoctorToolSource.Contains('Test-SafeZipEntryPath') -and
    $externalStarterKitTemplateDoctorToolSource.Contains('Submission zip contains unsafe entry paths') -and
    $externalStarterKitTemplateDoctorToolSource.Contains('Submission zip contains unreviewed entries') -and
    $externalStarterKitTemplateDoctorToolSource.Contains('Submission zip contains duplicate entry paths') -and
    $externalStarterKitTemplateDoctorToolSource.Contains('Generated/')
$externalStarterKitDoctorUsesCaseExactSubmissionZipIntegrity =
    $externalStarterKitTemplateDoctorToolSource.Contains('$MaxDoctorReviewManifestBytes = 1048576') -and
    $externalStarterKitTemplateDoctorToolSource.Contains('[System.Collections.Generic.Dictionary[string,object]]::new([System.StringComparer]::Ordinal)') -and
    $externalStarterKitTemplateDoctorToolSource.Contains('expectedCaseFoldPaths') -and
    $externalStarterKitTemplateDoctorToolSource.Contains('zipCaseFoldPaths') -and
    $externalStarterKitTemplateDoctorToolSource.Contains('Test-Sha256Hex') -and
    $externalStarterKitTemplateDoctorToolSource.Contains('DuplicateReviewPathCount') -and
    $externalStarterKitTemplateDoctorToolSource.Contains('InvalidReviewRecordCount') -and
    $externalStarterKitContractText.Contains('case-exact package doctor zip integrity')
$externalStarterKitDoctorRejectsReservedTopLevelCaseVariants =
    $externalStarterKitTemplateDoctorToolSource.Contains('$ReservedTopLevelFolders') -and
    $externalStarterKitTemplateDoctorToolSource.Contains('Test-ReservedTopLevelCaseVariant') -and
    $externalStarterKitTemplateDoctorToolSource.Contains('Test-ReviewOutputPath') -and
    $externalStarterKitTemplateDoctorToolSource.Contains("StartsWith('Generated/', [System.StringComparison]::OrdinalIgnoreCase)") -and
    $externalStarterKitTemplateDoctorToolSource.Contains("StartsWith('Reports/', [System.StringComparison]::OrdinalIgnoreCase)") -and
    $externalStarterKitContractText.Contains('reserved top-level folder case variants')
$externalStarterKitDoctorFailsNonReadyExit =
    $externalStarterKitTemplateDoctorToolSource.Contains("if (`$status -eq 'invalid')") -and
    $externalStarterKitTemplateDoctorToolSource.Contains('exit 1') -and
    $externalStarterKitTemplateDoctorToolSource.Contains("if (`$status -eq 'needs_review')") -and
    $externalStarterKitTemplateDoctorToolSource.Contains('exit 2') -and
    $externalStarterKitContractText.Contains('exits `2` for `needs_review`') -and
    $runtimePlaybookText.Contains('ExternalStarterKitDoctorFailsNonReadyExit = True') -and
    $specText.Contains('schema revision `130`')
$externalStarterKitRootLauncherPreservesToolExitCodes =
    $externalStarterKitTemplateLauncherSource.Contains('$toolSucceeded = $?') -and
    $externalStarterKitTemplateLauncherSource.Contains('$toolExitCode = $global:LASTEXITCODE') -and
    $externalStarterKitTemplateLauncherSource.Contains('if ($toolExitCode -ne 0)') -and
    $externalStarterKitTemplateLauncherSource.Contains('exit $toolExitCode') -and
    $externalStarterKitTemplateLauncherSource.Contains('if (-not $toolSucceeded)') -and
    $externalStarterKitContractText.Contains('preserves nonzero tool exit codes') -and
    $runtimePlaybookText.Contains('ExternalStarterKitRootLauncherPreservesToolExitCodes = True')
$externalStarterKitNestedToolsPreserveChildExitCodes =
    $externalStarterKitTemplateFirstModToolSource.Contains('function Complete-Tool([bool]$ToolSucceeded, [int]$ToolExitCode, [string]$Step)') -and
    $externalStarterKitTemplateFirstModToolSource.Contains('Complete-Tool $toolSucceeded $toolExitCode $Step') -and
    $externalStarterKitTemplateInstallLocalToolSource.Contains('function Complete-Tool([bool]$ToolSucceeded, [int]$ToolExitCode, [string]$Step)') -and
    $externalStarterKitTemplateInstallLocalToolSource.Contains('Complete-Tool $toolSucceeded $toolExitCode $Step') -and
    $externalStarterKitTemplateReviewManifestBuilderSource.Contains('Invoke-RequiredTool { & $validator -Root $rootFull }') -and
    $externalStarterKitTemplatePrepareToolSource.Contains('Invoke-RequiredTool { & $identityTool -Root $rootFull') -and
    $externalStarterKitTemplatePrepareToolSource.Contains('Invoke-RequiredTool { & $reviewTool -Root $rootFull -Output $ReviewOutput }') -and
    $externalStarterKitTemplateSubmissionPackageToolSource.Contains('Invoke-RequiredTool { & $prepareTool -Root $rootFull -ReviewOutput $ReviewOutput }') -and
    $externalStarterKitTemplateIdentityToolSource.Contains('Invoke-RequiredTool { & $validator -Root $rootFull }') -and
    $externalStarterKitTemplateReviewManifestBuilderSource.Contains('$toolExitCode = $global:LASTEXITCODE') -and
    $externalStarterKitTemplatePrepareToolSource.Contains('$toolExitCode = $global:LASTEXITCODE') -and
    $externalStarterKitTemplateSubmissionPackageToolSource.Contains('$toolExitCode = $global:LASTEXITCODE') -and
    $externalStarterKitTemplateIdentityToolSource.Contains('$toolExitCode = $global:LASTEXITCODE') -and
    $externalStarterKitTemplateReviewManifestBuilderSource.Contains('exit $toolExitCode') -and
    $externalStarterKitTemplatePrepareToolSource.Contains('exit $toolExitCode') -and
    $externalStarterKitTemplateSubmissionPackageToolSource.Contains('exit $toolExitCode') -and
    $externalStarterKitTemplateIdentityToolSource.Contains('exit $toolExitCode') -and
    $externalStarterKitContractText.Contains('nested starter tools preserve child exit codes') -and
    $runtimePlaybookText.Contains('ExternalStarterKitNestedToolsPreserveChildExitCodes = True') -and
    $specText.Contains('schema revision `131`')
$externalStarterKitDependencyToolMirrorsBothManifests =
    $externalStarterKitTemplateDependenciesToolSource.Contains("Set-JsonStringArray `$authoring 'Dependencies'") -and
    $externalStarterKitTemplateDependenciesToolSource.Contains("Set-JsonStringArray `$runtime 'Dependencies'") -and
    $externalStarterKitTemplateDependenciesToolSource.Contains('Write-ManifestsWithValidation $authoring $runtime')
$externalStarterKitDependenciesToolWritesStrictUtf8NoBom =
    $externalStarterKitTemplateDependenciesToolSource.Contains('function Write-TextFileUtf8NoBom') -and
    $externalStarterKitTemplateDependenciesToolSource.Contains('function Write-JsonFileUtf8NoBom') -and
    $externalStarterKitTemplateDependenciesToolSource.Contains('New-Object System.Text.UTF8Encoding $false') -and
    $externalStarterKitTemplateDependenciesToolSource.Contains('[System.IO.File]::WriteAllText') -and
    $externalStarterKitTemplateDependenciesToolSource.Contains("Read-H8JsonFileCapped `$Path 'Written dependency manifest' `$MaxManifestJsonBytes") -and
    (-not $externalStarterKitTemplateDependenciesToolSource.Contains('Set-Content -LiteralPath $authoringTemp -Encoding UTF8')) -and
    (-not $externalStarterKitTemplateDependenciesToolSource.Contains('Set-Content -LiteralPath $runtimeTemp -Encoding UTF8')) -and
    (-not $externalStarterKitTemplateDependenciesToolSource.Contains('Set-Content -LiteralPath $authoringPath -Encoding UTF8')) -and
    (-not $externalStarterKitTemplateDependenciesToolSource.Contains('Set-Content -LiteralPath $runtimePath -Encoding UTF8')) -and
    $externalStarterKitContractText.Contains('dependency helper writes UTF-8 without BOM') -and
    $runtimePlaybookText.Contains('ExternalStarterKitDependenciesToolWritesStrictUtf8NoBom = True') -and
    $specText.Contains('schema revision `132`')
$externalStarterKitDiagnoseLocalUsesRecursiveManifestDiscovery =
    $externalStarterKitTemplateDiagnoseLocalToolSource.Contains('[System.IO.SearchOption]::AllDirectories') -and
    $externalStarterKitTemplateDiagnoseLocalToolSource.Contains('Get-DiscoveredManifestFiles') -and
    $externalStarterKitTemplateDiagnoseLocalToolSource.Contains('RecursiveManifestDiscovery')
$externalStarterKitDiagnoseLocalChecksDependencyGraph =
    $externalStarterKitTemplateDiagnoseLocalToolSource.Contains('Resolve-DependencyGraph') -and
    $externalStarterKitTemplateDiagnoseLocalToolSource.Contains('DuplicateIdCount') -and
    $externalStarterKitTemplateDiagnoseLocalToolSource.Contains('MissingDependencyCount') -and
    $externalStarterKitTemplateDiagnoseLocalToolSource.Contains('CycleOrDeadlockCount') -and
    $externalStarterKitTemplateDiagnoseLocalToolSource.Contains('LoadOrderIndex') -and
    $externalStarterKitTemplateDiagnoseLocalToolSource.Contains('Dependency cycle or unresolved ordering deadlock.')
$externalStarterKitRootLauncherSupportsGraphNodeSnippet =
    $externalStarterKitTemplateLauncherSource.Contains("'node-snippet'") -and
    $externalStarterKitTemplateLauncherSource.Contains('Invoke-GraphNodeSnippet') -and
    $externalStarterKitTemplateLauncherSource.Contains("Resolve-StarterTool 'Tools/create_graph_node_snippet.ps1'")
$externalStarterKitRootLauncherSupportsGraphNodeParameters =
    $externalStarterKitTemplateLauncherSource.Contains('NodeParametersJson') -and
    $externalStarterKitTemplateLauncherSource.Contains('NodeDisabled') -and
    $externalStarterKitTemplateLauncherSource.Contains('-ParametersJson') -and
    $externalStarterKitTemplateLauncherSource.Contains('$snippetParametersJson') -and
    $externalStarterKitTemplateLauncherSource.Contains('-Disabled')
$externalStarterKitRootLauncherSupportsGraphNodeApply =
    $externalStarterKitTemplateLauncherSource.Contains("'apply-node-snippet'") -and
    $externalStarterKitTemplateLauncherSource.Contains('Invoke-ApplyGraphNodeSnippet') -and
    $externalStarterKitTemplateLauncherSource.Contains("Resolve-StarterTool 'Tools/apply_graph_node_snippet.ps1'")
$externalStarterKitRootLauncherSupportsAuthoringSnippets =
    $externalStarterKitTemplateLauncherSource.Contains("'setting-snippet'") -and
    $externalStarterKitTemplateLauncherSource.Contains("'locale-snippet'") -and
    $externalStarterKitTemplateLauncherSource.Contains('Invoke-SettingsRowSnippet') -and
    $externalStarterKitTemplateLauncherSource.Contains('Invoke-LocaleEntrySnippet') -and
    $externalStarterKitTemplateLauncherSource.Contains("Resolve-StarterTool 'Tools/create_settings_row_snippet.ps1'") -and
    $externalStarterKitTemplateLauncherSource.Contains("Resolve-StarterTool 'Tools/create_locale_entry_snippet.ps1'")
$externalStarterKitRootLauncherSupportsAuthoringSnippetApply =
    $externalStarterKitTemplateLauncherSource.Contains("'apply-setting-snippet'") -and
    $externalStarterKitTemplateLauncherSource.Contains("'apply-locale-snippet'") -and
    $externalStarterKitTemplateLauncherSource.Contains('Invoke-ApplySettingsRowSnippet') -and
    $externalStarterKitTemplateLauncherSource.Contains('Invoke-ApplyLocaleEntrySnippet') -and
    $externalStarterKitTemplateLauncherSource.Contains("Resolve-StarterTool 'Tools/apply_settings_row_snippet.ps1'") -and
    $externalStarterKitTemplateLauncherSource.Contains("Resolve-StarterTool 'Tools/apply_locale_entry_snippet.ps1'")
$externalStarterKitRootLauncherSupportsAssetEntrySnippet =
    $externalStarterKitTemplateLauncherSource.Contains("'asset-snippet'") -and
    $externalStarterKitTemplateLauncherSource.Contains('Invoke-AssetEntrySnippet') -and
    $externalStarterKitTemplateLauncherSource.Contains("Resolve-StarterTool 'Tools/create_asset_entry_snippet.ps1'")
$externalStarterKitRootLauncherSupportsAssetEntryApply =
    $externalStarterKitTemplateLauncherSource.Contains("'apply-asset-snippet'") -and
    $externalStarterKitTemplateLauncherSource.Contains('Invoke-ApplyAssetEntrySnippet') -and
    $externalStarterKitTemplateLauncherSource.Contains("Resolve-StarterTool 'Tools/apply_asset_entry_snippet.ps1'")
$externalStarterKitRootLauncherSupportsManifestContract =
    $externalStarterKitTemplateLauncherSource.Contains("'manifest-contract'") -and
    $externalStarterKitTemplateLauncherSource.Contains('Invoke-ManifestContractConfig') -and
    $externalStarterKitTemplateLauncherSource.Contains("Resolve-StarterTool 'Tools/configure_manifest_contract.ps1'") -and
    $externalStarterKitTemplateLauncherSource.Contains('CapabilityState') -and
    $externalStarterKitTemplateLauncherSource.Contains('MaxEnvelopesPerFrame') -and
    $externalStarterKitTemplateLauncherSource.Contains('MaxAssetBytes') -and
    $externalStarterKitManifestContractProbe.LauncherExitCode -eq 0 -and
    $externalStarterKitManifestContractProbe.LauncherHasSettingsCapability -eq $true
$externalStarterKitRootLauncherSupportsCapabilities =
    $externalStarterKitTemplateLauncherSource.Contains("'capabilities'") -and
    $externalStarterKitTemplateLauncherSource.Contains('Invoke-Capabilities') -and
    $externalStarterKitTemplateLauncherSource.Contains('Docs/capabilities.md')
$externalStarterKitRootLauncherUsesStrictCapabilityGuideRead =
    $externalStarterKitRootLauncherSupportsCapabilities -and
    $externalStarterKitTemplateLauncherSource.Contains('$MaxCapabilitiesGuideBytes = 262144') -and
    $externalStarterKitTemplateLauncherSource.Contains("Resolve-StarterTool 'Tools/strict_json_io.ps1'") -and
    $externalStarterKitTemplateLauncherSource.Contains('Read-H8TextFileCapped $guide ''Docs/capabilities.md'' $MaxCapabilitiesGuideBytes') -and
    (-not $externalStarterKitTemplateLauncherSource.Contains('Get-Content -LiteralPath $guide'))
$externalStarterKitRequiredTemplateScriptPaths = @(
    'h8mod.ps1',
    'Tools/strict_json_io.ps1',
    'Tools/apply_asset_entry_snippet.ps1',
    'Tools/build_review_manifest.ps1',
    'Tools/build_submission_package.ps1',
    'Tools/configure_dependencies.ps1',
    'Tools/configure_manifest_contract.ps1',
    'Tools/create_first_mod.ps1',
    'Tools/install_local_mod.ps1',
    'Tools/diagnose_local_mods.ps1',
    'Tools/run_doctor.ps1',
    'Tools/apply_graph_node_snippet.ps1',
    'Tools/apply_locale_entry_snippet.ps1',
    'Tools/apply_settings_row_snippet.ps1',
    'Tools/create_asset_entry_snippet.ps1',
    'Tools/create_locale_entry_snippet.ps1',
    'Tools/create_graph_node_snippet.ps1',
    'Tools/create_settings_row_snippet.ps1',
    'Tools/list_allowed_opcodes.ps1',
    'Tools/prepare_mod.ps1',
    'Tools/set_mod_identity.ps1',
    'Tools/validate_structure.ps1'
)
$externalStarterKitHubScriptsRequireCheckedInTemplates =
    ([regex]::Matches($moddingSdkHubSource, 'BuildStarterKitTemplateFile\("(?:(?:Tools/[^"]+\.ps1)|h8mod\.ps1)')).Count -eq 0
foreach ($templateScriptPath in $externalStarterKitRequiredTemplateScriptPaths) {
    $externalStarterKitHubScriptsRequireCheckedInTemplates =
        $externalStarterKitHubScriptsRequireCheckedInTemplates -and
        $moddingSdkHubSource.Contains('BuildStarterKitToolFromTemplate("' + $templateScriptPath + '")')
}
function Test-HubToolShim([string]$MethodName, [string]$TemplatePath) {
    $pattern =
        'private static string ' + [regex]::Escape($MethodName) +
        '\(\)\s*\{\s*return BuildStarterKitToolFromTemplate\("' +
        [regex]::Escape($TemplatePath) +
        '"\);\s*\}'
    return [regex]::IsMatch($moddingSdkHubSource, $pattern, [System.Text.RegularExpressions.RegexOptions]::Singleline)
}
$externalStarterKitHubExecutableFallbackBodiesRemoved =
    (Test-HubToolShim 'BuildStarterKitLauncherScript' 'h8mod.ps1') -and
    (Test-HubToolShim 'BuildStarterKitAllowedOpcodesScript' 'Tools/list_allowed_opcodes.ps1') -and
    (Test-HubToolShim 'BuildStarterKitSettingsRowSnippetScript' 'Tools/create_settings_row_snippet.ps1') -and
    (Test-HubToolShim 'BuildStarterKitLocaleEntrySnippetScript' 'Tools/create_locale_entry_snippet.ps1') -and
    (Test-HubToolShim 'BuildStarterKitPrepareScript' 'Tools/prepare_mod.ps1') -and
    (Test-HubToolShim 'BuildStarterKitIdentityScript' 'Tools/set_mod_identity.ps1') -and
    (Test-HubToolShim 'BuildStarterKitValidatorScript' 'Tools/validate_structure.ps1') -and
    (-not [regex]::IsMatch($moddingSdkHubSource, 'BuildStarterKit(?:LauncherScript|AllowedOpcodesScript|SettingsRowSnippetScript|LocaleEntrySnippetScript|PrepareScript|IdentityScript|ValidatorScript)\(\)\s*\{\s*StringBuilder', [System.Text.RegularExpressions.RegexOptions]::Singleline)) -and
    (-not $moddingSdkHubSource.Contains('H8MOD_OPCODE_LIST')) -and
    (-not $moddingSdkHubSource.Contains('H8MOD_SETTINGS_SNIPPET')) -and
    (-not $moddingSdkHubSource.Contains('H8MOD_LOCALE_SNIPPET')) -and
    (-not $moddingSdkHubSource.Contains('H8MOD_PREPARE')) -and
    (-not $moddingSdkHubSource.Contains('H8MOD_SET_IDENTITY')) -and
    (-not $moddingSdkHubSource.Contains('H8MOD_STARTER_VALIDATION')) -and
    (-not $moddingSdkHubSource.Contains('Get-Content -Raw -LiteralPath')) -and
    (-not $moddingSdkHubSource.Contains('foreach ($line in (Get-Content -LiteralPath $path))'))
$externalStarterKitSubmissionPackageProbe = Invoke-StarterSubmissionPackageProbe $externalStarterKitTemplatePath
$externalStarterKitSubmissionPackageToolPasses =
    $externalStarterKitSubmissionPackageProbe.ExitCode -eq 0 -and
    $externalStarterKitSubmissionPackageProbe.ZipExists -eq $true -and
    $externalStarterKitSubmissionPackageProbe.Output -contains 'PASS HECTON-8 submission package: Generated/com.example.starter_submission.zip' -and
    $externalStarterKitSubmissionPackageProbe.HasRuntimeManifest -eq $true -and
    $externalStarterKitSubmissionPackageProbe.HasAuthoringManifest -eq $true -and
    $externalStarterKitSubmissionPackageProbe.HasSubmissionTool -eq $true -and
    $externalStarterKitSubmissionPackageProbe.ZipIsNotOlderThanReview -eq $true -and
    $externalStarterKitSubmissionPackageProbe.HasGeneratedOutputEntry -eq $false -and
    $externalStarterKitSubmissionPackageProbe.HasReportsSourceEntry -eq $false
$externalStarterKitSubmissionPackageIncludesReviewManifest =
    $externalStarterKitSubmissionPackageProbe.HasReviewManifest -eq $true
$externalStarterKitRootLauncherSupportsSubmissionPackage =
    $externalStarterKitTemplateLauncherSource.Contains("'submission'") -and
    $externalStarterKitTemplateLauncherSource.Contains('Invoke-SubmissionPackage') -and
    $externalStarterKitTemplateLauncherSource.Contains("Resolve-StarterTool 'Tools/build_submission_package.ps1'")
$externalStarterKitRootLauncherSupportsFirstMod =
    $externalStarterKitTemplateLauncherSource.Contains("'first-mod'") -and
    $externalStarterKitTemplateLauncherSource.Contains('Invoke-FirstMod') -and
    $externalStarterKitTemplateLauncherSource.Contains("Resolve-StarterTool 'Tools/create_first_mod.ps1'") -and
    $externalStarterKitTemplateLauncherSource.Contains('-BuildSubmission') -and
    $externalStarterKitTemplateLauncherSource.Contains('-Replace -BuildSubmission')
$externalStarterKitRootLauncherSupportsInstallLocal =
    $externalStarterKitTemplateLauncherSource.Contains("'install-local'") -and
    $externalStarterKitTemplateLauncherSource.Contains('Invoke-InstallLocal') -and
    $externalStarterKitTemplateLauncherSource.Contains("Resolve-StarterTool 'Tools/install_local_mod.ps1'") -and
    $externalStarterKitTemplateLauncherSource.Contains('ProjectRoot') -and
    $externalStarterKitTemplateLauncherSource.Contains('ModsRoot') -and
    $externalStarterKitTemplateLauncherSource.Contains('-Replace')
$externalStarterKitRootLauncherSupportsDiagnoseLocal =
    $externalStarterKitTemplateLauncherSource.Contains("'diagnose-local'") -and
    $externalStarterKitTemplateLauncherSource.Contains('Invoke-DiagnoseLocal') -and
    $externalStarterKitTemplateLauncherSource.Contains("Resolve-StarterTool 'Tools/diagnose_local_mods.ps1'") -and
    $externalStarterKitTemplateLauncherSource.Contains('ProjectRoot') -and
    $externalStarterKitTemplateLauncherSource.Contains('ModsRoot') -and
    $externalStarterKitTemplateLauncherSource.Contains('-ModsRoot $diagnoseModsRoot -Json')
$externalStarterKitRootLauncherSupportsDoctor =
    $externalStarterKitTemplateLauncherSource.Contains("'doctor'") -and
    $externalStarterKitTemplateLauncherSource.Contains('Invoke-Doctor') -and
    $externalStarterKitTemplateLauncherSource.Contains("Resolve-StarterTool 'Tools/run_doctor.ps1'") -and
    $externalStarterKitTemplateLauncherSource.Contains('& $tool -Root $Root -Json')
$externalStarterKitRootLauncherSupportsDependencies =
    $externalStarterKitTemplateLauncherSource.Contains("'dependencies'") -and
    $externalStarterKitTemplateLauncherSource.Contains('Invoke-Dependencies') -and
    $externalStarterKitTemplateLauncherSource.Contains("Resolve-StarterTool 'Tools/configure_dependencies.ps1'") -and
    $externalStarterKitTemplateLauncherSource.Contains('DependencyAction') -and
    $externalStarterKitTemplateLauncherSource.Contains('DependencyId')
$externalStarterKitIdentityToolValidatesCanonicalId =
    $externalStarterKitTemplateIdentityToolSource.Contains('Validate-ModId $Id') -and
    $externalStarterKitTemplateIdentityToolSource.Contains('reserved filesystem device segment') -and
    $externalStarterKitTemplateIdentityToolSource.Contains('PASS HECTON-8 starter identity set')
$externalStarterKitInvalidVersionProbe = Invoke-StarterInvalidVersionProbe $externalStarterKitTemplatePath
$externalStarterKitIdentityToolRejectsInvalidVersion =
    $externalStarterKitInvalidVersionProbe.ExitCode -ne 0
$externalStarterKitIdentityRollbackProbe = Invoke-StarterIdentityRollbackProbe $externalStarterKitTemplatePath
$externalStarterKitIdentityToolRollsBackBothManifestsOnValidationFailure =
    $externalStarterKitTemplateIdentityToolSource.Contains('Restore-FileBackup $authoringBackupPath $authoringPath') -and
    $externalStarterKitTemplateIdentityToolSource.Contains('Restore-FileBackup $runtimeBackupPath $runtimePath') -and
    $externalStarterKitTemplateIdentityToolSource.Contains('Read-H8JsonFileCapped $Path ''Written identity manifest'' $MaxManifestJsonBytes') -and
    $externalStarterKitIdentityRollbackProbe.ExitCode -ne 0 -and
    $externalStarterKitIdentityRollbackProbe.AuthoringAfterId -eq $externalStarterKitIdentityRollbackProbe.AuthoringBeforeId -and
    $externalStarterKitIdentityRollbackProbe.RuntimeAfterId -eq $externalStarterKitIdentityRollbackProbe.RuntimeBeforeId -and
    $externalStarterKitIdentityRollbackProbe.AuthoringAfterDisplayName -eq 'Starter Mod' -and
    $externalStarterKitIdentityRollbackProbe.RuntimeAfterName -eq 'Starter Mod'
$externalStarterKitValidatorChecksSemver =
    $externalStarterKitTemplateValidatorSource.Contains('function Validate-Version') -and
    $externalStarterKitTemplateIdentityToolSource.Contains('function Validate-Version') -and
    $externalStarterKitTemplateValidatorSource.Contains('semantic version form MAJOR.MINOR.PATCH') -and
    $externalStarterKitTemplateIdentityToolSource.Contains('semantic version form MAJOR.MINOR.PATCH') -and
    $externalStarterKitIdentityToolRejectsInvalidVersion
$externalStarterKitValidatorChecksManifestIdentityTextParity =
    $externalStarterKitTemplateValidatorSource.Contains('DisplayName must match mod.json Name') -and
    $externalStarterKitTemplateValidatorSource.Contains('Author must match mod.json Author') -and
    $externalStarterKitTemplateValidatorSource.Contains('Version must match mod.json Version')
$externalStarterKitToolsAvoidNestedPowerShell =
    (-not $externalStarterKitTemplateLauncherSource.Contains('& powershell')) -and
    (-not $externalStarterKitTemplateIdentityToolSource.Contains('& powershell')) -and
    (-not $externalStarterKitTemplateReviewManifestBuilderSource.Contains('& powershell')) -and
    (-not $externalStarterKitTemplateSubmissionPackageToolSource.Contains('& powershell')) -and
    (-not $externalStarterKitTemplateAllowedOpcodeListToolSource.Contains('& powershell')) -and
    (-not $externalStarterKitTemplateGraphNodeSnippetToolSource.Contains('& powershell')) -and
    (-not $externalStarterKitTemplateGraphNodeApplyToolSource.Contains('& powershell')) -and
    (-not $externalStarterKitTemplateSettingsRowSnippetToolSource.Contains('& powershell')) -and
    (-not $externalStarterKitTemplateLocaleEntrySnippetToolSource.Contains('& powershell')) -and
    (-not $externalStarterKitTemplateSettingsRowApplyToolSource.Contains('& powershell')) -and
    (-not $externalStarterKitTemplateLocaleEntryApplyToolSource.Contains('& powershell')) -and
    (-not $externalStarterKitTemplateAssetEntrySnippetToolSource.Contains('& powershell')) -and
    (-not $externalStarterKitTemplateAssetEntryApplyToolSource.Contains('& powershell')) -and
    (-not $externalStarterKitTemplateManifestContractToolSource.Contains('& powershell')) -and
    (-not $externalStarterKitTemplateFirstModToolSource.Contains('& powershell')) -and
    (-not $externalStarterKitTemplatePrepareToolSource.Contains('& powershell')) -and
    $externalStarterKitTemplatePrepareToolSource.Contains('Invoke-RequiredTool { & $identityTool -Root $rootFull -Id $Id') -and
    $externalStarterKitTemplatePrepareToolSource.Contains('Invoke-RequiredTool { & $reviewTool -Root $rootFull -Output $ReviewOutput }') -and
    $externalStarterKitTemplateReviewManifestBuilderSource.Contains('Invoke-RequiredTool { & $validator -Root $rootFull }')
$externalStarterKitToolsUsePortableJoinPath =
    $externalStarterKitTemplateLauncherSource.Contains('function Join-StarterPath') -and
    $externalStarterKitTemplateLauncherSource.Contains('$tool = Join-StarterPath $Root $RelativePath') -and
    $externalStarterKitTemplateValidatorSource.Contains('function Join-StarterPath') -and
    $externalStarterKitTemplateIdentityToolSource.Contains('function Join-StarterPath') -and
    $externalStarterKitTemplateReviewManifestBuilderSource.Contains('function Join-StarterPath') -and
    $externalStarterKitTemplateSubmissionPackageToolSource.Contains('function Join-StarterPath') -and
    $externalStarterKitTemplateAllowedOpcodeListToolSource.Contains('function Join-StarterPath') -and
    $externalStarterKitTemplateGraphNodeSnippetToolSource.Contains('function Join-StarterPath') -and
    $externalStarterKitTemplateGraphNodeApplyToolSource.Contains('function Join-StarterPath') -and
    $externalStarterKitTemplateSettingsRowSnippetToolSource.Contains('function Join-StarterPath') -and
    $externalStarterKitTemplateLocaleEntrySnippetToolSource.Contains('function Join-StarterPath') -and
    $externalStarterKitTemplateSettingsRowApplyToolSource.Contains('function Join-StarterPath') -and
    $externalStarterKitTemplateLocaleEntryApplyToolSource.Contains('function Join-StarterPath') -and
    $externalStarterKitTemplateAssetEntrySnippetToolSource.Contains('function Join-StarterPath') -and
    $externalStarterKitTemplateAssetEntryApplyToolSource.Contains('function Join-StarterPath') -and
    $externalStarterKitTemplateManifestContractToolSource.Contains('function Join-StarterPath') -and
    $externalStarterKitTemplateFirstModToolSource.Contains('function Join-StarterPath') -and
    $externalStarterKitTemplatePrepareToolSource.Contains('function Join-StarterPath') -and
    $externalStarterKitTemplateReviewManifestBuilderSource.Contains('$outputPath = Join-StarterPath $rootFull $normalizedOutput') -and
    $externalStarterKitTemplateSubmissionPackageToolSource.Contains('$outputPath = Join-StarterPath $rootFull $Output') -and
    $externalStarterKitTemplatePrepareToolSource.Contains('Join-StarterPath $rootFull ''Tools/set_mod_identity.ps1''') -and
    $externalStarterKitTemplateIdentityToolSource.Contains('Join-StarterPath $rootFull ''Tools/validate_structure.ps1''') -and
    (-not $externalStarterKitTemplateIdentityToolSource.Contains("'Tools\")) -and
    (-not $externalStarterKitTemplateReviewManifestBuilderSource.Contains("'Tools\")) -and
    (-not $externalStarterKitTemplateSubmissionPackageToolSource.Contains("'Tools\")) -and
    (-not $externalStarterKitTemplateAllowedOpcodeListToolSource.Contains("'Tools\")) -and
    (-not $externalStarterKitTemplateGraphNodeSnippetToolSource.Contains("'Tools\")) -and
    (-not $externalStarterKitTemplateGraphNodeApplyToolSource.Contains("'Tools\")) -and
    (-not $externalStarterKitTemplateSettingsRowSnippetToolSource.Contains("'Tools\")) -and
    (-not $externalStarterKitTemplateLocaleEntrySnippetToolSource.Contains("'Tools\")) -and
    (-not $externalStarterKitTemplateSettingsRowApplyToolSource.Contains("'Tools\")) -and
    (-not $externalStarterKitTemplateLocaleEntryApplyToolSource.Contains("'Tools\")) -and
    (-not $externalStarterKitTemplateAssetEntrySnippetToolSource.Contains("'Tools\")) -and
    (-not $externalStarterKitTemplateAssetEntryApplyToolSource.Contains("'Tools\")) -and
    (-not $externalStarterKitTemplateManifestContractToolSource.Contains("'Tools\")) -and
    (-not $externalStarterKitTemplateFirstModToolSource.Contains("'Tools\")) -and
    (-not $externalStarterKitTemplatePrepareToolSource.Contains("'Tools\")) -and
    (-not $externalStarterKitTemplateLauncherSource.Contains("'Tools\")) -and
    (-not $moddingSdkHubSource.Contains("'Tools\\"))
$externalStarterKitWritesJsonSchemas =
    $moddingSdkHubSource.Contains('BuildAuthoringManifestSchema') -and
    $moddingSdkHubSource.Contains('BuildRuntimeManifestSchema') -and
    $moddingSdkHubSource.Contains('BuildGraphSchema') -and
    $moddingSdkHubSource.Contains('BuildAssetsSchema') -and
    $moddingSdkHubSource.Contains('BuildSettingsTableSchema') -and
    $moddingSdkHubSource.Contains('BuildLocaleSchema') -and
    $moddingSdkHubSource.Contains('BuildVsCodeSettings') -and
    $externalStarterKitContractText.Contains('Schemas/')
$starterKitGeneratorTemplateMarkers = @(
    'BuildStarterKitTemplateFile("README.md"',
    'BuildStarterKitTemplateFile("Docs/capabilities.md"',
    'BuildStarterKitTemplateFile("mod.h8manifest.json"',
    'BuildStarterKitTemplateFile("mod.json"',
    'BuildStarterKitTemplateFile("Content/README.md"',
    'BuildStarterKitTemplateFile("Content/Assets/README.md"',
    'BuildStarterKitTemplateFile("Content/assets.h8manifest.json"',
    'BuildStarterKitTemplateFile("Graphs/main.h8graph.json"',
    'BuildStarterKitTemplateFile("Tables/settings.h8table.json"',
    'BuildStarterKitTemplateFile("Locales/en.h8loc.json"',
    'BuildStarterKitTemplateFile("Generated/README.md"',
    'BuildStarterKitTemplateFile("Reports/README.md"',
    'BuildStarterKitTemplateFile("Reference/README.md"',
    'BuildStarterKitTemplateFile("Schemas/assets.schema.json"',
    'BuildStarterKitTemplateFile("Schemas/h8graph.schema.json"',
    'BuildStarterKitTemplateFile("Schemas/h8mod.authoring.schema.json"',
    'BuildStarterKitTemplateFile("Schemas/locale.schema.json"',
    'BuildStarterKitTemplateFile("Schemas/runtime.mod.schema.json"',
    'BuildStarterKitTemplateFile("Schemas/settings_table.schema.json"',
    'BuildStarterKitTemplateFile("Tools/README.md"',
    'BuildStarterKitTemplateFile(".vscode/settings.json"',
    'BuildStarterKitTemplateFile(".vscode/tasks.json"'
)
$externalStarterKitGeneratorUsesCheckedInTemplates =
    $moddingSdkHubSource.Contains('private static string BuildStarterKitTemplateFile') -and
    $moddingSdkHubSource.Contains('return fallbackFactory();') -and
    (@($starterKitGeneratorTemplateMarkers | Where-Object { -not $moddingSdkHubSource.Contains($_) }).Count -eq 0)
$externalStarterKitValidatorChecksJsonSchemas =
    $externalStarterKitTemplateValidatorSource.Contains('$schemaFiles = @(') -and
    $externalStarterKitTemplateValidatorSource.Contains('requires $schema') -and
    $externalStarterKitTemplateValidatorSource.Contains('.vscode/settings.json requires json.schemas mapping')
$externalStarterKitValidatorChecksEditorSchemaMappings =
    $externalStarterKitTemplateValidatorSource.Contains('$requiredSchemaMappings = @(') -and
    $externalStarterKitTemplateValidatorSource.Contains("Url = './Schemas/h8mod.authoring.schema.json'; Match = '/mod.h8manifest.json'") -and
    $externalStarterKitTemplateValidatorSource.Contains("Url = './Schemas/runtime.mod.schema.json'; Match = '/mod.json'") -and
    $externalStarterKitTemplateValidatorSource.Contains("Url = './Schemas/h8graph.schema.json'; Match = '/Graphs/*.h8graph.json'") -and
    $externalStarterKitTemplateValidatorSource.Contains("Url = './Schemas/assets.schema.json'; Match = '/Content/*.h8manifest.json'") -and
    $externalStarterKitTemplateValidatorSource.Contains("Url = './Schemas/settings_table.schema.json'; Match = '/Tables/*.h8table.json'") -and
    $externalStarterKitTemplateValidatorSource.Contains("Url = './Schemas/locale.schema.json'; Match = '/Locales/*.h8loc.json'") -and
    $externalStarterKitTemplateValidatorSource.Contains('.vscode/settings.json missing schema mapping')
$requiredVsCodeTaskLabels = @(
    'HECTON-8: setup identity',
    'HECTON-8: create first playable mod',
    'HECTON-8: validate starter',
    'HECTON-8: prepare review manifest',
    'HECTON-8: build submission zip',
    'HECTON-8: install local discovery copy',
    'HECTON-8: diagnose local Mods folder',
    'HECTON-8: doctor package readiness',
    'HECTON-8: list dependencies',
    'HECTON-8: add dependency',
    'HECTON-8: remove dependency',
    'HECTON-8: clear dependencies',
    'HECTON-8: show capabilities',
    'HECTON-8: show opcodes',
    'HECTON-8: create graph node snippet',
    'HECTON-8: create disabled graph node snippet',
    'HECTON-8: apply graph node snippet',
    'HECTON-8: replace graph node snippet',
    'HECTON-8: create settings row snippet',
    'HECTON-8: apply settings row snippet',
    'HECTON-8: replace settings row snippet',
    'HECTON-8: create locale entry snippet',
    'HECTON-8: apply locale entry snippet',
    'HECTON-8: replace locale entry snippet',
    'HECTON-8: create asset entry snippet',
    'HECTON-8: apply asset entry snippet',
    'HECTON-8: replace asset entry snippet',
    'HECTON-8: configure manifest contract'
)
$requiredVsCodeTaskInputIds = @('modId','displayName','author','version','projectRoot','dependencyId','nodeId','opcode','nodeParametersJson','settingId','settingKind','settingDefault','localeKey','localeValue','assetId','assetKind','assetPath','capability','capabilityState','maxEnvelopesPerFrame','maxAssetBytes')
$externalStarterKitVsCodeTaskLabels = @($externalStarterKitTemplateVsCodeTasks.tasks | ForEach-Object { [string]$_.label })
$externalStarterKitVsCodeTaskInputIds = @($externalStarterKitTemplateVsCodeTasks.inputs | ForEach-Object { [string]$_.id })
$externalStarterKitVsCodeTasksRouteThroughLauncher = $true
foreach ($task in @($externalStarterKitTemplateVsCodeTasks.tasks)) {
    $taskArgs = @($task.args | ForEach-Object { [string]$_ })
    if ([string]$task.type -ne 'shell') { $externalStarterKitVsCodeTasksRouteThroughLauncher = $false }
    if ([string]$task.command -ne '${config:hecton8.powerShellExecutable}') { $externalStarterKitVsCodeTasksRouteThroughLauncher = $false }
    if ($taskArgs -notcontains 'h8mod.ps1') { $externalStarterKitVsCodeTasksRouteThroughLauncher = $false }
    if ($taskArgs -notcontains '-Action') { $externalStarterKitVsCodeTasksRouteThroughLauncher = $false }
    foreach ($arg in $taskArgs) {
        if (([string]$arg).Replace('\','/') -match '^Tools/.*[.]ps1$') { $externalStarterKitVsCodeTasksRouteThroughLauncher = $false }
    }
}
$externalStarterKitVsCodeTasksPresent =
    (Test-Path -LiteralPath $externalStarterKitTemplateVsCodeTasksPath -PathType Leaf) -and
    [string]$externalStarterKitTemplateVsCodeTasks.version -eq '2.0.0' -and
    (@($requiredVsCodeTaskLabels | Where-Object { $externalStarterKitVsCodeTaskLabels -notcontains $_ }).Count -eq 0) -and
    (@($requiredVsCodeTaskInputIds | Where-Object { $externalStarterKitVsCodeTaskInputIds -notcontains $_ }).Count -eq 0) -and
    $externalStarterKitVsCodeTasksRouteThroughLauncher
$externalStarterKitVsCodeTasksSupportDisabledAndReplace =
    (@($externalStarterKitTemplateVsCodeTasks.tasks | Where-Object {
        [string]$_.label -eq 'HECTON-8: create first playable mod' -and
        @($_.args | ForEach-Object { [string]$_ }) -contains 'first-mod' -and
        @($_.args | ForEach-Object { [string]$_ }) -contains '-Replace'
    }).Count -eq 1) -and
    (@($externalStarterKitTemplateVsCodeTasks.tasks | Where-Object {
        [string]$_.label -eq 'HECTON-8: create disabled graph node snippet' -and
        @($_.args | ForEach-Object { [string]$_ }) -contains '-NodeDisabled'
    }).Count -eq 1) -and
    (@($externalStarterKitTemplateVsCodeTasks.tasks | Where-Object {
        [string]$_.label -eq 'HECTON-8: replace graph node snippet' -and
        @($_.args | ForEach-Object { [string]$_ }) -contains '-Replace'
    }).Count -eq 1) -and
    (@($externalStarterKitTemplateVsCodeTasks.tasks | Where-Object {
        [string]$_.label -eq 'HECTON-8: replace settings row snippet' -and
        @($_.args | ForEach-Object { [string]$_ }) -contains '-Replace'
    }).Count -eq 1) -and
    (@($externalStarterKitTemplateVsCodeTasks.tasks | Where-Object {
        [string]$_.label -eq 'HECTON-8: replace locale entry snippet' -and
        @($_.args | ForEach-Object { [string]$_ }) -contains '-Replace'
    }).Count -eq 1) -and
    (@($externalStarterKitTemplateVsCodeTasks.tasks | Where-Object {
        [string]$_.label -eq 'HECTON-8: replace asset entry snippet' -and
        @($_.args | ForEach-Object { [string]$_ }) -contains '-Replace'
    }).Count -eq 1)
$externalStarterKitVsCodeTasksSupportLocalInstall =
    (@($externalStarterKitTemplateVsCodeTasks.tasks | Where-Object {
        [string]$_.label -eq 'HECTON-8: install local discovery copy' -and
        @($_.args | ForEach-Object { [string]$_ }) -contains 'install-local' -and
        @($_.args | ForEach-Object { [string]$_ }) -contains '-ProjectRoot' -and
        @($_.args | ForEach-Object { [string]$_ }) -contains '-Replace'
    }).Count -eq 1) -and
    ($externalStarterKitVsCodeTaskInputIds -contains 'projectRoot')
$externalStarterKitVsCodeTasksSupportLocalDiagnose =
    (@($externalStarterKitTemplateVsCodeTasks.tasks | Where-Object {
        [string]$_.label -eq 'HECTON-8: diagnose local Mods folder' -and
        @($_.args | ForEach-Object { [string]$_ }) -contains 'diagnose-local' -and
        @($_.args | ForEach-Object { [string]$_ }) -contains '-ProjectRoot'
    }).Count -eq 1) -and
    ($externalStarterKitVsCodeTaskInputIds -contains 'projectRoot')
$externalStarterKitVsCodeTasksSupportDoctor =
    (@($externalStarterKitTemplateVsCodeTasks.tasks | Where-Object {
        [string]$_.label -eq 'HECTON-8: doctor package readiness' -and
        @($_.args | ForEach-Object { [string]$_ }) -contains 'doctor'
    }).Count -eq 1)
$externalStarterKitVsCodeTasksSupportDependencies =
    (@($externalStarterKitTemplateVsCodeTasks.tasks | Where-Object {
        [string]$_.label -eq 'HECTON-8: list dependencies' -and
        @($_.args | ForEach-Object { [string]$_ }) -contains 'dependencies' -and
        @($_.args | ForEach-Object { [string]$_ }) -contains 'list'
    }).Count -eq 1) -and
    (@($externalStarterKitTemplateVsCodeTasks.tasks | Where-Object {
        [string]$_.label -eq 'HECTON-8: add dependency' -and
        @($_.args | ForEach-Object { [string]$_ }) -contains 'dependencies' -and
        @($_.args | ForEach-Object { [string]$_ }) -contains 'add' -and
        @($_.args | ForEach-Object { [string]$_ }) -contains '-DependencyId'
    }).Count -eq 1) -and
    (@($externalStarterKitTemplateVsCodeTasks.tasks | Where-Object {
        [string]$_.label -eq 'HECTON-8: remove dependency' -and
        @($_.args | ForEach-Object { [string]$_ }) -contains 'dependencies' -and
        @($_.args | ForEach-Object { [string]$_ }) -contains 'remove' -and
        @($_.args | ForEach-Object { [string]$_ }) -contains '-DependencyId'
    }).Count -eq 1) -and
    (@($externalStarterKitTemplateVsCodeTasks.tasks | Where-Object {
        [string]$_.label -eq 'HECTON-8: clear dependencies' -and
        @($_.args | ForEach-Object { [string]$_ }) -contains 'dependencies' -and
        @($_.args | ForEach-Object { [string]$_ }) -contains 'clear'
    }).Count -eq 1) -and
    ($externalStarterKitVsCodeTaskInputIds -contains 'dependencyId')
$externalStarterKitWritesVsCodeTasks =
    $moddingSdkHubSource.Contains('BuildVsCodeTasks') -and
    $moddingSdkHubSource.Contains('Path.Combine(rootPath, ".vscode", "tasks.json")') -and
    $moddingSdkHubSource.Contains('BuildStarterKitTemplateFile(".vscode/tasks.json"') -and
    $externalStarterKitContractText.Contains('.vscode/tasks.json')
$externalStarterKitValidatorChecksVsCodeTasks =
    $externalStarterKitTemplateValidatorSource.Contains('function Validate-VsCodeTasks') -and
    $externalStarterKitTemplateValidatorSource.Contains('.vscode/tasks.json requires version 2.0.0') -and
    $externalStarterKitTemplateValidatorSource.Contains('HECTON-8: setup identity') -and
    $externalStarterKitTemplateValidatorSource.Contains('HECTON-8: create first playable mod') -and
    $externalStarterKitTemplateValidatorSource.Contains('first playable mod task must pass -Action first-mod') -and
    $externalStarterKitTemplateValidatorSource.Contains('HECTON-8: install local discovery copy') -and
    $externalStarterKitTemplateValidatorSource.Contains('local install task must pass -Action install-local') -and
    $externalStarterKitTemplateValidatorSource.Contains('HECTON-8: diagnose local Mods folder') -and
    $externalStarterKitTemplateValidatorSource.Contains('local diagnose task must pass -Action diagnose-local') -and
    $externalStarterKitTemplateValidatorSource.Contains('HECTON-8: doctor package readiness') -and
    $externalStarterKitTemplateValidatorSource.Contains('doctor task must pass -Action doctor') -and
    $externalStarterKitTemplateValidatorSource.Contains('HECTON-8: add dependency') -and
    $externalStarterKitTemplateValidatorSource.Contains('dependency task must pass -Action dependencies') -and
    $externalStarterKitTemplateValidatorSource.Contains('dependency id task must pass -DependencyId') -and
    $externalStarterKitTemplateValidatorSource.Contains('HECTON-8: build submission zip') -and
    $externalStarterKitTemplateValidatorSource.Contains('HECTON-8: create disabled graph node snippet') -and
    $externalStarterKitTemplateValidatorSource.Contains('replace task must pass -Replace') -and
    $externalStarterKitTemplateValidatorSource.Contains('${config:hecton8.powerShellExecutable}') -and
    $externalStarterKitTemplateValidatorSource.Contains('task must route through h8mod.ps1') -and
    $externalStarterKitTemplateValidatorSource.Contains('task must not call Tools scripts directly')
$externalStarterKitTemplateSettingsSchemaSource = Get-Content -Raw -LiteralPath (Join-Path $externalStarterKitTemplatePath 'Schemas\settings_table.schema.json')
$externalStarterKitTemplateLocaleSchemaSource = Get-Content -Raw -LiteralPath (Join-Path $externalStarterKitTemplatePath 'Schemas\locale.schema.json')
$externalStarterKitTemplateAssetsSchemaSource = Get-Content -Raw -LiteralPath (Join-Path $externalStarterKitTemplatePath 'Schemas\assets.schema.json')
$externalStarterKitAuthoringDataValidationProbe = Invoke-StarterAuthoringDataValidationProbe $externalStarterKitTemplatePath
$externalStarterKitValidatorChecksSettingsAndLocaleContracts =
    $externalStarterKitTemplateValidatorSource.Contains('function Validate-SettingsTable') -and
    $externalStarterKitTemplateValidatorSource.Contains('function Validate-LocaleTable') -and
    $externalStarterKitTemplateValidatorSource.Contains('Kind must be one of: bool, int, float, string, enum') -and
    $externalStarterKitTemplateValidatorSource.Contains('Strings exceeds 512 entries') -and
    $externalStarterKitTemplateSettingsSchemaSource.Contains('"maxItems": 128') -and
    $externalStarterKitTemplateSettingsSchemaSource.Contains('"enum": ["bool", "int", "float", "string", "enum"]') -and
    $externalStarterKitTemplateLocaleSchemaSource.Contains('"maxProperties": 512') -and
    $externalStarterKitTemplateLocaleSchemaSource.Contains('"propertyNames"') -and
    $externalStarterKitAuthoringDataValidationProbe.InvalidSettingsExitCode -ne 0 -and
    $externalStarterKitAuthoringDataValidationProbe.InvalidLocaleExitCode -ne 0
$externalStarterKitValidatorChecksAssetManifestContracts =
    $externalStarterKitTemplateValidatorSource.Contains('function Validate-AssetManifest') -and
    $externalStarterKitTemplateValidatorSource.Contains('Path must stay under Content/Assets/.') -and
    $externalStarterKitTemplateValidatorSource.Contains('Crc32 does not match file') -and
    $externalStarterKitTemplateValidatorSource.Contains('Content/assets.h8manifest.json Assets exceeds 512 entries.') -and
    $externalStarterKitTemplateValidatorSource.Contains('Content/assets.h8manifest.json total Bytes must not exceed mod.h8manifest.json Budgets.MaxAssetBytes.') -and
    $externalStarterKitTemplateValidatorSource.Contains("'Content/Assets/README.md'") -and
    $externalStarterKitTemplateAssetsSchemaSource.Contains('"maxItems": 512') -and
    $externalStarterKitTemplateAssetsSchemaSource.Contains('"enum": ["raw_texture", "audio_clip", "data_blob"]') -and
    $externalStarterKitTemplateAssetsSchemaSource.Contains('"pattern": "^[0-9A-Fa-f]{8}$"') -and
    $externalStarterKitTemplateAssetsSchemaSource.Contains('"maximum": 4194304') -and
    $moddingSdkHubSource.Contains('BuildStarterKitToolFromTemplate("Tools/validate_structure.ps1")')
$externalStarterKitValidatorChecksManifestCapabilities =
    $externalStarterKitTemplateValidatorSource.Contains('function Get-AllowedManifestCapabilities') -and
    $externalStarterKitTemplateValidatorSource.Contains('function Validate-ManifestCapabilities') -and
    $externalStarterKitTemplateValidatorSource.Contains('Capabilities exceeds 16 entries.') -and
    $externalStarterKitTemplateValidatorSource.Contains('Capability is not public') -and
    $externalStarterKitTemplateValidatorSource.Contains('Budgets.MaxEnvelopesPerFrame exceeds 256.') -and
    $externalStarterKitTemplateValidatorSource.Contains('Budgets.MaxAssetBytes exceeds 33554432.') -and
    $externalStarterKitTemplateValidatorSource.Contains("'Tools/configure_manifest_contract.ps1'") -and
    $externalStarterKitTemplateManifestContractToolSource.Contains('Get-AllowedCapabilityIds') -and
    $externalStarterKitTemplateManifestContractToolSource.Contains('MaxEnvelopesPerFrame cannot be lower than current graph requirement') -and
    $externalStarterKitTemplateManifestContractToolSource.Contains('MaxAssetBytes cannot be lower than currently declared asset bytes') -and
    $externalStarterKitTemplateManifestContractToolSource.Contains('Restore-ManifestBackup') -and
    $externalStarterKitTemplateManifestContractToolSource.Contains('Invoke-StarterValidator') -and
    $externalStarterKitTemplateManifestContractToolSource.Contains('hecton8.manifest_contract_config.v1') -and
    $externalStarterKitManifestContractRejectsUnknownCapability
$externalStarterKitSchemaRelativePaths = @(
    'Schemas\assets.schema.json',
    'Schemas\h8graph.schema.json',
    'Schemas\h8mod.authoring.schema.json',
    'Schemas\locale.schema.json',
    'Schemas\runtime.mod.schema.json',
    'Schemas\settings_table.schema.json'
)
$externalStarterKitTemplateJsonSchemasVersioned = $true
$externalStarterKitTemplateJsonSchemasParse = $true
foreach ($schemaRelativePath in $externalStarterKitSchemaRelativePaths) {
    $schemaFilePath = Join-Path $externalStarterKitTemplatePath $schemaRelativePath
    if (-not (Test-Path -LiteralPath $schemaFilePath -PathType Leaf)) {
        $externalStarterKitTemplateJsonSchemasVersioned = $false
        $externalStarterKitTemplateJsonSchemasParse = $false
        continue
    }

    try {
        $templateSchema = Get-Content -Raw -LiteralPath $schemaFilePath | ConvertFrom-Json
        if ($null -eq $templateSchema.PSObject.Properties['$schema'] -or
            [string]::IsNullOrWhiteSpace([string]$templateSchema.title) -or
            [string]$templateSchema.type -ne 'object') {
            $externalStarterKitTemplateJsonSchemasParse = $false
        }
    } catch {
        $externalStarterKitTemplateJsonSchemasParse = $false
    }
}
$externalStarterKitEditorSchemaMappingPresent = $false
$schemaMappingsProperty = $externalStarterKitTemplateVsCodeSettings.PSObject.Properties['json.schemas']
if ($null -ne $schemaMappingsProperty) {
    $schemaMappingUrls = @($schemaMappingsProperty.Value | ForEach-Object { [string]$_.url })
    $externalStarterKitEditorSchemaMappingPresent =
        $schemaMappingUrls -contains './Schemas/h8mod.authoring.schema.json' -and
        $schemaMappingUrls -contains './Schemas/runtime.mod.schema.json' -and
        $schemaMappingUrls -contains './Schemas/h8graph.schema.json' -and
        $schemaMappingUrls -contains './Schemas/assets.schema.json' -and
        $schemaMappingUrls -contains './Schemas/settings_table.schema.json' -and
    $schemaMappingUrls -contains './Schemas/locale.schema.json'
}
$externalStarterKitIdentityProbe = Invoke-StarterIdentityToolProbe $externalStarterKitTemplatePath
$externalStarterKitIdentityToolPasses =
    $externalStarterKitIdentityProbe.ExitCode -eq 0 -and
    $externalStarterKitIdentityProbe.Output -contains 'PASS HECTON-8 starter identity set: com.validation.identity' -and
    $externalStarterKitIdentityProbe.AuthoringId -eq 'com.validation.identity' -and
    $externalStarterKitIdentityProbe.RuntimeId -eq 'com.validation.identity' -and
    $externalStarterKitIdentityProbe.AuthoringDisplayName -eq 'Validation Identity' -and
    $externalStarterKitIdentityProbe.RuntimeName -eq 'Validation Identity' -and
    $externalStarterKitIdentityProbe.AuthoringAuthor -eq 'StaticValidator' -and
    $externalStarterKitIdentityProbe.RuntimeAuthor -eq 'StaticValidator' -and
    $externalStarterKitIdentityProbe.AuthoringVersion -eq '9.9.9' -and
    $externalStarterKitIdentityProbe.RuntimeVersion -eq '9.9.9'
$externalStarterKitPrepareProbe = Invoke-StarterPrepareToolProbe $externalStarterKitTemplatePath
$externalStarterKitPrepareToolPasses =
    $externalStarterKitPrepareProbe.ExitCode -eq 0 -and
    $externalStarterKitPrepareProbe.Output -contains 'PASS HECTON-8 starter prepared: com.validation.prepared' -and
    $externalStarterKitPrepareProbe.AuthoringId -eq 'com.validation.prepared' -and
    $externalStarterKitPrepareProbe.RuntimeId -eq 'com.validation.prepared' -and
    $externalStarterKitPrepareProbe.ReviewRootId -eq 'com.validation.prepared' -and
    $externalStarterKitPrepareProbe.ReviewIdentityId -eq 'com.validation.prepared' -and
    $externalStarterKitPrepareProbe.ReviewIdentityDisplayName -eq 'Prepared Validation' -and
    $externalStarterKitPrepareProbe.ReviewIdentityAuthor -eq 'StaticValidator' -and
    $externalStarterKitPrepareProbe.ReviewIdentityVersion -eq '10.0.0' -and
    $externalStarterKitPrepareProbe.ReviewIdentityRequiredAPIVersion -eq 2 -and
    $externalStarterKitPrepareProbe.ReviewIdentityModPriority -eq 0 -and
    $externalStarterKitPrepareProbe.ReviewFileCount -gt 0 -and
    $externalStarterKitPrepareProbe.ReviewTotalBytes -gt 0 -and
    $externalStarterKitPrepareProbe.ReviewMaxFiles -eq 256 -and
    $externalStarterKitPrepareProbe.ReviewMaxFileBytes -eq 4194304 -and
    $externalStarterKitPrepareProbe.ReviewMaxTotalBytes -eq 33554432 -and
    $externalStarterKitPrepareProbe.ReviewHasPrepareTool -eq $true -and
    $externalStarterKitPrepareProbe.ReviewHasSubmissionPackageTool -eq $true -and
    $externalStarterKitPrepareProbe.ReviewHasAllowedOpcodeListTool -eq $true -and
    $externalStarterKitPrepareProbe.ReviewHasGraphNodeSnippetTool -eq $true -and
    $externalStarterKitPrepareProbe.ReviewHasGraphNodeApplyTool -eq $true -and
    $externalStarterKitPrepareProbe.ReviewHasSettingsRowSnippetTool -eq $true -and
    $externalStarterKitPrepareProbe.ReviewHasLocaleEntrySnippetTool -eq $true -and
    $externalStarterKitPrepareProbe.ReviewHasManifestContractTool -eq $true -and
    $externalStarterKitPrepareProbe.ReviewHasDependenciesTool -eq $true -and
    $externalStarterKitPrepareProbe.ReviewExcludesReports -eq $true
$externalStarterKitPrepareToolSupportsExistingManifest =
    $externalStarterKitPrepareProbe.ExistingExitCode -eq 0 -and
    $externalStarterKitPrepareProbe.ExistingOutput -contains 'PASS HECTON-8 starter prepared: com.validation.prepared' -and
    $externalStarterKitPrepareProbe.ExistingReviewRootId -eq 'com.validation.prepared' -and
    $externalStarterKitPrepareProbe.ExistingReviewIdentityId -eq 'com.validation.prepared' -and
    $externalStarterKitTemplatePrepareToolSource.Contains('$hasIdentityEdits = -not [string]::IsNullOrWhiteSpace($Id)') -and
    $externalStarterKitTemplatePrepareToolSource.Contains('Review manifest did not report package identity')
$externalStarterKitReviewManifestLimitProbe = Invoke-StarterReviewManifestLimitProbe $externalStarterKitTemplatePath
$externalStarterKitReviewManifestRejectsOversizedFile =
    $externalStarterKitReviewManifestLimitProbe.ExitCode -ne 0
$externalStarterKitReviewManifestHasLimits =
    $externalStarterKitTemplateReviewManifestBuilderSource.Contains('$MaxReviewFiles = 256') -and
    $externalStarterKitTemplateReviewManifestBuilderSource.Contains('$MaxReviewFileBytes = 4194304') -and
    $externalStarterKitTemplateReviewManifestBuilderSource.Contains('$MaxReviewTotalBytes = 33554432') -and
    $externalStarterKitTemplateReviewManifestBuilderSource.Contains('Review manifest source file limit exceeded') -and
    $externalStarterKitTemplateReviewManifestBuilderSource.Contains('Review file exceeds max bytes') -and
    $externalStarterKitTemplateReviewManifestBuilderSource.Contains('Review manifest total byte limit exceeded') -and
    $externalStarterKitTemplateReviewManifestBuilderSource.Contains('Limits = [pscustomobject][ordered]@{') -and
    $externalStarterKitTemplateReviewManifestBuilderSource.Contains('TotalBytes = $totalBytes') -and
    $externalStarterKitTemplateReviewManifestBuilderSource.Contains('Identity = [pscustomobject][ordered]@{') -and
    $moddingSdkHubSource.Contains('BuildStarterKitToolFromTemplate("Tools/build_review_manifest.ps1")') -and
    $externalStarterKitPrepareToolPasses -and
    $externalStarterKitReviewManifestRejectsOversizedFile
$externalStarterKitTemplateRequiredFiles = @(
    'README.md',
    'h8mod.ps1',
    'mod.h8manifest.json',
    'mod.json',
    'Content\README.md',
    'Content\Assets\README.md',
    'Content\assets.h8manifest.json',
    'Graphs\main.h8graph.json',
    'Tables\settings.h8table.json',
    'Locales\en.h8loc.json',
    'Generated\README.md',
    'Reports\README.md',
    'Reference\README.md',
    'Reference\allowed_opcodes.csv',
    'Reference\kernel_tuning_profiles.csv',
    'Schemas\assets.schema.json',
    'Schemas\h8graph.schema.json',
    'Schemas\h8mod.authoring.schema.json',
    'Schemas\locale.schema.json',
    'Schemas\runtime.mod.schema.json',
    'Schemas\settings_table.schema.json',
    'Tools\README.md',
    'Tools\build_review_manifest.ps1',
    'Tools\build_submission_package.ps1',
    'Tools\create_first_mod.ps1',
    'Tools\configure_dependencies.ps1',
    'Tools\install_local_mod.ps1',
    'Tools\diagnose_local_mods.ps1',
    'Tools\run_doctor.ps1',
    'Tools\apply_graph_node_snippet.ps1',
    'Tools\apply_asset_entry_snippet.ps1',
    'Tools\apply_locale_entry_snippet.ps1',
    'Tools\apply_settings_row_snippet.ps1',
    'Tools\create_locale_entry_snippet.ps1',
    'Tools\create_graph_node_snippet.ps1',
    'Tools\create_asset_entry_snippet.ps1',
    'Tools\create_settings_row_snippet.ps1',
    'Tools\configure_manifest_contract.ps1',
    'Tools\list_allowed_opcodes.ps1',
    'Tools\prepare_mod.ps1',
    'Tools\set_mod_identity.ps1',
    'Tools\validate_structure.ps1',
    '.vscode\settings.json',
    '.vscode\tasks.json'
)
$externalStarterKitTemplateVersioned = $true
foreach ($requiredTemplateFile in $externalStarterKitTemplateRequiredFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $externalStarterKitTemplatePath $requiredTemplateFile) -PathType Leaf)) {
        $externalStarterKitTemplateVersioned = $false
    }
}
$externalStarterKitTemplatePassesLocalValidator =
    $externalStarterKitTemplateLocalValidatorExitCode -eq 0 -and
    @($externalStarterKitTemplateValidatorOutput) -contains 'PASS HECTON-8 external starter structure'
$externalStarterKitRootLauncherPasses =
    $externalStarterKitTemplateLauncherValidateExitCode -eq 0 -and
    @($externalStarterKitTemplateLauncherValidateOutput) -contains 'PASS HECTON-8 external starter structure'
$externalStarterKitValidatorChecksRootLauncher =
    $externalStarterKitTemplateValidatorSource.Contains("'h8mod.ps1'") -and
    $moddingSdkHubSource.Contains('BuildStarterKitToolFromTemplate("h8mod.ps1")')
$externalStarterKitTemplateAllowedOpcodesPath = Join-Path $externalStarterKitTemplatePath 'Reference\allowed_opcodes.csv'
$externalStarterKitTemplateKernelTuningPath = Join-Path $externalStarterKitTemplatePath 'Reference\kernel_tuning_profiles.csv'
$externalStarterKitTemplateReferenceCsvsMatchSource = $false
if ((Test-Path -LiteralPath $externalStarterKitTemplateAllowedOpcodesPath -PathType Leaf) -and
    (Test-Path -LiteralPath $externalStarterKitTemplateKernelTuningPath -PathType Leaf)) {
    $externalStarterKitTemplateReferenceCsvsMatchSource =
        (Normalize-TextForCompare (Get-Content -Raw -LiteralPath $externalStarterKitTemplateAllowedOpcodesPath)) -eq (Normalize-TextForCompare $allowedOpcodesCsvText) -and
        (Normalize-TextForCompare (Get-Content -Raw -LiteralPath $externalStarterKitTemplateKernelTuningPath)) -eq (Normalize-TextForCompare $kernelTuningProfilesCsvText)
}
$externalStarterKitReviewManifestPasses =
    $externalStarterKitTemplateReviewManifestExitCode -eq 0 -and
    @($externalStarterKitTemplateReviewManifestOutput) -contains 'PASS HECTON-8 review manifest: Reports/review_manifest.json' -and
    (Test-Path -LiteralPath $externalStarterKitTemplateReviewManifestPath -PathType Leaf)
$externalStarterKitReviewManifest = $null
if (Test-Path -LiteralPath $externalStarterKitTemplateReviewManifestPath -PathType Leaf) {
    try {
        $externalStarterKitReviewManifest = Get-Content -Raw -LiteralPath $externalStarterKitTemplateReviewManifestPath | ConvertFrom-Json
    } catch {
        $externalStarterKitReviewManifest = $null
    }
}
$externalStarterKitReviewManifestPaths = @()
$externalStarterKitReviewManifestHashShapeValid = $false
$externalStarterKitReviewManifestIncludesIdentity = $false
$externalStarterKitReviewManifestIdentityMatchesRuntimeManifest = $false
if ($null -ne $externalStarterKitReviewManifest) {
    $externalStarterKitReviewManifestFiles = @($externalStarterKitReviewManifest.Files)
    $externalStarterKitReviewManifestPaths = @($externalStarterKitReviewManifestFiles | ForEach-Object { [string]$_.Path })
    $externalStarterKitReviewManifestHashShapeValid = $externalStarterKitReviewManifestFiles.Count -gt 0
    foreach ($reviewManifestFile in $externalStarterKitReviewManifestFiles) {
        if ([string]::IsNullOrWhiteSpace([string]$reviewManifestFile.Path) -or
            [long]$reviewManifestFile.Bytes -lt 0 -or
            ([string]$reviewManifestFile.Sha256) -notmatch '^[0-9a-f]{64}$') {
            $externalStarterKitReviewManifestHashShapeValid = $false
        }
    }

    $templateAuthoring = Get-Content -Raw -LiteralPath (Join-Path $externalStarterKitTemplatePath 'mod.h8manifest.json') | ConvertFrom-Json
    $templateRuntime = Get-Content -Raw -LiteralPath (Join-Path $externalStarterKitTemplatePath 'mod.json') | ConvertFrom-Json
    $externalStarterKitReviewManifestIncludesIdentity =
        $null -ne $externalStarterKitReviewManifest.Identity -and
        -not [string]::IsNullOrWhiteSpace([string]$externalStarterKitReviewManifest.Identity.Id) -and
        -not [string]::IsNullOrWhiteSpace([string]$externalStarterKitReviewManifest.Identity.DisplayName) -and
        -not [string]::IsNullOrWhiteSpace([string]$externalStarterKitReviewManifest.Identity.Author) -and
        ([string]$externalStarterKitReviewManifest.Identity.Version) -match '^(0|[1-9][0-9]*)[.](0|[1-9][0-9]*)[.](0|[1-9][0-9]*)(-[0-9A-Za-z]+([.-][0-9A-Za-z]+)*)?([+][0-9A-Za-z]+([.-][0-9A-Za-z]+)*)?$' -and
        [int]$externalStarterKitReviewManifest.Identity.RequiredAPIVersion -eq [int]$templateRuntime.RequiredAPIVersion -and
        [int]$externalStarterKitReviewManifest.Identity.ModPriority -eq [int]$templateRuntime.ModPriority
    $externalStarterKitReviewManifestIdentityMatchesRuntimeManifest =
        $externalStarterKitReviewManifestIncludesIdentity -and
        [string]$externalStarterKitReviewManifest.RootId -eq [string]$externalStarterKitReviewManifest.Identity.Id -and
        [string]$externalStarterKitReviewManifest.Identity.Id -eq [string]$templateRuntime.Id -and
        [string]$externalStarterKitReviewManifest.Identity.Id -eq [string]$templateAuthoring.Id -and
        [string]$externalStarterKitReviewManifest.Identity.DisplayName -eq [string]$templateRuntime.Name -and
        [string]$externalStarterKitReviewManifest.Identity.DisplayName -eq [string]$templateAuthoring.DisplayName -and
        [string]$externalStarterKitReviewManifest.Identity.Author -eq [string]$templateRuntime.Author -and
        [string]$externalStarterKitReviewManifest.Identity.Author -eq [string]$templateAuthoring.Author -and
        [string]$externalStarterKitReviewManifest.Identity.Version -eq [string]$templateRuntime.Version -and
        [string]$externalStarterKitReviewManifest.Identity.Version -eq [string]$templateAuthoring.Version
}
$externalStarterKitReviewManifestHashesFiles =
    $externalStarterKitReviewManifestPasses -and
    $null -ne $externalStarterKitReviewManifest -and
    [string]$externalStarterKitReviewManifest.Schema -eq 'hecton8.external_review_manifest.v1' -and
    [string]$externalStarterKitReviewManifest.Runtime -eq 'envelope-only' -and
    [string]$externalStarterKitReviewManifest.RootId -eq 'com.example.starter' -and
    $externalStarterKitReviewManifestIncludesIdentity -and
    $externalStarterKitReviewManifestIdentityMatchesRuntimeManifest -and
    [int]$externalStarterKitReviewManifest.FileCount -eq @($externalStarterKitReviewManifest.Files).Count -and
    $externalStarterKitReviewManifestHashShapeValid -and
    $externalStarterKitReviewManifestPaths -contains 'mod.h8manifest.json' -and
    $externalStarterKitReviewManifestPaths -contains 'h8mod.ps1' -and
    $externalStarterKitReviewManifestPaths -contains 'mod.json' -and
    $externalStarterKitReviewManifestPaths -contains 'Content/Assets/README.md' -and
    $externalStarterKitReviewManifestPaths -contains 'Content/assets.h8manifest.json' -and
    $externalStarterKitReviewManifestPaths -contains '.vscode/settings.json' -and
    $externalStarterKitReviewManifestPaths -contains '.vscode/tasks.json' -and
    $externalStarterKitReviewManifestPaths -contains 'Tools/build_review_manifest.ps1' -and
    $externalStarterKitReviewManifestPaths -contains 'Tools/build_submission_package.ps1' -and
    $externalStarterKitReviewManifestPaths -contains 'Tools/create_graph_node_snippet.ps1' -and
    $externalStarterKitReviewManifestPaths -contains 'Tools/apply_graph_node_snippet.ps1' -and
    $externalStarterKitReviewManifestPaths -contains 'Tools/create_settings_row_snippet.ps1' -and
    $externalStarterKitReviewManifestPaths -contains 'Tools/apply_settings_row_snippet.ps1' -and
    $externalStarterKitReviewManifestPaths -contains 'Tools/create_locale_entry_snippet.ps1' -and
    $externalStarterKitReviewManifestPaths -contains 'Tools/apply_locale_entry_snippet.ps1' -and
    $externalStarterKitReviewManifestPaths -contains 'Tools/create_asset_entry_snippet.ps1' -and
    $externalStarterKitReviewManifestPaths -contains 'Tools/apply_asset_entry_snippet.ps1' -and
    $externalStarterKitReviewManifestPaths -contains 'Tools/configure_manifest_contract.ps1' -and
    $externalStarterKitReviewManifestPaths -contains 'Tools/configure_dependencies.ps1' -and
    $externalStarterKitReviewManifestPaths -contains 'Tools/create_first_mod.ps1' -and
    $externalStarterKitReviewManifestPaths -contains 'Tools/install_local_mod.ps1' -and
    $externalStarterKitReviewManifestPaths -contains 'Tools/run_doctor.ps1' -and
    $externalStarterKitReviewManifestPaths -contains 'Tools/list_allowed_opcodes.ps1' -and
    $externalStarterKitReviewManifestPaths -contains 'Tools/prepare_mod.ps1'
$externalStarterKitReviewManifestExcludesReports =
    $externalStarterKitReviewManifestPasses -and
    (@($externalStarterKitReviewManifestPaths | Where-Object { $_ -like 'Reports/*' -or $_ -like 'Generated/*' }).Count -eq 0)
Assert-True $externalStarterKitGeneratorPresent 'ModdingSdkHubWindow must create/open an external starter kit.'
Assert-True $externalStarterKitWritesRootLauncher 'External starter kit generator must write the root no-Unity h8mod.ps1 launcher.'
Assert-True $externalStarterKitWritesAuthoringManifest 'External starter kit must write mod.h8manifest.json authoring contract.'
Assert-True $externalStarterKitWritesRuntimeManifest 'External starter kit must write mod.json runtime compatibility manifest.'
Assert-True $externalStarterKitWritesFolderReadmes 'External starter kit must write folder README guidance.'
Assert-True $externalStarterKitWritesCapabilityGuide 'External starter kit must write Docs/capabilities.md capability guidance.'
Assert-True $externalStarterKitCopiesOpcodeReferences 'External starter kit must copy opcode and tuning reference CSV files.'
Assert-True $externalStarterKitDocumentsNoUnityProjectRequirement 'External starter kit must document that normal authors do not need the full Unity project.'
Assert-True $externalStarterKitDocumentsEnvelopeOnlyBoundary 'External starter kit must document the envelope-only boundary.'
Assert-True $externalStarterKitWritesLocalStructureValidator 'External starter kit must write a local no-Unity structure validator.'
Assert-True $externalStarterKitValidatorChecksRequiredFiles 'External starter kit local validator must check required files and reference CSVs.'
Assert-True $externalStarterKitValidatorChecksCapabilityGuide 'External starter kit local validator must reject missing or stale capability guidance.'
Assert-True $externalStarterKitValidatorChecksRootLauncher 'External starter kit local validator must require the root no-Unity h8mod.ps1 launcher.'
Assert-True $externalStarterKitValidatorChecksEnvelopeOnly 'External starter kit local validator must check envelope-only manifest/graph flags.'
Assert-True $externalStarterKitValidatorChecksManagedEntryDisabled 'External starter kit local validator must reject managed entry fields.'
Assert-True $externalStarterKitValidatorChecksCanonicalIds 'External starter kit local validator must enforce canonical mod IDs.'
Assert-True $externalStarterKitValidatorChecksManifestIdParity 'External starter kit local validator must require authoring/runtime manifest ID parity.'
Assert-True $externalStarterKitValidatorChecksDependencyIds 'External starter kit local validator must check runtime dependency IDs.'
Assert-True $externalStarterKitValidatorChecksGraphOpcodes 'External starter kit local validator must check graph node IDs and opcode allowlist membership.'
Assert-True $externalStarterKitValidatorChecksGraphBudget 'External starter kit local validator must check graph budget parity with the authoring manifest.'
Assert-True $externalStarterKitValidatorRejectsInvalidGraphOpcode 'External starter kit local validator must reject invalid graph opcodes on a temp copy.'
Assert-True $externalStarterKitWritesReviewManifestBuilder 'External starter kit must write a no-Unity review manifest builder.'
Assert-True $externalStarterKitWritesSubmissionPackageTool 'External starter kit must write a no-Unity submission package tool.'
Assert-True $externalStarterKitSubmissionPackagePreservesPreviousOutputUntilSuccess 'External starter kit submission package tool must keep the previous zip until the new temp zip has been written successfully.'
Assert-True $externalStarterKitSubmissionPackageRefreshesTimestampAfterReplace 'External starter kit submission package tool must keep the final zip timestamp at or after the rebuilt review manifest.'
Assert-True $externalStarterKitReviewManifestRejectsCaseFoldSourceDuplicates 'External starter kit review manifest builder must reject duplicate and case-fold duplicate source paths.'
Assert-True $externalStarterKitSubmissionPackageUsesCaseExactSourceEntries 'External starter kit submission package builder must use exact source entry casing and reject case-fold duplicates.'
Assert-True $externalStarterKitValidatorRequiresExactPathCasing 'External starter kit local validator must require exact path casing for required files and folders.'
Assert-True $externalStarterKitRejectsReservedTopLevelCaseVariants 'External starter kit tools must reject reserved top-level folder case variants.'
Assert-True $externalStarterKitReviewManifestUsesExactReservedOutputFolders 'External starter kit review/submission builders must treat only exact Generated/ and Reports/ as reserved output folders.'
Assert-True $externalStarterKitWritesIdentityTool 'External starter kit must write a no-Unity identity helper.'
Assert-True $externalStarterKitWritesPrepareTool 'External starter kit must write a one-command no-Unity prepare tool.'
Assert-True $externalStarterKitWritesAllowedOpcodeListTool 'External starter kit must write a no-Unity allowed opcode list helper.'
Assert-True $externalStarterKitAllowedOpcodeListToolPasses 'External starter kit allowed opcode list helper must print current opcode aliases and hashes.'
Assert-True $externalStarterKitAllowedOpcodeListToolSupportsJson 'External starter kit allowed opcode list helper must emit machine-readable JSON for Workbench/CLI reuse.'
Assert-True $externalStarterKitWritesGraphNodeSnippetTool 'External starter kit must write a no-Unity graph node snippet helper.'
Assert-True $externalStarterKitGraphNodeSnippetToolPasses 'External starter kit graph node snippet helper must write a validated Generated/ graph node snippet and reject invalid opcodes.'
Assert-True $externalStarterKitGraphNodeSnippetToolSupportsJson 'External starter kit graph node snippet helper must emit machine-readable JSON for Workbench/CLI reuse.'
Assert-True $externalStarterKitGraphNodeSnippetSupportsParameters 'External starter kit graph node snippet helper must accept bounded Parameters JSON and disabled-node output.'
Assert-True $externalStarterKitRootLauncherSupportsGraphNodeSnippet 'External starter kit root launcher must route node-snippet to the graph node snippet helper.'
Assert-True $externalStarterKitRootLauncherSupportsGraphNodeParameters 'External starter kit root launcher must pass graph node Parameters JSON and disabled-node flags to the snippet helper.'
Assert-True $externalStarterKitWritesGraphNodeApplyTool 'External starter kit must write a no-Unity graph node snippet apply helper.'
Assert-True $externalStarterKitGraphNodeApplyToolPasses 'External starter kit graph node apply helper must insert the generated node into Graphs/main.h8graph.json, repair minimum budget, and validate after write.'
Assert-True $externalStarterKitGraphNodeApplyToolRejectsDuplicateWithoutReplace 'External starter kit graph node apply helper must reject duplicate node ids unless -Replace is explicit.'
Assert-True $externalStarterKitRootLauncherSupportsGraphNodeApply 'External starter kit root launcher must route apply-node-snippet to the graph node apply helper.'
Assert-True $externalStarterKitWritesSettingsRowSnippetTool 'External starter kit must write a no-Unity settings row snippet helper.'
Assert-True $externalStarterKitSettingsRowSnippetToolPasses 'External starter kit settings row snippet helper must write a validated Generated/ settings row snippet and reject invalid kind/default values.'
Assert-True $externalStarterKitSettingsRowSnippetToolSupportsJson 'External starter kit settings row snippet helper must emit machine-readable JSON for Workbench/CLI reuse.'
Assert-True $externalStarterKitWritesSettingsRowApplyTool 'External starter kit must write a no-Unity settings row snippet apply helper.'
Assert-True $externalStarterKitSettingsRowApplyToolPasses 'External starter kit settings row apply helper must insert the generated row into Tables/settings.h8table.json and validate after write.'
Assert-True $externalStarterKitSettingsRowApplyToolRejectsDuplicateWithoutReplace 'External starter kit settings row apply helper must reject duplicate IDs unless -Replace is explicit.'
Assert-True $externalStarterKitWritesLocaleEntrySnippetTool 'External starter kit must write a no-Unity locale entry snippet helper.'
Assert-True $externalStarterKitLocaleEntrySnippetToolPasses 'External starter kit locale entry snippet helper must write a validated Generated/ locale entry snippet and reject invalid keys/values.'
Assert-True $externalStarterKitLocaleEntrySnippetToolSupportsJson 'External starter kit locale entry snippet helper must emit machine-readable JSON for Workbench/CLI reuse.'
Assert-True $externalStarterKitWritesLocaleEntryApplyTool 'External starter kit must write a no-Unity locale entry snippet apply helper.'
Assert-True $externalStarterKitLocaleEntryApplyToolPasses 'External starter kit locale entry apply helper must insert the generated entry into Locales/en.h8loc.json and validate after write.'
Assert-True $externalStarterKitLocaleEntryApplyToolRejectsDuplicateWithoutReplace 'External starter kit locale entry apply helper must reject duplicate keys unless -Replace is explicit.'
Assert-True $externalStarterKitWritesAssetEntrySnippetTool 'External starter kit must write a no-Unity content asset entry snippet helper.'
Assert-True $externalStarterKitAssetEntrySnippetToolPasses 'External starter kit content asset entry snippet helper must write a validated Generated/ asset entry and reject invalid kind/path/CRC input.'
Assert-True $externalStarterKitAssetEntrySnippetToolSupportsJson 'External starter kit content asset entry snippet helper must emit machine-readable JSON for Workbench/CLI reuse.'
Assert-True $externalStarterKitWritesAssetEntryApplyTool 'External starter kit must write a no-Unity content asset entry apply helper.'
Assert-True $externalStarterKitAssetEntryApplyToolPasses 'External starter kit content asset apply helper must insert the generated asset, verify file CRC/bytes, repair asset byte budget, and validate after write.'
Assert-True $externalStarterKitAssetEntryApplyToolRejectsDuplicateWithoutReplace 'External starter kit content asset apply helper must reject duplicate asset IDs unless -Replace is explicit.'
Assert-True $externalStarterKitSnippetPathsRequireStrictJsonRelativePaths 'External starter kit snippet helpers must require exact starter-relative Generated/*.json paths and reject ADS/empty/dot segments.'
Assert-True $externalStarterKitSnippetApplyUsesBoundedJsonReads 'External starter kit snippet apply helpers must cap JSON reads before ConvertFrom-Json.'
Assert-True $externalStarterKitAssetSnippetRejectsNonPortableAssetPaths 'External starter kit asset snippet helpers must reject non-portable Content/Assets paths before CRC/file probing.'
Assert-True $externalStarterKitGraphNodeSnippetCapsParametersJson 'External starter kit graph node snippet helper must cap ParametersJson before parsing.'
Assert-True $externalStarterKitValidatorCapsRootJsonReads 'External starter kit structure validator must cap all root JSON/text reads before parsing.'
Assert-True $externalStarterKitValidatorRejectsNonPortableAssetManifestPaths 'External starter kit structure validator must reject non-portable manually edited asset manifest paths.'
Assert-True $externalStarterKitCoreToolsUseBoundedJsonReads 'External starter kit core prepare/identity/dependency/package/doctor tools must cap external JSON reads before parsing.'
Assert-True $externalStarterKitCoreToolsUseStreamingStrictUtf8JsonReads 'External starter kit no-Unity tools must use shared streaming capped strict UTF-8 JSON/text reads.'
Assert-True $externalStarterKitWritesManifestContractTool 'External starter kit must write a no-Unity manifest capability/budget configuration helper.'
Assert-True $externalStarterKitManifestContractToolPasses 'External starter kit manifest contract helper must configure allowlisted capabilities/budgets and validate after write.'
Assert-True $externalStarterKitManifestContractRejectsUnknownCapability 'External starter kit manifest contract helper must reject unknown public capability IDs.'
Assert-True $externalStarterKitWritesFirstModTool 'External starter kit must write a no-Unity first playable mod onboarding helper.'
Assert-True $externalStarterKitWritesInstallLocalTool 'External starter kit must write a no-Unity local discovery install helper.'
Assert-True $externalStarterKitWritesDiagnoseLocalTool 'External starter kit must write a no-Unity read-only local Mods diagnosis helper.'
Assert-True $externalStarterKitLocalInstallRequiresExactReviewProof 'External starter kit local install must require exact review output casing, lowercase SHA-256 rows, and reject case-fold duplicate review entries.'
Assert-True $externalStarterKitDiagnoseLocalRequiresExactReviewProof 'External starter kit local Mods diagnosis must require exact lowercase review proof and reject case-fold duplicate review entries.'
Assert-True $externalStarterKitDiagnoseLocalUsesRecursiveManifestDiscovery 'External starter kit local diagnosis must mirror recursive ModLoader mod.json discovery.'
Assert-True $externalStarterKitDiagnoseLocalChecksDependencyGraph 'External starter kit local diagnosis must report duplicate IDs, missing dependencies, dependency cycles, and load order.'
Assert-True $externalStarterKitWritesDependenciesTool 'External starter kit must write a no-Unity dependency configuration helper.'
Assert-True $externalStarterKitDependencyToolMirrorsBothManifests 'External starter kit dependency helper must mirror Dependencies across authoring/runtime manifests and validate after write.'
Assert-True $externalStarterKitDependenciesToolWritesStrictUtf8NoBom 'External starter kit dependency helper must write manifest JSON as strict UTF-8 without BOM.'
Assert-True $externalStarterKitWritesDoctorTool 'External starter kit must write a no-Unity read-only package doctor.'
Assert-True $externalStarterKitDoctorToolIsReadOnly 'External starter kit package doctor must be read-only and bounded.'
Assert-True $externalStarterKitDoctorVerifiesSubmissionZipContents 'External starter kit package doctor must verify submission zip contents against the review manifest.'
Assert-True $externalStarterKitDoctorRejectsUnsafeZipEntries 'External starter kit package doctor must reject unsafe, duplicate, generated, and unreviewed submission zip entries.'
Assert-True $externalStarterKitDoctorUsesCaseExactSubmissionZipIntegrity 'External starter kit package doctor must use case-exact submission zip integrity and reject case-fold duplicates.'
Assert-True $externalStarterKitDoctorRejectsReservedTopLevelCaseVariants 'External starter kit package doctor must reject reserved top-level folder case variants in sources and zip entries.'
Assert-True $externalStarterKitDoctorFailsNonReadyExit 'External starter kit package doctor must exit nonzero for needs_review and invalid readiness states.'
Assert-True $externalStarterKitRootLauncherPreservesToolExitCodes 'External starter kit root launcher must preserve delegated tool exit codes instead of collapsing them to exit 1.'
Assert-True $externalStarterKitNestedToolsPreserveChildExitCodes 'External starter kit nested parent tools must capture child $? and LASTEXITCODE immediately and preserve child exit codes.'
Assert-True $externalStarterKitRootLauncherSupportsAuthoringSnippets 'External starter kit root launcher must route setting-snippet and locale-snippet to authoring snippet helpers.'
Assert-True $externalStarterKitRootLauncherSupportsAuthoringSnippetApply 'External starter kit root launcher must route apply-setting-snippet and apply-locale-snippet to bounded authoring data apply helpers.'
Assert-True $externalStarterKitRootLauncherSupportsAssetEntrySnippet 'External starter kit root launcher must route asset-snippet to the content asset entry snippet helper.'
Assert-True $externalStarterKitRootLauncherSupportsAssetEntryApply 'External starter kit root launcher must route apply-asset-snippet to the bounded content asset apply helper.'
Assert-True $externalStarterKitRootLauncherSupportsManifestContract 'External starter kit root launcher must route manifest-contract to the bounded manifest contract helper.'
Assert-True $externalStarterKitRootLauncherSupportsCapabilities 'External starter kit root launcher must route capabilities to Docs/capabilities.md.'
Assert-True $externalStarterKitRootLauncherUsesStrictCapabilityGuideRead 'External starter kit root launcher must print Docs/capabilities.md through the shared strict capped UTF-8 reader.'
Assert-True $externalStarterKitHubScriptsRequireCheckedInTemplates 'External starter kit Hub must copy root/tool scripts from checked-in templates or generate fail-closed missing-template scripts, not stale C# fallbacks.'
Assert-True $externalStarterKitHubExecutableFallbackBodiesRemoved 'External starter kit Hub must not retain embedded executable fallback bodies after switching root/tool scripts to checked-in templates.'
Assert-True $externalStarterKitSubmissionPackageToolPasses 'External starter kit submission package tool must write a Generated/ zip on a temp copy.'
Assert-True $externalStarterKitSubmissionPackageIncludesReviewManifest 'External starter kit submission package must include Reports/review_manifest.json as the review proof.'
Assert-True $externalStarterKitRootLauncherSupportsSubmissionPackage 'External starter kit root launcher must route submission to the submission package helper.'
Assert-True $externalStarterKitRootLauncherSupportsFirstMod 'External starter kit root launcher must route first-mod to the first playable mod helper.'
Assert-True $externalStarterKitRootLauncherSupportsInstallLocal 'External starter kit root launcher must route install-local to the local discovery install helper.'
Assert-True $externalStarterKitRootLauncherSupportsDiagnoseLocal 'External starter kit root launcher must route diagnose-local to the read-only local Mods diagnosis helper.'
Assert-True $externalStarterKitRootLauncherSupportsDoctor 'External starter kit root launcher must route doctor to the read-only package doctor.'
Assert-True $externalStarterKitRootLauncherSupportsDependencies 'External starter kit root launcher must route dependencies to the dependency configuration helper.'
Assert-True $externalStarterKitIdentityToolValidatesCanonicalId 'External starter kit identity helper must validate canonical mod IDs.'
Assert-True $externalStarterKitValidatorChecksSemver 'External starter kit validator and identity helper must enforce semantic version strings.'
Assert-True $externalStarterKitValidatorChecksManifestIdentityTextParity 'External starter kit local validator must enforce display name, author, and version parity across manifests.'
Assert-True $externalStarterKitIdentityToolRejectsInvalidVersion 'External starter kit identity helper must reject invalid version strings on a temp copy.'
Assert-True $externalStarterKitIdentityToolRollsBackBothManifestsOnValidationFailure 'External starter kit identity helper must restore both manifests when post-write validation fails.'
Assert-True $externalStarterKitToolsAvoidNestedPowerShell 'External starter kit tools must chain scripts in-process instead of requiring nested Windows PowerShell.'
Assert-True $externalStarterKitToolsUsePortableJoinPath 'External starter kit tools must compose child paths through portable Join-Path segments.'
Assert-True $externalStarterKitWritesJsonSchemas 'External starter kit must write JSON Schemas and editor schema mapping.'
Assert-True $externalStarterKitGeneratorUsesCheckedInTemplates 'External starter kit generator must prefer checked-in starter templates for docs, manifests, schemas, and VS Code files.'
Assert-True $externalStarterKitValidatorChecksJsonSchemas 'External starter kit local validator must check JSON Schema files and editor mapping.'
Assert-True $externalStarterKitValidatorChecksEditorSchemaMappings 'External starter kit local validator must check each editor schema URL and fileMatch mapping.'
Assert-True $externalStarterKitWritesVsCodeTasks 'External starter kit generator must write VS Code task runner integration.'
Assert-True $externalStarterKitVsCodeTasksPresent 'Versioned external starter kit must include VS Code tasks routed through h8mod.ps1.'
Assert-True $externalStarterKitVsCodeTasksSupportDisabledAndReplace 'Versioned external starter kit VS Code tasks must expose disabled-node creation and explicit replace actions.'
Assert-True $externalStarterKitVsCodeTasksSupportLocalInstall 'Versioned external starter kit VS Code tasks must expose local discovery install through h8mod.ps1.'
Assert-True $externalStarterKitVsCodeTasksSupportLocalDiagnose 'Versioned external starter kit VS Code tasks must expose local Mods diagnosis through h8mod.ps1.'
Assert-True $externalStarterKitVsCodeTasksSupportDependencies 'Versioned external starter kit VS Code tasks must expose dependency list/add/remove/clear through h8mod.ps1.'
Assert-True $externalStarterKitValidatorChecksVsCodeTasks 'External starter kit local validator must check VS Code task labels, inputs, launcher routing, and command indirection.'
Assert-True $externalStarterKitValidatorChecksSettingsAndLocaleContracts 'External starter kit local validator must reject invalid settings rows and locale string contracts.'
Assert-True $externalStarterKitValidatorChecksAssetManifestContracts 'External starter kit local validator must reject invalid content asset manifest paths, kinds, CRCs, byte sizes, duplicates, caps, and budget drift.'
Assert-True $externalStarterKitValidatorChecksManifestCapabilities 'External starter kit local validator must reject invalid manifest capabilities and over-cap budgets.'
Assert-True $externalStarterKitTemplateVersioned 'External starter kit template must be versioned as files under ModdingSDK/ExternalStarterKit.'
Assert-True $externalStarterKitTemplatePassesLocalValidator 'Versioned external starter kit must pass its local no-Unity validator.'
Assert-True $externalStarterKitRootLauncherPasses 'Versioned external starter kit root launcher must route validate to the local structure validator.'
Assert-True $externalStarterKitTemplateReferenceCsvsMatchSource 'Versioned external starter kit reference CSVs must match Docs/Modding authoritative source CSVs.'
Assert-True $externalStarterKitReviewManifestPasses 'Versioned external starter kit review manifest builder must pass.'
Assert-True $externalStarterKitReviewManifestHashesFiles 'Versioned external starter kit review manifest must hash required authoring/tool files.'
Assert-True $externalStarterKitReviewManifestExcludesReports 'Versioned external starter kit review manifest must exclude Generated/ and Reports/ outputs.'
Assert-True $externalStarterKitReviewManifestHasLimits 'Versioned external starter kit review manifest must enforce source file count and byte limits.'
Assert-True $externalStarterKitReviewManifestRejectsOversizedFile 'Versioned external starter kit review manifest must reject oversized source files.'
Assert-True $externalStarterKitIdentityToolPasses 'Versioned external starter kit identity helper must update both manifests and pass validation on a temp copy.'
Assert-True $externalStarterKitPrepareToolPasses 'Versioned external starter kit prepare tool must set identity and build the review manifest on a temp copy.'
Assert-True $externalStarterKitPrepareToolSupportsExistingManifest 'Versioned external starter kit prepare tool must validate existing manifests and rebuild review reports without requiring identity arguments.'
Assert-True $externalStarterKitTemplateJsonSchemasVersioned 'Versioned external starter kit must include JSON Schema files.'
Assert-True $externalStarterKitTemplateJsonSchemasParse 'Versioned external starter kit JSON Schema files must parse and declare object schemas.'
Assert-True $externalStarterKitEditorSchemaMappingPresent 'Versioned external starter kit must include VS Code JSON schema mapping.'
$saveStatePublicMethods = @()
if ($hectonApiSource -match '(?m)^\s*public\s+static\s+void\s+SetModString\s*\(') { $saveStatePublicMethods += 'SetModString' }
if ($hectonApiSource -match '(?m)^\s*public\s+static\s+string\s+GetModString\s*\(') { $saveStatePublicMethods += 'GetModString' }
$saveDictionaryPrefixMatch = [regex]::Match($modRuntimeStateSource, 'private\s+const\s+string\s+SaveDictionaryPrefix\s*=\s*"([^"]+)";')
Assert-True $saveDictionaryPrefixMatch.Success 'Missing ModSaveStateStore.SaveDictionaryPrefix.'
$saveDictionaryPrefix = $saveDictionaryPrefixMatch.Groups[1].Value
$saveStateStoreRequiresScopedOrEngineOwner =
    $modRuntimeStateSource.Contains('private const string EngineStorageKeyPrefix = "hecton.internal.";') -and
    $modRuntimeStateSource.Contains('private const string EngineStorageOwnerId = "hecton.internal.engine_save_owner";') -and
    $modRuntimeStateSource.Contains('internal static void SetEngineString') -and
    $modRuntimeStateSource.Contains('internal static string GetEngineString') -and
    $modRuntimeStateSource.Contains('RequireActivePersistenceOwnerHash("SetModString")') -and
    $modRuntimeStateSource.Contains('RequireActivePersistenceOwnerHash("GetModString")') -and
    $modRuntimeStateSource.Contains('requires an active mod execution scope. Engine-owned save payloads must use SetEngineString or GetEngineString.') -and
    $modRuntimeStateSource.Contains('Engine-owned mod save payload keys must use the hecton.internal. prefix.') -and
    $modRuntimeStateSource.Contains('EngineStorageOwnerHash == 0u') -and
    $modRuntimeStateSource.Contains('uint legacyOwnerHash = ModCommandDispatcher.ComputeModHash(key);') -and
    $modWorldPersistenceManagerSource.Contains('ModSaveStateStore.SetEngineString(SaveKey') -and
    $modWorldPersistenceManagerSource.Contains('ModSaveStateStore.GetEngineString(SaveKey') -and
    (-not $modRuntimeStateSource.Contains('ResolvePersistenceOwnerHash'))
Assert-True $saveStateStoreRequiresScopedOrEngineOwner 'ModSaveStateStore must require active mod scope or explicit engine-owned save route.'
$modPayloadHeaderMatch = [regex]::Match($saveBinaryStorageSource, 'internal\s+const\s+int\s+ModPayloadHeaderSizeBytes\s*=\s*(\d+);')
Assert-True $modPayloadHeaderMatch.Success 'Missing SaveBinaryStorage.ModPayloadHeaderSizeBytes.'
$modPayloadHeaderSize = [int]$modPayloadHeaderMatch.Groups[1].Value
$protectedBlockMatch = [regex]::Match($saveBinaryPayloadCodecSource, 'internal\s+const\s+int\s+ProtectedLz4BlockSizeBytes\s*=\s*(\d+)\s*\*\s*(\d+);')
Assert-True $protectedBlockMatch.Success 'Missing SaveBinaryPayloadCodec.ProtectedLz4BlockSizeBytes.'
$modPayloadBlockSize = [int]$protectedBlockMatch.Groups[1].Value * [int]$protectedBlockMatch.Groups[2].Value
$modPayloadMaxBytes = $modPayloadBlockSize - $modPayloadHeaderSize
$nativeEventKindNames = Get-EnumNames $eventContractsSource 'HectonNativeEventKind'
$projectedEventKindNames = Get-EnumNames $eventContractsSource 'ModEventKind'
$publicEventMethodPatterns = [ordered]@{
    'Subscribe<TPayload>' = 'public\s+static\s+HectonEventSubscription\s+Subscribe<TPayload>\s*\('
    'SubscribeNative' = 'public\s+static\s+HectonEventSubscription\s+SubscribeNative\s*\('
    'SubscribeProjected' = 'public\s+static\s+HectonEventSubscription\s+SubscribeProjected\s*\('
    'OnPlayerSpawned' = 'public\s+static\s+HectonEventSubscription\s+OnPlayerSpawned\s*\('
    'OnBiomeChanged' = 'public\s+static\s+HectonEventSubscription\s+OnBiomeChanged\s*\('
    'Unsubscribe' = 'public\s+static\s+void\s+Unsubscribe\s*\('
    'Publish<TPayload>' = 'public\s+static\s+void\s+Publish<TPayload>\s*\('
}
$publicEventMethodNames = @()
foreach ($entry in $publicEventMethodPatterns.GetEnumerator()) {
    Assert-True ([regex]::IsMatch($hectonApiSource, $entry.Value)) "Missing public event method pattern: $($entry.Key)"
    $publicEventMethodNames += $entry.Key
}
$engineOwnedPublishPayloads = @(
    'ModEventDto',
    'ModPlayerSpawnedEvent',
    'ModBiomeChangedEvent',
    'ModRaycastResultPayload',
    'ModInteractionRejectedPayload',
    'ModCriticalMemoryEvictionPayload',
    'ModAupResponse',
    'FutureCommandEnvelope',
    'ModCommand',
    'ModAupCommand',
    'ModRenderInstanceCommand'
)
Assert-True ([regex]::IsMatch($hectonEventBusSource, '(?m)^\s*internal\s+static\s+class\s+HectonEventBus\b')) 'HectonEventBus must remain internal; public mods must route through HectonAPI.Events.'
$hectonEventBusPublicStaticMembersForbidden = -not [regex]::IsMatch($hectonEventBusSource, '(?m)^\s*public\s+static\s+')
Assert-True $hectonEventBusPublicStaticMembersForbidden 'HectonEventBus public static members are forbidden; HectonAPI.Events is the only public event route.'
$gameEventPayloadMembersInternalOnly = -not [regex]::IsMatch($hectonGameEventsSource, '(?m)^\s*public\s+')
Assert-True $gameEventPayloadMembersInternalOnly 'HectonGameEvents managed event payload classes and members must remain internal-only.'
Assert-True (-not [regex]::IsMatch($eventContractsSource, '(?m)^\s*public\s+static\s+class\s+HectonModHooks\b')) 'HectonModHooks must not be public; first-party event publication is engine-only.'
Assert-True ([regex]::IsMatch($eventContractsSource, '(?m)^\s*internal\s+static\s+class\s+HectonModHooks\b')) 'HectonModHooks must remain internal first-party publication infrastructure.'
$hectonModHooksPublicStaticMembersForbidden = -not [regex]::IsMatch($eventContractsSource, '(?m)^\s*public\s+static\s+void\s+Publish(?:PlayerSpawned|BiomeChanged)\s*\(')
Assert-True $hectonModHooksPublicStaticMembersForbidden 'HectonModHooks publication methods must remain internal first-party infrastructure.'
Assert-True (-not [regex]::IsMatch($commandDispatcherSource, '(?m)^\s*public\s+interface\s+IModCommandKernel\b')) 'IModCommandKernel must not be public; legacy managed command kernels are engine-owned and quarantined.'
Assert-True ([regex]::IsMatch($commandDispatcherSource, '(?m)^\s*internal\s+interface\s+IModCommandKernel\b')) 'IModCommandKernel must remain internal engine-owned command infrastructure.'
$modCommandDispatcherStart = $commandDispatcherSource.IndexOf('internal static class ModCommandDispatcher')
Assert-True ($modCommandDispatcherStart -ge 0) 'Missing ModCommandDispatcher source body for member visibility audit.'
$modCommandDispatcherBody = $commandDispatcherSource.Substring($modCommandDispatcherStart)
$modCommandDispatcherPublicStaticMembersForbidden = -not [regex]::IsMatch($modCommandDispatcherBody, '(?m)^\s*public\s+static\s+')
Assert-True $modCommandDispatcherPublicStaticMembersForbidden 'ModCommandDispatcher public static members are forbidden; public mods must route through HectonAPI.Commands.'
$nativeBridgePublishLanes = @([regex]::Matches($hectonEventBusSource, 'PublishNativePayload\(HectonNativeEventKind\.([A-Za-z0-9_]+),') | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
$maxDispatchDepthMatch = [regex]::Match($hectonEventBusSource, 'private\s+const\s+int\s+MaxEventDispatchDepth\s*=\s*(\d+);')
Assert-True $maxDispatchDepthMatch.Success 'Missing HectonEventBus.MaxEventDispatchDepth.'
$maxDispatchDepth = [int]$maxDispatchDepthMatch.Groups[1].Value
$watchdogSecondsMatch = [regex]::Match($hectonEventBusSource, 'Stopwatch\.Frequency\s*\*\s*([0-9.]+)d')
Assert-True $watchdogSecondsMatch.Success 'Missing HectonEventBus callback watchdog seconds.'
$callbackWatchdogMilliseconds = [double]$watchdogSecondsMatch.Groups[1].Value * 1000.0
$subscriptionTokenHasIsActive = [regex]::IsMatch($hectonEventBusSource, 'public\s+bool\s+IsActive\s*=>')
$subscriptionTokenHasDispose = [regex]::IsMatch($hectonEventBusSource, 'public\s+void\s+Dispose\s*\(')
$subscriptionTokenConstructorRequiresOwnerScope = [regex]::IsMatch($hectonEventBusSource, 'internal\s+HectonEventSubscription\s*\([^)]*bool\s+requiresOwnerScope\)', 'Singleline')
$subscriptionTokenStoresOwnerScope = $hectonEventBusSource.Contains('private readonly bool _requiresOwnerScope') -and $hectonEventBusSource.Contains('_requiresOwnerScope = requiresOwnerScope;')
$subscriptionTokenDisposeChecksOwnerScope = $hectonEventBusSource.Contains('ThrowIfOwnerScopeMismatch();')
$subscriptionTokenOwnerScopeUsesActiveMod = $hectonEventBusSource.Contains('if (!ModExecutionScope.HasActiveMod)') -and $hectonEventBusSource.Contains('StringComparison.Ordinal')
$eventSubscriptionSources = $hectonEventBusSource + "`n" + $projectionSource
$subscriptionTokenConstructorCallCount = [regex]::Matches($eventSubscriptionSources, 'new\s+HectonEventSubscription\s*\(', 'Singleline').Count
$subscriptionTokenConstructorCallOwnerScopeCount = [regex]::Matches($eventSubscriptionSources, 'new\s+HectonEventSubscription\s*\([^;]*ModExecutionScope\.HasActiveMod[^;]*\)', 'Singleline').Count
$projectedEventBridgeRejectsAnonymousSubscribers =
    $hectonEventBusSource.Contains('RequireModSubscriberScope("HectonEventBus.Subscribe"') -and
    $hectonEventBusSource.Contains('RequireModSubscriberScope("HectonEventBus.SubscribeNative"') -and
    $hectonEventBusSource.Contains('RequireModSubscriberScope("HectonEventBus.SubscribeProjected"') -and
    $projectionSource.Contains('ModEventProjectionBridge.SubscribeProjected requires an active mod execution scope.') -and
    $projectionSource.Contains('Projected mod event subscriptions require a concrete mod subscriber id.') -and
    (-not $projectionSource.Contains('resolvedSubscriberId = "anonymous";'))
Assert-True $projectedEventBridgeRejectsAnonymousSubscribers 'Projected/unmanaged/native mod event subscription bridges must reject anonymous subscribers before token creation.'
$eventChannelsRejectAnonymousSubscribers =
    $hectonEventBusSource.Contains('private static string RequireConcreteSubscriberId') -and
    $hectonEventBusSource.Contains('requires a concrete subscriber id before token creation.') -and
    $hectonEventBusSource.Contains('RequireConcreteSubscriberId("HectonEventBus.Subscribe"') -and
    $hectonEventBusSource.Contains('RequireConcreteSubscriberId("HectonEventBus.UnmanagedEventChannel.Subscribe"') -and
    $hectonEventBusSource.Contains('RequireConcreteSubscriberId("HectonEventBus.NativePayloadChannel.Subscribe"') -and
    $hectonEventBusSource.Contains('RequireConcreteSubscriberId("HectonEventBus.EventChannel.Subscribe"') -and
    (-not $hectonEventBusSource.Contains('? "anonymous" : subscriberId')) -and
    (-not $hectonEventBusSource.Contains('resolvedSubscriberId = string.IsNullOrWhiteSpace(subscriberId)'))
Assert-True $eventChannelsRejectAnonymousSubscribers 'HectonEventBus private event channels must reject anonymous subscribers before token creation.'
$projectionLowCapMatch = [regex]::Match($projectionSource, 'private\s+const\s+int\s+LowTierProjectionCap\s*=\s*(\d+);')
$projectionHighCapMatch = [regex]::Match($projectionSource, 'private\s+const\s+int\s+HighTierProjectionCap\s*=\s*(\d+);')
Assert-True $projectionLowCapMatch.Success 'Missing ModEventProjectionBridge.LowTierProjectionCap.'
Assert-True $projectionHighCapMatch.Success 'Missing ModEventProjectionBridge.HighTierProjectionCap.'
$projectionLowCap = [int]$projectionLowCapMatch.Groups[1].Value
$projectionHighCap = [int]$projectionHighCapMatch.Groups[1].Value
$projectedEventCapUsesSmoothContinuousCurve =
    $projectionSource.Contains('float curve = Smooth01(qualityWeight01);') -and
    $projectionSource.Contains('math.lerp(LowTierProjectionCap, HighTierProjectionCap, curve)') -and
    $projectionSource.Contains('math.clamp(cap, LowTierProjectionCap, HighTierProjectionCap)') -and
    $projectionSource.Contains('return math.isfinite(qualityWeight01) ? math.saturate(qualityWeight01) : 0f;') -and
    $projectionSource.Contains('return t * t * (3f - 2f * t);')
Assert-True $projectedEventCapUsesSmoothContinuousCurve 'Projected event cap must use finite-saturated GlobalQualityWeight01 through Smooth01, then clamp between low/high caps.'
$publicResourceMethodNames = @([regex]::Matches($hectonApiSource, '(?m)^\s*public\s+static\s+bool\s+(TryResolve(?:Prefab|AudioClip|Texture))\s*\(') | ForEach-Object { $_.Groups[1].Value } | Sort-Object)
$resourceKindNames = Get-EnumNames $resourceProxySource 'ModResourceKind'
$resourceCapacityMatch = [regex]::Match($resourceProxySource, 'private\s+const\s+int\s+ResourceCapacity\s*=\s*(\d+);')
Assert-True $resourceCapacityMatch.Success 'Missing ModResourceRegistry.ResourceCapacity.'
$resourceRegistryCapacity = [int]$resourceCapacityMatch.Groups[1].Value
$internalAssetLoaderNames = @([regex]::Matches($hectonApiSource, '(?m)^\s*internal\s+static\s+(?:GameObject|AudioClip|Texture2D)\s+(Load(?:Prefab|AudioClip|Texture))\s*\(') | ForEach-Object { $_.Groups[1].Value } | Sort-Object)
$rawTextureBytesMatch = [regex]::Match($modAssetManagerSource, 'private\s+const\s+long\s+MaxRawTextureBytes\s*=\s*(\d+)L\s*\*\s*(\d+)L\s*\*\s*(\d+)L;')
Assert-True $rawTextureBytesMatch.Success 'Missing ModAssetManager.MaxRawTextureBytes.'
$rawTextureMaxBytes = [long]$rawTextureBytesMatch.Groups[1].Value * [long]$rawTextureBytesMatch.Groups[2].Value * [long]$rawTextureBytesMatch.Groups[3].Value
$rawTextureDimensionMatch = [regex]::Match($modAssetManagerSource, 'private\s+const\s+int\s+MaxRawTextureDimension\s*=\s*(\d+);')
Assert-True $rawTextureDimensionMatch.Success 'Missing ModAssetManager.MaxRawTextureDimension.'
$rawTextureMaxDimension = [int]$rawTextureDimensionMatch.Groups[1].Value
$rawTextureFileGateIndex = $modAssetManagerSource.IndexOf('if (!TryValidateRawTextureFile(filePath))', [System.StringComparison]::Ordinal)
$rawTextureReadAllBytesIndex = $modAssetManagerSource.IndexOf('pngBytes = File.ReadAllBytes(filePath);', [System.StringComparison]::Ordinal)
$rawTextureByteCapEnforcedBeforeRead =
    $modAssetManagerSource.Contains('private static bool TryValidateRawTextureFile') -and
    $modAssetManagerSource.Contains('fileInfo.Length > MaxRawTextureBytes') -and
    $modAssetManagerSource.Contains('exceeded ", MaxRawTextureBytesLabel, " byte cap.') -and
    ($rawTextureFileGateIndex -ge 0) -and
    ($rawTextureReadAllBytesIndex -gt $rawTextureFileGateIndex)
Assert-True $rawTextureByteCapEnforcedBeforeRead 'ModAssetManager must reject oversized raw textures before File.ReadAllBytes.'
$rawTextureReadFailsClosed =
    $modAssetManagerSource.Contains('catch (System.UnauthorizedAccessException exception)') -and
    $modAssetManagerSource.Contains("Rejected inaccessible raw texture '") -and
    $modAssetManagerSource.Contains('catch (IOException exception)') -and
    $modAssetManagerSource.Contains("Failed to read raw texture '") -and
    $modAssetManagerSource.Contains('catch (System.Exception exception)') -and
    $modAssetManagerSource.Contains("Rejected invalid raw texture read '")
Assert-True $rawTextureReadFailsClosed 'ModAssetManager raw texture File.ReadAllBytes must fail closed on access, IO, and invalid read exceptions.'
$assetBundleSuffixFallbackDisabled =
    -not $modAssetManagerSource.Contains('bundle.GetAllAssetNames()') -and
    -not $modAssetManagerSource.Contains('EndsWithAssetPath(')
Assert-True $assetBundleSuffixFallbackDisabled 'ModAssetManager must not use AssetBundle.GetAllAssetNames suffix fallback for legacy asset lookup.'
$assetBundleGetAllAssetNamesForbidden =
    -not $modAssetManagerSource.Contains('GetAllAssetNames')
Assert-True $assetBundleGetAllAssetNamesForbidden 'ModAssetManager must not allocate AssetBundle.GetAllAssetNames arrays in legacy asset lookup.'
$contentMethodPatterns = [ordered]@{
    'InjectBabelEnvelope' = 'public\s+static\s+void\s+InjectBabelEnvelope\s*\('
    'ShowInfo' = 'public\s+static\s+void\s+ShowInfo\s*\('
    'ShowWarning' = 'public\s+static\s+void\s+ShowWarning\s*\('
    'ShowCritical' = 'public\s+static\s+void\s+ShowCritical\s*\('
    'RegisterSettingBool' = 'public\s+static\s+void\s+RegisterSetting\s*\(\s*string\s+modId,\s*string\s+settingName,\s*bool\s+defaultValue'
    'RegisterSettingFloat' = 'public\s+static\s+void\s+RegisterSetting\s*\(\s*string\s+modId,\s*string\s+settingName,\s*float\s+defaultValue'
}
$publicContentMethodNames = @()
foreach ($entry in $contentMethodPatterns.GetEnumerator()) {
    Assert-True ([regex]::IsMatch($hectonApiSource, $entry.Value, 'Singleline')) "Missing public content method pattern: $($entry.Key)"
    $publicContentMethodNames += $entry.Key
}

$forbiddenPublicUnityObjectFacadePatterns = [ordered]@{
    'RegisterCustomItem(ItemData)' = 'public\s+static\s+bool\s+RegisterCustomItem\s*\(\s*ItemData\s+'
    'TryFindItem(out ItemData)' = 'public\s+static\s+bool\s+TryFindItem\s*\([^)]*out\s+ItemData\s+'
    'RegisterRecipe(RecipeData)' = 'public\s+static\s+bool\s+RegisterRecipe\s*\(\s*RecipeData\s+'
    'RegisterBuildable(BuildableData)' = 'public\s+static\s+bool\s+RegisterBuildable\s*\(\s*BuildableData\s+'
    'TryFindBuildable(out BuildableData)' = 'public\s+static\s+bool\s+TryFindBuildable\s*\([^)]*out\s+BuildableData\s+'
}
foreach ($entry in $forbiddenPublicUnityObjectFacadePatterns.GetEnumerator()) {
    Assert-True (-not [regex]::IsMatch($hectonApiSource, $entry.Value, 'Singleline')) "Public HectonAPI exposes Unity object facade signature: $($entry.Key)"
}

$forbiddenPublicAuthorityFacadePatterns = [ordered]@{
    'RegisterRecycleYield' = 'public\s+static\s+bool\s+RegisterRecycleYield\s*\('
    'ProcessRecycle' = 'public\s+static\s+bool\s+ProcessRecycle\s*\('
    'RegisterBiomeMutation' = 'public\s+static\s+bool\s+RegisterBiomeMutation\s*\('
}
foreach ($entry in $forbiddenPublicAuthorityFacadePatterns.GetEnumerator()) {
    Assert-True (-not [regex]::IsMatch($hectonApiSource, $entry.Value, 'Singleline')) "Public HectonAPI exposes direct authority facade method: $($entry.Key)"
}

$forbiddenPublicDiagnosticsFacadePatterns = [ordered]@{
    'Mods surface' = 'public\s+static\s+class\s+Mods\b'
    'GetLoadedMods(List<ModRuntimeInfo>)' = 'public\s+static\s+void\s+GetLoadedMods\s*\(\s*List<ModRuntimeInfo>\s+'
}
foreach ($entry in $forbiddenPublicDiagnosticsFacadePatterns.GetEnumerator()) {
    Assert-True (-not [regex]::IsMatch($hectonApiSource, $entry.Value, 'Singleline')) "Public HectonAPI exposes loader diagnostics facade: $($entry.Key)"
}
Assert-True ([regex]::IsMatch($hectonApiSource, '(?m)^\s*internal\s+static\s+class\s+Mods\b')) 'HectonAPI.Mods diagnostics surface must remain internal.'
Assert-True (-not [regex]::IsMatch($modRuntimeInfoSource, '(?m)^\s*public\s+enum\s+ModLoadStatus\b')) 'ModLoadStatus must not be public mod API.'
Assert-True (-not [regex]::IsMatch($modRuntimeInfoSource, '(?m)^\s*public\s+struct\s+ModRuntimeInfo\b')) 'ModRuntimeInfo contains loader paths and must not be public mod API.'
Assert-True ([regex]::IsMatch($modRuntimeInfoSource, '(?m)^\s*internal\s+enum\s+ModLoadStatus\b')) 'ModLoadStatus must remain internal engine UI diagnostics.'
Assert-True ([regex]::IsMatch($modRuntimeInfoSource, '(?m)^\s*internal\s+struct\s+ModRuntimeInfo\b')) 'ModRuntimeInfo must remain internal engine UI diagnostics.'
$modRuntimeInfoMembersInternalOnly = -not [regex]::IsMatch($modRuntimeInfoSource, '(?m)^\s*public\s+')
Assert-True $modRuntimeInfoMembersInternalOnly 'ModRuntimeInfo members must remain internal-only because the descriptor contains package paths and loader status.'
Assert-True (-not [regex]::IsMatch($modMenuModEntryViewSource, '(?m)^\s*public\s+void\s+Bind\s*\(\s*ModRuntimeInfo\s+info\s*\)')) 'ModMenuModEntryView.Bind must not expose ModRuntimeInfo publicly.'
Assert-True (-not [regex]::IsMatch($modWorldPersistenceManagerSource, '(?m)^\s*public\s+sealed\s+class\s+ModWorldPersistenceManager\b')) 'ModWorldPersistenceManager must not be public mod API.'
Assert-True ([regex]::IsMatch($modWorldPersistenceManagerSource, '(?m)^\s*internal\s+sealed\s+class\s+ModWorldPersistenceManager\b')) 'ModWorldPersistenceManager must remain internal engine save/spawn infrastructure.'
Assert-True (-not [regex]::IsMatch($globalRegistrySource, '(?m)^\s*public\s+static\s+ModWorldPersistenceManager\s+ModWorldPersistence\s*=>')) 'GlobalRegistry.ModWorldPersistence must not expose the concrete mod world persistence service publicly.'
Assert-True ([regex]::IsMatch($globalRegistrySource, '(?m)^\s*internal\s+static\s+ModWorldPersistenceManager\s+ModWorldPersistence\s*=>')) 'GlobalRegistry.ModWorldPersistence must remain an internal engine route.'
Assert-True (-not [regex]::IsMatch($globalRegistrySource, '(?m)^\s*public\s+static\s+void\s+RegisterModWorldPersistenceRuntime\s*\(\s*ModWorldPersistenceManager\s+instance\s*\)')) 'GlobalRegistry.RegisterModWorldPersistenceRuntime must not be a public SDK route.'
Assert-True (-not [regex]::IsMatch($globalRegistrySource, '(?m)^\s*public\s+static\s+void\s+UnregisterModWorldPersistenceRuntime\s*\(\s*ModWorldPersistenceManager\s+instance\s*\)')) 'GlobalRegistry.UnregisterModWorldPersistenceRuntime must not be a public SDK route.'
Assert-True ([regex]::IsMatch($globalRegistrySource, '(?m)^\s*internal\s+static\s+void\s+RegisterModWorldPersistenceRuntime\s*\(\s*ModWorldPersistenceManager\s+instance\s*\)')) 'GlobalRegistry.RegisterModWorldPersistenceRuntime must remain internal engine bootstrap infrastructure.'
Assert-True ([regex]::IsMatch($globalRegistrySource, '(?m)^\s*internal\s+static\s+void\s+UnregisterModWorldPersistenceRuntime\s*\(\s*ModWorldPersistenceManager\s+instance\s*\)')) 'GlobalRegistry.UnregisterModWorldPersistenceRuntime must remain internal engine bootstrap infrastructure.'
Assert-True (-not [regex]::IsMatch($modRegistryEventsSource, '(?m)^\s*public\s+enum\s+ModRegistryEventType\b')) 'ModRegistryEventType must not be public mod API.'
Assert-True (-not [regex]::IsMatch($modRegistryEventsSource, '(?m)^\s*public\s+struct\s+ModRegistryEventPayload\b')) 'ModRegistryEventPayload must not be public mod API.'
Assert-True (-not [regex]::IsMatch($modRegistryEventsSource, '(?m)^\s*public\s+interface\s+IModRegistryEventListener\b')) 'IModRegistryEventListener must not be public mod API.'
Assert-True ([regex]::IsMatch($modRegistryEventsSource, '(?m)^\s*internal\s+enum\s+ModRegistryEventType\b')) 'ModRegistryEventType must remain internal engine invalidation infrastructure.'
Assert-True ([regex]::IsMatch($modRegistryEventsSource, '(?m)^\s*internal\s+struct\s+ModRegistryEventPayload\b')) 'ModRegistryEventPayload must remain internal engine invalidation infrastructure.'
Assert-True ([regex]::IsMatch($modRegistryEventsSource, '(?m)^\s*internal\s+interface\s+IModRegistryEventListener\b')) 'IModRegistryEventListener must remain internal engine invalidation infrastructure.'
Assert-True (-not [regex]::IsMatch($modSettingsRegistrySource, '(?m)^\s*public\s+enum\s+ModSettingKind\b')) 'ModSettingKind must not be public mod API.'
Assert-True (-not [regex]::IsMatch($modSettingsRegistrySource, '(?m)^\s*public\s+struct\s+ModSettingView\b')) 'ModSettingView must not be public mod API.'
Assert-True ([regex]::IsMatch($modSettingsRegistrySource, '(?m)^\s*internal\s+enum\s+ModSettingKind\b')) 'ModSettingKind must remain internal engine UI snapshot infrastructure.'
Assert-True ([regex]::IsMatch($modSettingsRegistrySource, '(?m)^\s*internal\s+struct\s+ModSettingView\b')) 'ModSettingView must remain internal engine UI snapshot infrastructure.'
Assert-True (-not [regex]::IsMatch($modMenuSettingToggleViewSource, '(?m)^\s*public\s+void\s+Bind\s*\(\s*ModSettingView\s+view\s*\)')) 'ModMenuSettingToggleView.Bind must not expose ModSettingView publicly.'
Assert-True (-not [regex]::IsMatch($modMenuSettingSliderViewSource, '(?m)^\s*public\s+void\s+Bind\s*\(\s*ModSettingView\s+view\s*\)')) 'ModMenuSettingSliderView.Bind must not expose ModSettingView publicly.'
Assert-True (-not [regex]::IsMatch($modMenuUiControllerSource, '(?m)^\s*public\s+sealed\s+class\s+ModMenuUIController\s*:\s*MonoBehaviour\s*,\s*IModRegistryEventListener\b')) 'Public ModMenuUIController must not expose internal IModRegistryEventListener in its base list.'
Assert-True (-not [regex]::IsMatch($fabricatorSource, '(?m)^\s*public\s+sealed\s+partial\s+class\s+Fabricator\b[^\n]*IModRegistryEventListener\b')) 'Public Fabricator must not expose internal IModRegistryEventListener in its base list.'
Assert-True ([regex]::IsMatch($modMenuUiControllerSource, '(?m)^\s*private\s+sealed\s+class\s+ModRegistryEventAdapter\s*:\s*IModRegistryEventListener\b')) 'ModMenuUIController must use a private adapter for the internal mod registry listener route.'
Assert-True ([regex]::IsMatch($fabricatorSource, '(?m)^\s*private\s+sealed\s+class\s+ModRegistryEventAdapter\s*:\s*IModRegistryEventListener\b')) 'Fabricator must use a private adapter for the internal mod registry listener route.'
Assert-True ($modMenuUiControllerSource.Contains('ModRegistryEvents.Register(GetModRegistryEventAdapter())')) 'ModMenuUIController must register the private mod registry listener adapter.'
Assert-True ($fabricatorSource.Contains('ModRegistryEvents.Register(GetModRegistryEventAdapter())')) 'Fabricator must register the private mod registry listener adapter.'
Assert-True ($modMenuUiControllerSource.Contains('ModRegistryEvents.Unregister(_modRegistryEventAdapter)')) 'ModMenuUIController must unregister the private mod registry listener adapter.'
Assert-True ($fabricatorSource.Contains('ModRegistryEvents.Unregister(_modRegistryEventAdapter)')) 'Fabricator must unregister the private mod registry listener adapter.'

Assert-True ([regex]::IsMatch($futureCommandSandboxSource, '(?m)^\s*internal\s+static\s+unsafe\s+class\s+FutureCommandSandboxValidator\b')) 'FutureCommandSandboxValidator must remain internal engine/control-plane infrastructure.'
$futureCommandSandboxValidatorStart = $futureCommandSandboxSource.IndexOf('internal static unsafe class FutureCommandSandboxValidator')
Assert-True ($futureCommandSandboxValidatorStart -ge 0) 'Missing FutureCommandSandboxValidator source body for member visibility audit.'
$futureCommandSandboxValidatorBody = $futureCommandSandboxSource.Substring($futureCommandSandboxValidatorStart)
$futureCommandSandboxPublicStaticMembersForbidden = -not [regex]::IsMatch($futureCommandSandboxValidatorBody, '(?m)^\s*public\s+static\s+') -and -not [regex]::IsMatch($futureCommandSandboxSource, '(?m)^\s*public\s+static\s+MockModQueue\s+Wrap\s*\(')
Assert-True $futureCommandSandboxPublicStaticMembersForbidden 'FutureCommandSandboxValidator and MockModQueue control-plane static methods must remain internal.'
$mockModQueueMatch = [regex]::Match($futureCommandSandboxSource, '(?s)internal\s+(?:partial\s+)?(?:ref\s+)?struct\s+MockModQueue(?:\s*:\s*IDisposable)?\s*\{(?<body>.*?)\n\s*\}\s*\n\s*/// <summary>')
Assert-True $mockModQueueMatch.Success 'Missing MockModQueue body for member visibility audit.'
$mockModQueueBody = $mockModQueueMatch.Groups['body'].Value
$mockModQueueMembersInternalOnly =
    $mockModQueueBody.Contains('private NativeQueue<FutureCommandEnvelope> _queue;') -and
    [regex]::IsMatch($mockModQueueBody, '(?m)^\s*internal\s+bool\s+GetIsCreated\s*\(') -and
    [regex]::IsMatch($mockModQueueBody, '(?m)^\s*internal\s+bool\s+Attach\s*\(') -and
    ([regex]::IsMatch($mockModQueueBody, '(?m)^\s*void\s+IDisposable\.Dispose\s*\(') -or [regex]::IsMatch($mockModQueueBody, '(?m)^\s*internal\s+void\s+Dispose\s*\(')) -and
    -not [regex]::IsMatch($mockModQueueBody, '(?m)^\s*public\s+(?:NativeQueue<FutureCommandEnvelope>\s+\w+|bool\s+(?:GetIsCreated|Attach)\s*\(|void\s+Dispose\s*\()')
Assert-True $mockModQueueMembersInternalOnly 'MockModQueue queue handle and instance control-plane members must remain internal/private.'
Assert-True (-not [regex]::IsMatch($futureCommandSandboxSource, '(?m)^\s*public\s+static\s+class\s+FutureCommandSandboxConstants\b')) 'FutureCommandSandboxConstants exposes sandbox control-plane tuning/capacity constants and must not be public SDK API.'
Assert-True ([regex]::IsMatch($futureCommandSandboxSource, '(?m)^\s*internal\s+static\s+class\s+FutureCommandSandboxConstants\b')) 'FutureCommandSandboxConstants must remain internal sandbox control-plane constants.'
Assert-True ([regex]::IsMatch($futureCommandSandboxSource, '(?s)public\s+struct\s+FutureCommandEnvelope\s*\{.*?public\s+const\s+int\s+SizeBytes\s*=\s*FutureCommandSandboxConstants\.EnvelopeSizeBytes\s*;')) 'FutureCommandEnvelope must expose only its 64-byte size contract publicly.'
$forbiddenPublicSandboxControlTypes = @(
    'FutureCommandOpcodeRecord',
    'FutureCommandSandboxTuning',
    'ModderMemoryLease',
    'ModderFrameCounter',
    'ApprovedAssetRecord',
    'ModSandboxRingState',
    'ModSandboxTelemetryEntry',
    'KernelExecutionTelemetryEntry',
    'MockModQueue',
    'MockMaliciousEnvelopeInjectionJob'
)
foreach ($sandboxControlType in $forbiddenPublicSandboxControlTypes) {
    Assert-True (-not [regex]::IsMatch($futureCommandSandboxSource, "(?m)^\s*public\s+(?:partial\s+)?(?:ref\s+)?(?:struct|class)\s+$sandboxControlType\b")) "Future command sandbox control-plane type is public: $sandboxControlType"
    Assert-True ([regex]::IsMatch($futureCommandSandboxSource, "(?m)^\s*internal\s+(?:partial\s+)?(?:ref\s+)?struct\s+$sandboxControlType\b")) "Future command sandbox control-plane type must remain internal: $sandboxControlType"
}
$forbiddenPublicFutureSignalTypes = @(
    'ModSpawnRequestSignal',
    'ModAssetReferenceSignal',
    'SandboxMockAcousticSignal',
    'MockDamageSignal',
    'ModFutureDevNullSignal',
    'SurvivalOverrideSignal',
    'ModHapticPulseSignal',
    'ModSubtitleCueSignal'
)
foreach ($futureSignalType in $forbiddenPublicFutureSignalTypes) {
    Assert-True (-not [regex]::IsMatch($futureCommandSandboxSource, "(?m)^\s*public\s+struct\s+$futureSignalType\s*:\s*ISignal\b")) "Future command output signal type is public: $futureSignalType"
    Assert-True ([regex]::IsMatch($futureCommandSandboxSource, "(?m)^\s*internal\s+struct\s+$futureSignalType\s*:\s*ISignal\b")) "Future command output signal type must remain internal: $futureSignalType"
}

$requiredActiveScopeGuards = @(
    'RequireSubscriberScope("Events.Subscribe", subscriberId)',
    'RequireSubscriberScope("Events.SubscribeNative", subscriberId)',
    'RequireSubscriberScope("Events.SubscribeProjected", subscriberId)',
    'RequireSubscriberScope("Events.OnPlayerSpawned", subscriberId)',
    'RequireSubscriberScope("Events.OnBiomeChanged", subscriberId)',
    'ThrowIfSubscriptionScopeMismatch("Events.Unsubscribe", subscription)',
    'ThrowIfNoActiveMod("Events.Publish")',
    'ThrowIfSignatureMismatch("Commands.RequestFuture", envelope.ModderSignature)',
    'ThrowIfNoActiveMod("Commands.Request")',
    'ThrowIfNoActiveMod("Commands.RequestAup")',
    'ThrowIfNoActiveMod("Commands.RequestRenderInstance")',
    'ThrowIfNoActiveMod("Input.GetButtonMask")',
    'ThrowIfNoActiveMod("Resources.TryResolvePrefab")',
    'ThrowIfNoActiveMod("Resources.TryResolveAudioClip")',
    'ThrowIfNoActiveMod("Resources.TryResolveTexture")',
    'ThrowIfNoActiveMod("Resources.Proxy")',
    'ThrowIfNoActiveMod("Telemetry.Publish")',
    'ThrowIfNoActiveMod("Localization.InjectBabelEnvelope")',
    'ThrowIfNoActiveMod("UI.ShowInfo")',
    'ThrowIfNoActiveMod("UI.ShowWarning")',
    'ThrowIfNoActiveMod("UI.ShowCritical")',
    'ThrowIfScopeMismatch("UI.RegisterSetting", modId)',
    'ThrowIfNoActiveMod("World.IsGameReady")',
    'ThrowIfNoActiveMod("World.TryGetPlayerEntityHash")',
    'ThrowIfNoActiveMod("SaveState.SetModString")',
    'ThrowIfNoActiveMod("SaveState.GetModString")'
)
foreach ($requiredActiveScopeGuard in $requiredActiveScopeGuards) {
    Assert-True ($hectonApiSource.Contains($requiredActiveScopeGuard)) "Missing active ModExecutionScope guard: $requiredActiveScopeGuard"
}

$eventFacadeScopeBeforeEnvelopePatterns = [ordered]@{
    'Events.Subscribe' = 'public\s+static\s+HectonEventSubscription\s+Subscribe<TPayload>\s*\([^)]*\)\s*where\s+TPayload\s*:\s*unmanaged\s*\{(?<body>.*?)\n\s*\}'
    'Events.SubscribeNative' = 'public\s+static\s+HectonEventSubscription\s+SubscribeNative\s*\([^)]*\)\s*\{(?<body>.*?)\n\s*\}'
    'Events.SubscribeProjected' = 'public\s+static\s+HectonEventSubscription\s+SubscribeProjected\s*\([^)]*\)\s*\{(?<body>.*?)\n\s*\}'
    'Events.OnPlayerSpawned' = 'public\s+static\s+HectonEventSubscription\s+OnPlayerSpawned\s*\([^)]*\)\s*\{(?<body>.*?)\n\s*\}'
    'Events.OnBiomeChanged' = 'public\s+static\s+HectonEventSubscription\s+OnBiomeChanged\s*\([^)]*\)\s*\{(?<body>.*?)\n\s*\}'
    'Events.Publish' = 'public\s+static\s+void\s+Publish<TPayload>\s*\([^)]*\)\s*where\s+TPayload\s*:\s*unmanaged\s*\{(?<body>.*?)\n\s*\}'
}
foreach ($entry in $eventFacadeScopeBeforeEnvelopePatterns.GetEnumerator()) {
    $methodMatch = [regex]::Match($hectonApiSource, $entry.Value, 'Singleline')
    Assert-True $methodMatch.Success "Missing public event facade body for ordering check: $($entry.Key)"
    $methodBody = $methodMatch.Groups['body'].Value
    if ($entry.Key -eq 'Events.Publish') {
        Assert-True ([regex]::IsMatch($methodBody, 'ThrowIfNoActiveMod\("Events\.Publish"\);\s*ThrowIfEnvelopeOnly\(\);', 'Singleline')) 'Events.Publish must validate active ModExecutionScope before envelope-only quarantine.'
    }
    else {
        $scopeGuard = 'RequireSubscriberScope("' + $entry.Key + '", subscriberId);'
        $scopeIndex = $methodBody.IndexOf($scopeGuard, [System.StringComparison]::Ordinal)
        $envelopeIndex = $methodBody.IndexOf('ThrowIfEnvelopeOnly();', [System.StringComparison]::Ordinal)
        Assert-True ($scopeIndex -ge 0 -and $envelopeIndex -ge 0 -and $scopeIndex -lt $envelopeIndex) "$($entry.Key) must validate active ModExecutionScope before envelope-only quarantine."
    }
}
Assert-True ($hectonApiSource.Contains('ThrowIfEngineOwnedPublishPayload<TPayload>("Events.Publish")')) 'Events.Publish must reject engine-owned mod payload types before HectonEventBus.Publish.'
Assert-True ([regex]::IsMatch($hectonApiSource, 'private\s+static\s+void\s+ThrowIfEngineOwnedPublishPayload<TPayload>\s*\(', 'Singleline')) 'Missing engine-owned publish payload guard helper.'
foreach ($engineOwnedPublishPayload in $engineOwnedPublishPayloads) {
    Assert-True ($hectonApiSource.Contains("typeof($engineOwnedPublishPayload)")) "Events.Publish engine-owned payload guard missing type: $engineOwnedPublishPayload"
}
Assert-True ($hectonApiSource.Contains('public static IModResourceProxy Proxy => GetProxy();')) 'HectonAPI.Resources.Proxy must route through guarded accessor.'
Assert-True ($hectonApiSource.Contains('public static bool IsGameReady => GetIsGameReady();')) 'HectonAPI.World.IsGameReady must route through guarded accessor.'

Assert-True ($resourceProxySource.Contains('private static void ThrowIfNoActiveMod()')) 'ModResourceProxy must guard direct proxy access with active ModExecutionScope.'
Assert-True ($resourceProxySource.Contains('Resource proxy calls must originate from an active mod execution scope.')) 'ModResourceProxy active-scope rejection message missing.'
foreach ($resourceProxyMethod in @('TryResolvePrefab', 'TryResolveAudioClip', 'TryResolveTexture')) {
    $resourceProxyMethodMatch = [regex]::Match($resourceProxySource, "public\s+bool\s+$resourceProxyMethod\s*\([^)]*\)\s*\{(?<body>.*?)\n\s*\}", 'Singleline')
    Assert-True $resourceProxyMethodMatch.Success "ModResourceProxy.$resourceProxyMethod missing."
    Assert-True ($resourceProxyMethodMatch.Groups['body'].Value.Contains('ThrowIfNoActiveMod();')) "ModResourceProxy.$resourceProxyMethod must guard active ModExecutionScope before resource registration."
    Assert-True ([regex]::IsMatch($resourceProxyMethodMatch.Groups['body'].Value, 'ThrowIfNoActiveMod\(\);\s*if\s*\(\s*ModLoader\.GetIsFutureCommandEnvelopeOnly\(\)\s*\)', 'Singleline')) "ModResourceProxy.$resourceProxyMethod must reject anonymous calls before envelope-only fallback."
}
$resourceRegistryRejectsForgedOwner =
    $resourceProxySource.Contains('Resource registration owner must match the active mod execution scope.') -and
    $resourceProxySource.Contains('string.Equals(modId, ModExecutionScope.CurrentModId, System.StringComparison.Ordinal)')
Assert-True $resourceRegistryRejectsForgedOwner 'ModResourceRegistry.TryRegister must reject forged resource owner ids.'

$futureCommandOpcodeBlockMatch = [regex]::Match($futureCommandSandboxSource, 'public\s+static\s+class\s+FutureCommandOpcodes\s*\{(?<body>.*?)\n\s*\}', 'Singleline')
Assert-True $futureCommandOpcodeBlockMatch.Success 'FutureCommandOpcodes class missing.'
$futureCommandOpcodeMatches = [regex]::Matches($futureCommandOpcodeBlockMatch.Groups['body'].Value, 'public\s+const\s+uint\s+([A-Za-z0-9_]+)\s*=\s*(0x[0-9A-Fa-f]+)u;')
Assert-True ($futureCommandOpcodeMatches.Count -gt 0) 'FutureCommandOpcodes constants missing.'
$futureCommandOpcodeByName = @{}
foreach ($match in $futureCommandOpcodeMatches) {
    $futureCommandOpcodeByName[$match.Groups[1].Value] = $match.Groups[2].Value.ToUpperInvariant()
}
$futureCommandOpcodeHexes = @($futureCommandOpcodeMatches | ForEach-Object { $_.Groups[2].Value.ToUpperInvariant() } | Sort-Object -Unique)
$kernelOptionalPriorityMaxMatch = [regex]::Match($futureCommandSandboxSource, 'KernelOptionalPriorityMax\s*=\s*([0-9.]+)f;')
$kernelSurvivalPriorityMinMatch = [regex]::Match($futureCommandSandboxSource, 'KernelSurvivalPriorityMin\s*=\s*([0-9.]+)f;')
$kernelMaxProfileCommandsPerFrameMatch = [regex]::Match($futureCommandSandboxSource, 'KernelMaxProfileCommandsPerFrame\s*=\s*([0-9]+);')
Assert-True $kernelOptionalPriorityMaxMatch.Success 'FutureCommandSandboxConstants.KernelOptionalPriorityMax missing.'
Assert-True $kernelSurvivalPriorityMinMatch.Success 'FutureCommandSandboxConstants.KernelSurvivalPriorityMin missing.'
Assert-True $kernelMaxProfileCommandsPerFrameMatch.Success 'FutureCommandSandboxConstants.KernelMaxProfileCommandsPerFrame missing.'
$kernelOptionalPriorityMax = [double]::Parse($kernelOptionalPriorityMaxMatch.Groups[1].Value, [System.Globalization.CultureInfo]::InvariantCulture)
$kernelSurvivalPriorityMin = [double]::Parse($kernelSurvivalPriorityMinMatch.Groups[1].Value, [System.Globalization.CultureInfo]::InvariantCulture)
$kernelMaxProfileCommandsPerFrame = [int]::Parse($kernelMaxProfileCommandsPerFrameMatch.Groups[1].Value, [System.Globalization.CultureInfo]::InvariantCulture)
$allowedOpcodeCsvRawHexes = @()
foreach ($line in ($allowedOpcodesCsvText -split "`n")) {
    $trimmed = $line.Trim()
    if ($trimmed.Length -eq 0 -or $trimmed.StartsWith('#')) {
        continue
    }

    $token = @($trimmed -split '[,\s]', 2)[0].ToUpperInvariant()
    Assert-True ($token -match '^0X[0-9A-F]{1,8}$') "Allowed opcode CSV contains non-hex token: $token"
    $allowedOpcodeCsvRawHexes += $token
}

$allowedOpcodeCsvHexes = @($allowedOpcodeCsvRawHexes | Sort-Object -Unique)
Assert-True ($allowedOpcodeCsvRawHexes.Count -eq $allowedOpcodeCsvHexes.Count) 'Allowed opcode CSV contains duplicate opcode hashes.'

$runtimeForbiddenFutureCommandOpcodeNames = @(
    'TriggerSubtitleCue',
    'SurvivalOverride',
    'HapticPulse',
    'SubtitleCue'
)
$expectedKernelTuningProfileNames = @(
    'SurvivalOverride',
    'HapticPulse',
    'SubtitleCue'
)
$reservedFutureCommandOpcodeHexes = @()
foreach ($name in $runtimeForbiddenFutureCommandOpcodeNames) {
    Assert-True ($futureCommandOpcodeByName.ContainsKey($name)) "FutureCommandOpcodes missing reserved command-kernel opcode: $name"
    $reservedFutureCommandOpcodeHexes += $futureCommandOpcodeByName[$name]
    Assert-True (-not [regex]::IsMatch($futureCommandSandboxSource, "Add(?:EmergencyOpcode|OpcodeRecord)\(\s*opcodeRecords,\s*ref\s+state,\s*FutureCommandOpcodes\.$name\s*,")) "Reserved future command opcode is inserted into the runtime allowlist: $name"
}
$reservedFutureCommandOpcodeHexes = @($reservedFutureCommandOpcodeHexes | Sort-Object -Unique)

$runtimeAllowedOpcodeMatches = [regex]::Matches($futureCommandSandboxSource, 'AddEmergencyOpcode\(\s*opcodeRecords,\s*ref\s+state,\s*FutureCommandOpcodes\.([A-Za-z0-9_]+)\s*,\s*1u\s*\)')
Assert-True ($runtimeAllowedOpcodeMatches.Count -gt 0) 'GenerateEmergencyMockOpcodes has no runtime allowed future command opcodes.'
$runtimeAllowedFutureCommandOpcodeNames = @($runtimeAllowedOpcodeMatches | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
$runtimeAllowedFutureCommandOpcodeHexes = @()
foreach ($name in $runtimeAllowedFutureCommandOpcodeNames) {
    Assert-True ($futureCommandOpcodeByName.ContainsKey($name)) "GenerateEmergencyMockOpcodes references missing FutureCommandOpcodes constant: $name"
    Assert-True ($runtimeForbiddenFutureCommandOpcodeNames -notcontains $name) "GenerateEmergencyMockOpcodes references reserved future command opcode: $name"
    $runtimeAllowedFutureCommandOpcodeHexes += $futureCommandOpcodeByName[$name]
}
$runtimeAllowedFutureCommandOpcodeHexes = @($runtimeAllowedFutureCommandOpcodeHexes | Sort-Object -Unique)

$missingRuntimeAllowedOpcodesInCsv = @($runtimeAllowedFutureCommandOpcodeHexes | Where-Object { $allowedOpcodeCsvHexes -notcontains $_ })
$extraAllowedOpcodeCsvHexes = @($allowedOpcodeCsvHexes | Where-Object { $runtimeAllowedFutureCommandOpcodeHexes -notcontains $_ })
$reservedAllowedOpcodeCsvHexes = @($allowedOpcodeCsvHexes | Where-Object { $reservedFutureCommandOpcodeHexes -contains $_ })
Assert-True ($missingRuntimeAllowedOpcodesInCsv.Count -eq 0) "Runtime allowed FutureCommand opcodes missing from allowed_opcodes.csv: $($missingRuntimeAllowedOpcodesInCsv -join ', ')"
Assert-True ($extraAllowedOpcodeCsvHexes.Count -eq 0) "allowed_opcodes.csv contains hashes not present in GenerateEmergencyMockOpcodes: $($extraAllowedOpcodeCsvHexes -join ', ')"
Assert-True ($reservedAllowedOpcodeCsvHexes.Count -eq 0) "allowed_opcodes.csv contains reserved command-kernel hashes: $($reservedAllowedOpcodeCsvHexes -join ', ')"
Assert-True ($futureCommandSandboxSource.Contains('!IsRuntimeAllowedFutureCommandOpcode(opcodeHash)')) 'Future command CSV ingest does not reject unlisted runtime opcodes.'
$editorRuntimeOpcodeTunersRejectReservedSubtitleAliases =
    (-not $modApiSandboxTunerWindowSource.Contains('FutureCommandOpcodes.TriggerSubtitleCue')) -and
    (-not $modApiSandboxTunerWindowSource.Contains('FutureCommandOpcodes.SubtitleCue')) -and
    (-not $modKernelInspectorWindowSource.Contains('FutureCommandOpcodes.TriggerSubtitleCue')) -and
    (-not $modKernelInspectorWindowSource.Contains('FutureCommandOpcodes.SubtitleCue'))
Assert-True $editorRuntimeOpcodeTunersRejectReservedSubtitleAliases 'Editor runtime opcode tools still expose reserved subtitle cue aliases as injectable opcodes.'

$expectedKernelTuningProfileHexes = @()
foreach ($name in $expectedKernelTuningProfileNames) {
    Assert-True ($futureCommandOpcodeByName.ContainsKey($name)) "FutureCommandOpcodes missing expected kernel tuning opcode: $name"
    $expectedKernelTuningProfileHexes += $futureCommandOpcodeByName[$name]
}
$expectedKernelTuningProfileHexes = @($expectedKernelTuningProfileHexes | Sort-Object -Unique)
$kernelTuningCsvRawHexes = @()
foreach ($line in ($kernelTuningProfilesCsvText -split "`n")) {
    $trimmed = $line.Trim()
    if ($trimmed.Length -eq 0 -or $trimmed.StartsWith('#')) {
        continue
    }

    $parts = @($trimmed -split ',')
    Assert-True ($parts.Count -eq 7) "Kernel tuning CSV row must contain exactly 7 columns: $trimmed"

    $token = $parts[0].Trim().ToUpperInvariant()
    if ($token -eq 'OPCODE' -or $token -eq 'OPCODEHASH') {
        continue
    }

    Assert-True ($token -match '^0X[0-9A-F]{1,8}$') "Kernel tuning CSV contains non-hex opcode token: $token"
    Assert-True ($futureCommandOpcodeHexes -contains $token) "Kernel tuning CSV contains hash missing from FutureCommandOpcodes: $token"
    Assert-True (Test-StrictDecimalFloatRange $parts[1] 0.0 1.0) "Kernel tuning CSV priority is outside [0,1] for $token"
    Assert-True (Test-StrictInt32Range $parts[2] 1 $kernelMaxProfileCommandsPerFrame) "Kernel tuning CSV max_per_frame is outside [1,$kernelMaxProfileCommandsPerFrame] for $token"
    Assert-True (Test-StrictUInt32OrHex $parts[3]) "Kernel tuning CSV flags are malformed for $token"
    Assert-True (Test-StrictDecimalFloatRange $parts[4] 1.0 100000.0) "Kernel tuning CSV range is outside [1,100000] for $token"
    Assert-True (Test-StrictDecimalFloatRange $parts[5] 0.01 30.0) "Kernel tuning CSV max_duration is outside [0.01,30] for $token"
    Assert-True (Test-StrictDecimalFloatRange $parts[6] 0.0 ([single]::MaxValue)) "Kernel tuning CSV intensity_scale is negative or malformed for $token"
    $priorityValue = [double]::Parse($parts[1].Trim(), [System.Globalization.CultureInfo]::InvariantCulture)
    if ($token -eq $futureCommandOpcodeByName['SurvivalOverride']) {
        Assert-True ($priorityValue -ge $kernelSurvivalPriorityMin) "SurvivalOverride priority must stay in protected bucket. Value=$priorityValue Minimum=$kernelSurvivalPriorityMin"
    }
    if ($token -eq $futureCommandOpcodeByName['HapticPulse'] -or $token -eq $futureCommandOpcodeByName['SubtitleCue']) {
        Assert-True ($priorityValue -le $kernelOptionalPriorityMax) "Optional command priority must stay in optional shed bucket. Token=$token Value=$priorityValue Maximum=$kernelOptionalPriorityMax"
    }
    $kernelTuningCsvRawHexes += $token
}

$kernelTuningCsvHexes = @($kernelTuningCsvRawHexes | Sort-Object -Unique)
Assert-True ($kernelTuningCsvRawHexes.Count -eq $kernelTuningCsvHexes.Count) 'Kernel tuning CSV contains duplicate opcode hashes.'
$missingKernelTuningCsvHexes = @($expectedKernelTuningProfileHexes | Where-Object { $kernelTuningCsvHexes -notcontains $_ })
$extraKernelTuningCsvHexes = @($kernelTuningCsvHexes | Where-Object { $expectedKernelTuningProfileHexes -notcontains $_ })
Assert-True ($missingKernelTuningCsvHexes.Count -eq 0) "Kernel tuning CSV missing expected command-kernel profiles: $($missingKernelTuningCsvHexes -join ', ')"
Assert-True ($extraKernelTuningCsvHexes.Count -eq 0) "Kernel tuning CSV contains non-kernel profiles: $($extraKernelTuningCsvHexes -join ', ')"

Assert-True ($schema.status -eq 'MOD_API_DEFINED_STATIC_SOURCE_RUNTIME_PENDING') "Unexpected schema status: $($schema.status)"
Assert-True ($schema.globalRules.directSignalBusAccessForMods -eq $false) 'Schema allows direct SignalBus access.'
Assert-True ($schema.globalRules.directNativeContainerAccessForMods -eq $false) 'Schema allows direct native container access.'
Assert-True ($schema.globalRules.directDataVaultAccessForMods -eq $false) 'Schema allows direct DataVault access.'
Assert-True ($schema.globalRules.directSandboxValidatorAccessForMods -eq $false) 'Schema allows direct sandbox validator access.'
Assert-True ($schema.globalRules.directEngineHookPublicationForMods -eq $false) 'Schema allows direct engine hook publication.'
Assert-True ($schema.globalRules.directManagedCommandKernelAccessForMods -eq $false) 'Schema allows direct managed command kernel access.'
Assert-True ($schema.globalRules.directUnityObjectReferencesForMods -eq $false) 'Schema allows direct Unity object references.'
Assert-True ($schema.globalRules.publicCommandIngressRequiresActiveScope -eq $true) 'Schema does not require active scope for public command ingress.'
Assert-True ($schema.globalRules.stringEventNames -eq $false) 'Schema allows string event names.'
Assert-True ($schema.globalRules.jsonHotPathEvents -eq $false) 'Schema allows JSON hot-path events.'
Assert-True ($schema.staticValidation.contractIndex -eq 'Docs/Modding/README.md') 'Schema static validation does not point at Docs/Modding/README.md.'
Assert-True ($schema.staticValidation.changeControlChecklist -eq 'Docs/Modding/Change_Control_Checklist.md') 'Schema static validation does not point at Change_Control_Checklist.md.'
Assert-True ($schema.staticValidation.sampleModSpec -eq 'Docs/Modding/Sample_InfiniteO2_Mod.md') 'Schema static validation does not point at Sample_InfiniteO2_Mod.md.'
Assert-True ($schema.staticValidation.resourceContentAudit -eq 'Docs/Modding/Resource_Content_Audit_Matrix.md') 'Schema static validation does not point at Resource_Content_Audit_Matrix.md.'
Assert-True ($schema.staticValidation.sdkHub -eq 'Assets/_Project/Scripts/Editor/ModdingSDK/ModdingSdkHubWindow.cs') 'Schema static validation does not point at ModdingSdkHubWindow.cs.'
Assert-True ($schema.staticValidation.externalStarterKitContract -eq 'Docs/Modding/External_Starter_Kit_File_Contract.md') 'Schema static validation does not point at External_Starter_Kit_File_Contract.md.'

Assert-True ($uniqueSignals.Count -eq [int]$schema.sourceSignalInventory.uniqueISignalStructCount) "Signal count drift. Source=$($uniqueSignals.Count) Schema=$($schema.sourceSignalInventory.uniqueISignalStructCount)"
Assert-True ($allowedSignals.Count -eq [int]$schema.sourceSignalInventory.modProjectedISignalCount) "Projected count drift. Allowed=$($allowedSignals.Count) Schema=$($schema.sourceSignalInventory.modProjectedISignalCount)"
Assert-True (($uniqueSignals.Count - $allowedSignals.Count) -eq [int]$schema.sourceSignalInventory.deniedByDefaultISignalCount) "Denied count drift. SourceMinusAllowed=$($uniqueSignals.Count - $allowedSignals.Count) Schema=$($schema.sourceSignalInventory.deniedByDefaultISignalCount)"
Assert-True ($opcodeNames.Count -eq [int]$schema.commandAudit.opcodeEnumCountIncludingNone) "Opcode enum count drift. Source=$($opcodeNames.Count) Schema=$($schema.commandAudit.opcodeEnumCountIncludingNone)"
Assert-True ($acceptedOpcodes.Count -eq [int]$schema.commandAudit.acceptedOpcodeCount) "Accepted opcode count drift. Source=$($acceptedOpcodes.Count) Schema=$($schema.commandAudit.acceptedOpcodeCount)"
Assert-True ($targetNames.Count -eq [int]$schema.commandAudit.targetEnumCountIncludingNone) "Target enum count drift. Source=$($targetNames.Count) Schema=$($schema.commandAudit.targetEnumCountIncludingNone)"
Assert-True ($rejectReasonNames.Count -eq [int]$schema.commandAudit.rejectReasonCountIncludingNone) "Reject reason count drift. Source=$($rejectReasonNames.Count) Schema=$($schema.commandAudit.rejectReasonCountIncludingNone)"
Assert-True ($apiSurfaceNames.Count -eq [int]$schema.apiSurfaceAudit.publicNestedSurfaceCount) "API surface count drift. Source=$($apiSurfaceNames.Count) Schema=$($schema.apiSurfaceAudit.publicNestedSurfaceCount)"
Assert-True ($publicApiMethods.Count -eq [int]$schema.apiSurfaceAudit.publicStaticMethodCount) "Public API method count drift. Source=$($publicApiMethods.Count) Schema=$($schema.apiSurfaceAudit.publicStaticMethodCount)"
Assert-True ($publicApiProperties.Count -eq [int]$schema.apiSurfaceAudit.publicStaticPropertyCount) "Public API property count drift. Source=$($publicApiProperties.Count) Schema=$($schema.apiSurfaceAudit.publicStaticPropertyCount)"
Assert-True ($internalApiMethods.Count -eq [int]$schema.apiSurfaceAudit.internalForbiddenMethodCount) "Internal forbidden API method count drift. Source=$($internalApiMethods.Count) Schema=$($schema.apiSurfaceAudit.internalForbiddenMethodCount)"
Assert-True ($modEventDtoSize -eq [int]$schema.payloadLayoutAudit.modEventDtoSizeBytes) "ModEventDto size drift. Source=$modEventDtoSize Schema=$($schema.payloadLayoutAudit.modEventDtoSizeBytes)"
Assert-True ($modEventDtoOffsets.Count -eq [int]$schema.payloadLayoutAudit.modEventDtoFieldOffsetCount) "ModEventDto field offset count drift. Source=$($modEventDtoOffsets.Count) Schema=$($schema.payloadLayoutAudit.modEventDtoFieldOffsetCount)"
Assert-True ($modCommandSize -eq [int]$schema.payloadLayoutAudit.modCommandSizeBytes) "ModCommand size drift. Source=$modCommandSize Schema=$($schema.payloadLayoutAudit.modCommandSizeBytes)"
Assert-True ($modPlayerSpawnedEventSize -eq [int]$schema.payloadLayoutAudit.modPlayerSpawnedEventSizeBytes) "ModPlayerSpawnedEvent size drift. Source=$modPlayerSpawnedEventSize Schema=$($schema.payloadLayoutAudit.modPlayerSpawnedEventSizeBytes)"
Assert-True ($modBiomeChangedEventSize -eq [int]$schema.payloadLayoutAudit.modBiomeChangedEventSizeBytes) "ModBiomeChangedEvent size drift. Source=$modBiomeChangedEventSize Schema=$($schema.payloadLayoutAudit.modBiomeChangedEventSizeBytes)"
Assert-True ($modAupCommandSize -eq [int]$schema.payloadLayoutAudit.modAupCommandSizeBytes) "ModAupCommand size drift. Source=$modAupCommandSize Schema=$($schema.payloadLayoutAudit.modAupCommandSizeBytes)"
Assert-True ($modAupResponseSize -eq [int]$schema.payloadLayoutAudit.modAupResponseSizeBytes) "ModAupResponse size drift. Source=$modAupResponseSize Schema=$($schema.payloadLayoutAudit.modAupResponseSizeBytes)"
Assert-True ($modRenderInstanceCommandSize -eq [int]$schema.payloadLayoutAudit.modRenderInstanceCommandSizeBytes) "ModRenderInstanceCommand size drift. Source=$modRenderInstanceCommandSize Schema=$($schema.payloadLayoutAudit.modRenderInstanceCommandSizeBytes)"
Assert-True ($modRaycastResultPayloadSize -eq [int]$schema.payloadLayoutAudit.modRaycastResultPayloadSizeBytes) "ModRaycastResultPayload size drift. Source=$modRaycastResultPayloadSize Schema=$($schema.payloadLayoutAudit.modRaycastResultPayloadSizeBytes)"
Assert-True ($modInteractionRejectedPayloadSize -eq [int]$schema.payloadLayoutAudit.modInteractionRejectedPayloadSizeBytes) "ModInteractionRejectedPayload size drift. Source=$modInteractionRejectedPayloadSize Schema=$($schema.payloadLayoutAudit.modInteractionRejectedPayloadSizeBytes)"
Assert-True ($modCriticalMemoryEvictionPayloadSize -eq [int]$schema.payloadLayoutAudit.modCriticalMemoryEvictionPayloadSizeBytes) "ModCriticalMemoryEvictionPayload size drift. Source=$modCriticalMemoryEvictionPayloadSize Schema=$($schema.payloadLayoutAudit.modCriticalMemoryEvictionPayloadSizeBytes)"
Assert-True ($interactionEventPayloadSize -eq [int]$schema.payloadLayoutAudit.nativeInteractionEventPayloadSizeBytes) "InteractionEventPayload size drift. Source=$interactionEventPayloadSize Schema=$($schema.payloadLayoutAudit.nativeInteractionEventPayloadSizeBytes)"
Assert-True ($craftingEventPayloadSize -eq [int]$schema.payloadLayoutAudit.nativeCraftingEventPayloadSizeBytes) "CraftingEventPayload size drift. Source=$craftingEventPayloadSize Schema=$($schema.payloadLayoutAudit.nativeCraftingEventPayloadSizeBytes)"
$explicitLayoutSizesByPayload = @{
    ModPlayerSpawnedEvent = $modPlayerSpawnedEventSize
    ModBiomeChangedEvent = $modBiomeChangedEventSize
    ModAupCommand = $modAupCommandSize
    ModAupResponse = $modAupResponseSize
    ModRenderInstanceCommand = $modRenderInstanceCommandSize
    ModRaycastResultPayload = $modRaycastResultPayloadSize
    ModInteractionRejectedPayload = $modInteractionRejectedPayloadSize
    ModCriticalMemoryEvictionPayload = $modCriticalMemoryEvictionPayloadSize
    InteractionEventPayload = $interactionEventPayloadSize
    CraftingEventPayload = $craftingEventPayloadSize
}

foreach ($payloadName in $explicitLayoutSizesByPayload.Keys) {
    $payloadLayout = @($schema.payloadLayouts | Where-Object { $_.payload -eq $payloadName })
    Assert-True ($payloadLayout.Count -eq 1) "Schema payloadLayouts missing or duplicated payload: $payloadName"
    Assert-True ($payloadLayout[0].layout -eq 'Explicit') "Schema payloadLayouts must record explicit layout for ${payloadName}. Actual=$($payloadLayout[0].layout)"
    Assert-True ([int]$payloadLayout[0].sizeBytes -eq [int]$explicitLayoutSizesByPayload[$payloadName]) "Schema payloadLayouts size drift for ${payloadName}. Source=$($explicitLayoutSizesByPayload[$payloadName]) Schema=$($payloadLayout[0].sizeBytes)"
}

Assert-True ($manifestFileName -eq $schema.loaderSaveAudit.manifestFileName) "Manifest file name drift. Source=$manifestFileName Schema=$($schema.loaderSaveAudit.manifestFileName)"
Assert-True ($currentApiVersion -eq [int]$schema.loaderSaveAudit.currentApiVersion) "Current API version drift. Source=$currentApiVersion Schema=$($schema.loaderSaveAudit.currentApiVersion)"
Assert-True ($manifestFields.Count -eq [int]$schema.loaderSaveAudit.manifestFieldCount) "Manifest field count drift. Source=$($manifestFields.Count) Schema=$($schema.loaderSaveAudit.manifestFieldCount)"
Assert-True ($manifestMaxBytes -eq [long]$schema.loaderSaveAudit.manifestMaxBytes) "Manifest byte cap drift. Source=$manifestMaxBytes Schema=$($schema.loaderSaveAudit.manifestMaxBytes)"
Assert-True ([bool]$schema.loaderSaveAudit.manifestByteCapEnforcedBeforeRead) 'Schema loaderSaveAudit must record manifest byte cap before read.'
Assert-True ($manifestDiscoveryMaxCount -eq [int]$schema.loaderSaveAudit.maxDiscoveredManifestCount) "Manifest discovery cap drift. Source=$manifestDiscoveryMaxCount Schema=$($schema.loaderSaveAudit.maxDiscoveredManifestCount)"
Assert-True ([bool]$schema.loaderSaveAudit.manifestDiscoveryUsesBoundedEnumeration) 'Schema loaderSaveAudit must record bounded manifest discovery.'
Assert-True ($builderApiVersion -eq $currentApiVersion) "ModBuilder API version drift. Builder=$builderApiVersion Loader=$currentApiVersion"
Assert-True ($modBuilderManifestFields.Count -eq $manifestFields.Count) "ModBuilder manifest field count drift. Builder=$($modBuilderManifestFields.Count) Loader=$($manifestFields.Count)"
Assert-True ($missingBuilderManifestFields.Count -eq 0) "ModBuilder manifest missing loader-required fields: $($missingBuilderManifestFields -join ', ')"
Assert-True ($extraBuilderManifestFields.Count -eq 0) "ModBuilder manifest has fields absent from loader manifest: $($extraBuilderManifestFields -join ', ')"
Assert-True ($schema.sdkAuthoringAudit.hubWindowPath -eq 'Assets/_Project/Scripts/Editor/ModdingSDK/ModdingSdkHubWindow.cs') 'Schema sdkAuthoringAudit hub path drift.'
Assert-True ($schema.sdkAuthoringAudit.builderWindowPath -eq 'Assets/_Project/Scripts/Editor/ModdingSDK/ModBuilderWindow.cs') 'Schema sdkAuthoringAudit builder path drift.'
Assert-True ($schema.sdkAuthoringAudit.starterWorkbenchWindowPath -eq 'Assets/_Project/Scripts/Editor/ModdingSDK/ExternalStarterKitWorkbenchWindow.cs') 'Schema sdkAuthoringAudit starter workbench path drift.'
Assert-True ($schema.sdkAuthoringAudit.hubMenuPath -eq 'Hecton/Modding/SDK Hub') 'Schema sdkAuthoringAudit hub menu path drift.'
Assert-True ($schema.sdkAuthoringAudit.builderMenuPath -eq 'Hecton/Modding/Internal/Legacy Mod Builder') 'Schema sdkAuthoringAudit builder menu path drift.'
Assert-True ($schema.sdkAuthoringAudit.starterWorkbenchMenuPath -eq 'Hecton/Modding/External Starter Kit Workbench') 'Schema sdkAuthoringAudit starter workbench menu path drift.'
Assert-True ($schema.sdkAuthoringAudit.externalStarterKitContractPath -eq 'Docs/Modding/External_Starter_Kit_File_Contract.md') 'Schema sdkAuthoringAudit external starter kit contract path drift.'
Assert-True ($schema.sdkAuthoringAudit.externalStarterKitOutputPath -eq 'ModdingSDK/ExternalStarterKit') 'Schema sdkAuthoringAudit external starter kit output path drift.'
Assert-True ($schema.sdkAuthoringAudit.externalStarterKitTemplatePath -eq 'ModdingSDK/ExternalStarterKit') 'Schema sdkAuthoringAudit external starter kit template path drift.'
Assert-True ($schema.sdkAuthoringAudit.staticValidatorPath -eq 'Docs/Modding/Validate_Mod_API_Static.ps1') 'Schema sdkAuthoringAudit validator path drift.'
Assert-True ([bool]$schema.sdkAuthoringAudit.hubOpensBuilder) 'Schema sdkAuthoringAudit must record SDK hub builder launch.'
Assert-True ([bool]$schema.sdkAuthoringAudit.hubOpensStarterWorkbench) 'Schema sdkAuthoringAudit must record SDK hub starter workbench launch.'
Assert-True ([bool]$schema.sdkAuthoringAudit.hubPrioritizesExternalStarterKit) 'Schema sdkAuthoringAudit must record external starter kit priority.'
Assert-True ([bool]$schema.sdkAuthoringAudit.hubGatesLegacyBuilder) 'Schema sdkAuthoringAudit must record legacy builder warning gate.'
Assert-True ([bool]$schema.sdkAuthoringAudit.builderMenuIsInternalLegacy) 'Schema sdkAuthoringAudit must record internal legacy builder menu.'
Assert-True ([bool]$schema.sdkAuthoringAudit.hubLinksCoreDocs) 'Schema sdkAuthoringAudit must record SDK hub docs links.'
Assert-True ([bool]$schema.sdkAuthoringAudit.hubRunsStaticValidator) 'Schema sdkAuthoringAudit must record SDK hub static validator action.'
Assert-True ([bool]$schema.sdkAuthoringAudit.hubRunsStaticValidatorAsync) 'Schema sdkAuthoringAudit must record async SDK hub static validator execution.'
Assert-True ([bool]$schema.sdkAuthoringAudit.hubShowsValidatorFailuresAsErrors) 'Schema sdkAuthoringAudit must record SDK hub validator failure error UI.'
Assert-True ([bool]$schema.sdkAuthoringAudit.hubShowsEnvelopeOnlyBoundary) 'Schema sdkAuthoringAudit must record envelope-only boundary visibility.'
Assert-True ([bool]$schema.sdkAuthoringAudit.starterWorkbenchPresent) 'Schema sdkAuthoringAudit must record starter workbench presence.'
Assert-True ([bool]$schema.sdkAuthoringAudit.starterWorkbenchUsesIdentityTool) 'Schema sdkAuthoringAudit must record starter workbench identity tool route.'
Assert-True ([bool]$schema.sdkAuthoringAudit.starterWorkbenchUsesPrepareTool) 'Schema sdkAuthoringAudit must record starter workbench prepare tool route.'
Assert-True ([bool]$schema.sdkAuthoringAudit.starterWorkbenchCanRefreshStarterKit) 'Schema sdkAuthoringAudit must record starter workbench generator reuse.'
Assert-True ([bool]$schema.sdkAuthoringAudit.starterWorkbenchListsOpcodes) 'Schema sdkAuthoringAudit must record starter workbench opcode discovery.'
Assert-True ([bool]$schema.sdkAuthoringAudit.starterWorkbenchShowsReviewSummary) 'Schema sdkAuthoringAudit must record starter workbench review summary.'
Assert-True ([bool]$schema.sdkAuthoringAudit.starterWorkbenchShowsEnvelopeBoundary) 'Schema sdkAuthoringAudit must record starter workbench envelope-only boundary.'
Assert-True ([bool]$schema.sdkAuthoringAudit.starterWorkbenchShowsStarterHealth) 'Schema sdkAuthoringAudit must record starter workbench health panel.'
Assert-True ([bool]$schema.sdkAuthoringAudit.starterWorkbenchShowsCapabilityMatrix) 'Schema sdkAuthoringAudit must record starter workbench capability matrix.'
Assert-True ([bool]$schema.sdkAuthoringAudit.starterWorkbenchRequiredFileListMatchesValidator) 'Schema sdkAuthoringAudit must record starter workbench required-file list parity with validator.'
Assert-True ([bool]$schema.sdkAuthoringAudit.starterWorkbenchRunsStructureValidator) 'Schema sdkAuthoringAudit must record starter workbench structure validator route.'
Assert-True ([bool]$schema.sdkAuthoringAudit.starterWorkbenchLinksCoreDocs) 'Schema sdkAuthoringAudit must record starter workbench core doc links.'
Assert-True ([bool]$schema.sdkAuthoringAudit.starterWorkbenchOpensVsCodeTasks) 'Schema sdkAuthoringAudit must record starter workbench VS Code task/settings file access.'
Assert-True ([bool]$schema.sdkAuthoringAudit.starterWorkbenchRunsToolsAsync) 'Schema sdkAuthoringAudit must record async starter tool execution.'
Assert-True ([bool]$schema.sdkAuthoringAudit.starterWorkbenchShowsToolFailuresAsErrors) 'Schema sdkAuthoringAudit must record starter workbench tool failure error UI.'
Assert-True ([bool]$schema.sdkAuthoringAudit.starterWorkbenchShowsReviewFreshness) 'Schema sdkAuthoringAudit must record starter workbench review freshness.'
Assert-True ([bool]$schema.sdkAuthoringAudit.starterWorkbenchShowsGraphContractPreview) 'Schema sdkAuthoringAudit must record starter workbench graph contract preview.'
Assert-True ([bool]$schema.sdkAuthoringAudit.starterWorkbenchGeneratesGraphNodeSnippet) 'Schema sdkAuthoringAudit must record starter workbench graph node snippet generation.'
Assert-True ([bool]$schema.sdkAuthoringAudit.starterWorkbenchHasGraphOpcodePicker) 'Schema sdkAuthoringAudit must record starter workbench graph opcode picker.'
Assert-True ([bool]$schema.sdkAuthoringAudit.starterWorkbenchEditsGraphNodeParameters) 'Schema sdkAuthoringAudit must record starter workbench graph node parameter editing controls.'
Assert-True ([bool]$schema.sdkAuthoringAudit.starterWorkbenchAppliesGraphNodeSnippet) 'Schema sdkAuthoringAudit must record starter workbench graph node snippet apply route.'
Assert-True ([bool]$schema.sdkAuthoringAudit.starterWorkbenchGeneratesAuthoringSnippets) 'Schema sdkAuthoringAudit must record starter workbench settings/locale snippet generation.'
Assert-True ([bool]$schema.sdkAuthoringAudit.starterWorkbenchAppliesAuthoringSnippets) 'Schema sdkAuthoringAudit must record starter workbench settings/locale snippet apply route.'
Assert-True ([bool]$schema.sdkAuthoringAudit.starterWorkbenchBuildsSubmissionPackage) 'Schema sdkAuthoringAudit must record starter workbench submission package generation.'
Assert-True ([bool]$schema.sdkAuthoringAudit.starterWorkbenchShowsSubmissionPackageStatus) 'Schema sdkAuthoringAudit must record starter workbench submission package status/freshness.'
Assert-True ([bool]$schema.sdkAuthoringAudit.starterWorkbenchShowsSubmissionZipIntegrity) 'Schema sdkAuthoringAudit must record starter workbench submission zip integrity display.'
Assert-True ([bool]$schema.sdkAuthoringAudit.starterWorkbenchUsesCaseExactSubmissionZipIntegrity) 'Schema sdkAuthoringAudit must record starter workbench case-exact submission zip integrity.'
Assert-True ([bool]$schema.sdkAuthoringAudit.starterWorkbenchReportsReservedFolderCaseVariants) 'Schema sdkAuthoringAudit must record starter workbench reserved folder case-variant reporting.'
Assert-True ([bool]$schema.sdkAuthoringAudit.starterWorkbenchRunsDoctor) 'Schema sdkAuthoringAudit must record starter workbench package doctor route.'
Assert-True ([bool]$schema.sdkAuthoringAudit.starterWorkbenchShowsAuthoringDataPreview) 'Schema sdkAuthoringAudit must record starter workbench settings/locale authoring data preview.'
Assert-True ([bool]$schema.sdkAuthoringAudit.starterWorkbenchShowsContentAssetPreview) 'Schema sdkAuthoringAudit must record starter workbench content asset manifest preview.'
Assert-True ([bool]$schema.sdkAuthoringAudit.starterWorkbenchGeneratesAssetEntrySnippet) 'Schema sdkAuthoringAudit must record starter workbench content asset snippet generation.'
Assert-True ([bool]$schema.sdkAuthoringAudit.starterWorkbenchAppliesAssetEntrySnippet) 'Schema sdkAuthoringAudit must record starter workbench content asset snippet apply route.'
Assert-True ([bool]$schema.sdkAuthoringAudit.starterWorkbenchChecksRootLauncher) 'Schema sdkAuthoringAudit must record starter workbench root launcher health/file access.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitGeneratorPresent) 'Schema sdkAuthoringAudit must record external starter kit generator presence.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitWritesRootLauncher) 'Schema sdkAuthoringAudit must record root no-Unity launcher output.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitWritesAuthoringManifest) 'Schema sdkAuthoringAudit must record external starter kit authoring manifest output.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitWritesRuntimeManifest) 'Schema sdkAuthoringAudit must record external starter kit runtime manifest output.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitWritesFolderReadmes) 'Schema sdkAuthoringAudit must record external starter kit folder README output.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitWritesCapabilityGuide) 'Schema sdkAuthoringAudit must record external starter kit capability guide output.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitCopiesOpcodeReferences) 'Schema sdkAuthoringAudit must record copied opcode references.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitDocumentsNoUnityProjectRequirement) 'Schema sdkAuthoringAudit must record no-full-Unity-project guidance.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitDocumentsEnvelopeOnlyBoundary) 'Schema sdkAuthoringAudit must record envelope-only starter kit guidance.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitWritesLocalStructureValidator) 'Schema sdkAuthoringAudit must record local starter kit structure validator output.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitValidatorChecksRequiredFiles) 'Schema sdkAuthoringAudit must record starter validator required-file checks.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitValidatorChecksCapabilityGuide) 'Schema sdkAuthoringAudit must record starter validator capability guide checks.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitValidatorChecksRootLauncher) 'Schema sdkAuthoringAudit must record starter validator root launcher check.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitValidatorChecksEnvelopeOnly) 'Schema sdkAuthoringAudit must record starter validator envelope-only checks.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitValidatorChecksManagedEntryDisabled) 'Schema sdkAuthoringAudit must record starter validator managed-entry rejection.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitValidatorChecksCanonicalIds) 'Schema sdkAuthoringAudit must record starter validator canonical ID checks.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitValidatorChecksManifestIdParity) 'Schema sdkAuthoringAudit must record starter validator manifest ID parity checks.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitValidatorChecksDependencyIds) 'Schema sdkAuthoringAudit must record starter validator dependency ID checks.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitValidatorChecksGraphOpcodes) 'Schema sdkAuthoringAudit must record starter graph opcode allowlist checks.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitValidatorChecksGraphBudget) 'Schema sdkAuthoringAudit must record starter graph budget parity checks.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitValidatorRejectsInvalidGraphOpcode) 'Schema sdkAuthoringAudit must record starter invalid graph opcode rejection.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitWritesReviewManifestBuilder) 'Schema sdkAuthoringAudit must record starter review manifest builder output.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitWritesIdentityTool) 'Schema sdkAuthoringAudit must record starter identity helper output.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitWritesPrepareTool) 'Schema sdkAuthoringAudit must record starter prepare tool output.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitWritesAllowedOpcodeListTool) 'Schema sdkAuthoringAudit must record starter allowed opcode list helper output.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitAllowedOpcodeListToolPasses) 'Schema sdkAuthoringAudit must record starter allowed opcode list helper pass.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitAllowedOpcodeListToolSupportsJson) 'Schema sdkAuthoringAudit must record starter allowed opcode list JSON output.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitWritesGraphNodeSnippetTool) 'Schema sdkAuthoringAudit must record starter graph node snippet helper output.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitGraphNodeSnippetToolPasses) 'Schema sdkAuthoringAudit must record starter graph node snippet helper pass.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitGraphNodeSnippetToolSupportsJson) 'Schema sdkAuthoringAudit must record starter graph node snippet JSON output.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitGraphNodeSnippetSupportsParameters) 'Schema sdkAuthoringAudit must record starter graph node snippet parameter/disabled support.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitRootLauncherSupportsGraphNodeSnippet) 'Schema sdkAuthoringAudit must record starter root launcher graph node snippet route.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitRootLauncherSupportsGraphNodeParameters) 'Schema sdkAuthoringAudit must record starter root launcher graph node parameter route.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitWritesGraphNodeApplyTool) 'Schema sdkAuthoringAudit must record starter graph node apply helper output.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitGraphNodeApplyToolPasses) 'Schema sdkAuthoringAudit must record starter graph node apply helper pass.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitGraphNodeApplyToolRejectsDuplicateWithoutReplace) 'Schema sdkAuthoringAudit must record starter graph node apply duplicate rejection.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitRootLauncherSupportsGraphNodeApply) 'Schema sdkAuthoringAudit must record starter root launcher graph node apply route.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitWritesSettingsRowSnippetTool) 'Schema sdkAuthoringAudit must record starter settings row snippet helper output.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitSettingsRowSnippetToolPasses) 'Schema sdkAuthoringAudit must record starter settings row snippet helper pass.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitSettingsRowSnippetToolSupportsJson) 'Schema sdkAuthoringAudit must record starter settings row snippet JSON output.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitWritesSettingsRowApplyTool) 'Schema sdkAuthoringAudit must record starter settings row apply helper output.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitSettingsRowApplyToolPasses) 'Schema sdkAuthoringAudit must record starter settings row apply helper pass.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitSettingsRowApplyToolRejectsDuplicateWithoutReplace) 'Schema sdkAuthoringAudit must record starter settings row apply duplicate rejection.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitWritesLocaleEntrySnippetTool) 'Schema sdkAuthoringAudit must record starter locale entry snippet helper output.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitLocaleEntrySnippetToolPasses) 'Schema sdkAuthoringAudit must record starter locale entry snippet helper pass.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitLocaleEntrySnippetToolSupportsJson) 'Schema sdkAuthoringAudit must record starter locale entry snippet JSON output.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitWritesLocaleEntryApplyTool) 'Schema sdkAuthoringAudit must record starter locale entry apply helper output.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitLocaleEntryApplyToolPasses) 'Schema sdkAuthoringAudit must record starter locale entry apply helper pass.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitLocaleEntryApplyToolRejectsDuplicateWithoutReplace) 'Schema sdkAuthoringAudit must record starter locale entry apply duplicate rejection.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitWritesAssetEntrySnippetTool) 'Schema sdkAuthoringAudit must record starter asset entry snippet helper output.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitAssetEntrySnippetToolPasses) 'Schema sdkAuthoringAudit must record starter asset entry snippet helper pass.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitAssetEntrySnippetToolSupportsJson) 'Schema sdkAuthoringAudit must record starter asset entry snippet JSON output.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitWritesAssetEntryApplyTool) 'Schema sdkAuthoringAudit must record starter asset entry apply helper output.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitAssetEntryApplyToolPasses) 'Schema sdkAuthoringAudit must record starter asset entry apply helper pass.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitAssetEntryApplyToolRejectsDuplicateWithoutReplace) 'Schema sdkAuthoringAudit must record starter asset entry apply duplicate rejection.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitSnippetPathsRequireStrictJsonRelativePaths) 'Schema sdkAuthoringAudit must record starter snippet strict JSON path gates.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitSnippetApplyUsesBoundedJsonReads) 'Schema sdkAuthoringAudit must record starter snippet apply bounded JSON reads.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitAssetSnippetRejectsNonPortableAssetPaths) 'Schema sdkAuthoringAudit must record starter asset snippet portable path gate.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitGraphNodeSnippetCapsParametersJson) 'Schema sdkAuthoringAudit must record graph node snippet ParametersJson cap.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitValidatorCapsRootJsonReads) 'Schema sdkAuthoringAudit must record structure validator bounded root JSON/text reads.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitValidatorRejectsNonPortableAssetManifestPaths) 'Schema sdkAuthoringAudit must record structure validator portable asset manifest path gate.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitCoreToolsUseBoundedJsonReads) 'Schema sdkAuthoringAudit must record bounded JSON reads in public starter core tools.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitCoreToolsUseStreamingStrictUtf8JsonReads) 'Schema sdkAuthoringAudit must record shared streaming strict UTF-8 JSON/text reads in public starter tools.'
Assert-True ([bool]$schema.sdkAuthoringAudit.starterWorkbenchConfiguresManifestContract) 'Schema sdkAuthoringAudit must record starter Workbench manifest contract configuration.'
Assert-True ([bool]$schema.sdkAuthoringAudit.starterWorkbenchConfiguresDependencies) 'Schema sdkAuthoringAudit must record starter Workbench dependency configuration.'
Assert-True ([bool]$schema.sdkAuthoringAudit.starterWorkbenchUsesBoundedPreviewReads) 'Schema sdkAuthoringAudit must record starter Workbench bounded preview reads.'
Assert-True ([bool]$schema.sdkAuthoringAudit.starterWorkbenchUsesStreamingCappedPreviewReads) 'Schema sdkAuthoringAudit must record starter Workbench streaming capped preview reads.'
Assert-True ([bool]$schema.sdkAuthoringAudit.starterWorkbenchRejectsInvalidUtf8PreviewReads) 'Schema sdkAuthoringAudit must record starter Workbench strict UTF-8 preview reads.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitWritesManifestContractTool) 'Schema sdkAuthoringAudit must record starter manifest contract helper output.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitManifestContractToolPasses) 'Schema sdkAuthoringAudit must record starter manifest contract helper pass.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitManifestContractRejectsUnknownCapability) 'Schema sdkAuthoringAudit must record starter manifest contract unknown capability rejection.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitWritesFirstModTool) 'Schema sdkAuthoringAudit must record starter first playable mod helper output.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitWritesInstallLocalTool) 'Schema sdkAuthoringAudit must record starter local discovery install helper output.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitWritesDiagnoseLocalTool) 'Schema sdkAuthoringAudit must record starter local Mods diagnosis helper output.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitLocalInstallRequiresExactReviewProof) 'Schema sdkAuthoringAudit must record starter local install exact review proof.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitDiagnoseLocalRequiresExactReviewProof) 'Schema sdkAuthoringAudit must record starter local diagnosis exact review proof.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitDiagnoseLocalUsesRecursiveManifestDiscovery) 'Schema sdkAuthoringAudit must record recursive local Mods manifest discovery.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitDiagnoseLocalChecksDependencyGraph) 'Schema sdkAuthoringAudit must record local Mods dependency graph diagnosis.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitWritesDependenciesTool) 'Schema sdkAuthoringAudit must record starter dependency helper output.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitDependencyToolMirrorsBothManifests) 'Schema sdkAuthoringAudit must record starter dependency helper authoring/runtime manifest parity.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitDependenciesToolWritesStrictUtf8NoBom) 'Schema sdkAuthoringAudit must record starter dependency helper strict UTF-8 no-BOM writes.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitWritesDoctorTool) 'Schema sdkAuthoringAudit must record starter package doctor helper output.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitDoctorToolIsReadOnly) 'Schema sdkAuthoringAudit must record starter package doctor read-only contract.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitDoctorVerifiesSubmissionZipContents) 'Schema sdkAuthoringAudit must record starter package doctor submission zip content proof.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitDoctorRejectsUnsafeZipEntries) 'Schema sdkAuthoringAudit must record starter package doctor unsafe zip entry rejection.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitDoctorUsesCaseExactSubmissionZipIntegrity) 'Schema sdkAuthoringAudit must record starter package doctor case-exact submission zip integrity.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitDoctorRejectsReservedTopLevelCaseVariants) 'Schema sdkAuthoringAudit must record starter package doctor reserved top-level case-variant rejection.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitDoctorFailsNonReadyExit) 'Schema sdkAuthoringAudit must record starter package doctor non-ready nonzero exit contract.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitRootLauncherPreservesToolExitCodes) 'Schema sdkAuthoringAudit must record starter root launcher delegated exit-code preservation.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitNestedToolsPreserveChildExitCodes) 'Schema sdkAuthoringAudit must record nested starter child exit-code preservation.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitReviewManifestRejectsCaseFoldSourceDuplicates) 'Schema sdkAuthoringAudit must record starter review manifest case-fold duplicate rejection.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitValidatorRequiresExactPathCasing) 'Schema sdkAuthoringAudit must record starter validator exact path casing.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitRejectsReservedTopLevelCaseVariants) 'Schema sdkAuthoringAudit must record reserved starter top-level case-variant rejection.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitReviewManifestUsesExactReservedOutputFolders) 'Schema sdkAuthoringAudit must record exact reserved output folder handling.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitPackageProofRequiresLowercaseSha256) 'Schema sdkAuthoringAudit must record lowercase SHA-256 package proof.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitRootLauncherSupportsAuthoringSnippets) 'Schema sdkAuthoringAudit must record starter root launcher settings/locale snippet route.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitRootLauncherSupportsAuthoringSnippetApply) 'Schema sdkAuthoringAudit must record starter root launcher settings/locale apply route.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitRootLauncherSupportsAssetEntrySnippet) 'Schema sdkAuthoringAudit must record starter root launcher asset snippet route.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitRootLauncherSupportsAssetEntryApply) 'Schema sdkAuthoringAudit must record starter root launcher asset apply route.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitRootLauncherSupportsManifestContract) 'Schema sdkAuthoringAudit must record starter root launcher manifest contract route.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitRootLauncherSupportsCapabilities) 'Schema sdkAuthoringAudit must record starter root launcher capability guide route.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitRootLauncherUsesStrictCapabilityGuideRead) 'Schema sdkAuthoringAudit must record strict capped capability guide reads in the root launcher.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitHubScriptsRequireCheckedInTemplates) 'Schema sdkAuthoringAudit must record checked-in template enforcement for starter root/tool scripts.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitHubExecutableFallbackBodiesRemoved) 'Schema sdkAuthoringAudit must record removal of embedded executable fallback bodies from the starter Hub.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitWritesSubmissionPackageTool) 'Schema sdkAuthoringAudit must record starter submission package helper output.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitSubmissionPackagePreservesPreviousOutputUntilSuccess) 'Schema sdkAuthoringAudit must record starter submission package atomic handoff.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitSubmissionPackageRefreshesTimestampAfterReplace) 'Schema sdkAuthoringAudit must record starter submission package timestamp freshness after replace.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitSubmissionPackageToolPasses) 'Schema sdkAuthoringAudit must record starter submission package helper pass.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitSubmissionPackageIncludesReviewManifest) 'Schema sdkAuthoringAudit must record starter submission package review manifest inclusion.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitSubmissionPackageUsesCaseExactSourceEntries) 'Schema sdkAuthoringAudit must record starter submission package exact source entry contract.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitRootLauncherSupportsSubmissionPackage) 'Schema sdkAuthoringAudit must record starter root launcher submission package route.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitRootLauncherSupportsFirstMod) 'Schema sdkAuthoringAudit must record starter root launcher first-mod route.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitRootLauncherSupportsInstallLocal) 'Schema sdkAuthoringAudit must record starter root launcher install-local route.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitRootLauncherSupportsDiagnoseLocal) 'Schema sdkAuthoringAudit must record starter root launcher diagnose-local route.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitRootLauncherSupportsDoctor) 'Schema sdkAuthoringAudit must record starter root launcher doctor route.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitRootLauncherSupportsDependencies) 'Schema sdkAuthoringAudit must record starter root launcher dependencies route.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitIdentityToolValidatesCanonicalId) 'Schema sdkAuthoringAudit must record starter identity helper canonical ID validation.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitValidatorChecksSemver) 'Schema sdkAuthoringAudit must record starter semantic version validation.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitValidatorChecksManifestIdentityTextParity) 'Schema sdkAuthoringAudit must record starter identity text parity validation.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitIdentityToolRejectsInvalidVersion) 'Schema sdkAuthoringAudit must record starter invalid version rejection.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitIdentityToolRollsBackBothManifestsOnValidationFailure) 'Schema sdkAuthoringAudit must record starter identity rollback on validation failure.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitToolsAvoidNestedPowerShell) 'Schema sdkAuthoringAudit must record starter tools in-process script chaining.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitToolsUsePortableJoinPath) 'Schema sdkAuthoringAudit must record starter portable path composition.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitWritesJsonSchemas) 'Schema sdkAuthoringAudit must record starter JSON Schema outputs.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitGeneratorUsesCheckedInTemplates) 'Schema sdkAuthoringAudit must record checked-in starter template generator parity.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitValidatorChecksJsonSchemas) 'Schema sdkAuthoringAudit must record starter JSON Schema validator checks.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitValidatorChecksEditorSchemaMappings) 'Schema sdkAuthoringAudit must record exact starter editor schema mapping checks.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitWritesVsCodeTasks) 'Schema sdkAuthoringAudit must record starter VS Code task output.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitVsCodeTasksPresent) 'Schema sdkAuthoringAudit must record versioned starter VS Code task surface.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitVsCodeTasksSupportDisabledAndReplace) 'Schema sdkAuthoringAudit must record starter VS Code disabled-node and replace task surface.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitVsCodeTasksSupportLocalInstall) 'Schema sdkAuthoringAudit must record starter VS Code local discovery install task surface.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitVsCodeTasksSupportLocalDiagnose) 'Schema sdkAuthoringAudit must record starter VS Code local Mods diagnosis task surface.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitVsCodeTasksSupportDependencies) 'Schema sdkAuthoringAudit must record starter VS Code dependency task surface.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitVsCodeTasksSupportDoctor) 'Schema sdkAuthoringAudit must record starter VS Code package doctor task surface.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitValidatorChecksVsCodeTasks) 'Schema sdkAuthoringAudit must record starter VS Code task validation.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitValidatorChecksSettingsAndLocaleContracts) 'Schema sdkAuthoringAudit must record starter settings/locale contract validation.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitValidatorChecksAssetManifestContracts) 'Schema sdkAuthoringAudit must record starter content asset manifest validation.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitValidatorChecksManifestCapabilities) 'Schema sdkAuthoringAudit must record starter manifest capability/budget validation.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitTemplateVersioned) 'Schema sdkAuthoringAudit must record versioned starter kit template.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitTemplatePassesLocalValidator) 'Schema sdkAuthoringAudit must record starter template local validator pass.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitRootLauncherPasses) 'Schema sdkAuthoringAudit must record root launcher validation route pass.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitTemplateReferenceCsvsMatchSource) 'Schema sdkAuthoringAudit must record starter template reference CSV source parity.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitReviewManifestPasses) 'Schema sdkAuthoringAudit must record starter review manifest pass.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitReviewManifestHashesFiles) 'Schema sdkAuthoringAudit must record starter review manifest hash proof.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitReviewManifestIncludesIdentity) 'Schema sdkAuthoringAudit must record starter review manifest identity summary.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitReviewManifestIdentityMatchesRuntimeManifest) 'Schema sdkAuthoringAudit must record starter review manifest identity parity.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitReviewManifestExcludesReports) 'Schema sdkAuthoringAudit must record starter review manifest report/output exclusion.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitReviewManifestHasLimits) 'Schema sdkAuthoringAudit must record starter review manifest count/byte limits.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitReviewManifestRejectsOversizedFile) 'Schema sdkAuthoringAudit must record starter review manifest oversized-file rejection.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitIdentityToolPasses) 'Schema sdkAuthoringAudit must record starter identity helper pass.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitPrepareToolPasses) 'Schema sdkAuthoringAudit must record starter prepare tool pass.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitPrepareToolSupportsExistingManifest) 'Schema sdkAuthoringAudit must record starter prepare existing-manifest rerun proof.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitTemplateJsonSchemasVersioned) 'Schema sdkAuthoringAudit must record versioned starter JSON Schemas.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitTemplateJsonSchemasParse) 'Schema sdkAuthoringAudit must record starter JSON Schema parse proof.'
Assert-True ([bool]$schema.sdkAuthoringAudit.externalStarterKitEditorSchemaMappingPresent) 'Schema sdkAuthoringAudit must record editor JSON schema mapping.'
Assert-True ([int]$schema.sdkAuthoringAudit.maxBundleBuildAssetCount -eq $maxBundleBuildAssetCount) "Schema sdkAuthoringAudit bundle build asset cap drift. Source=$maxBundleBuildAssetCount Schema=$($schema.sdkAuthoringAudit.maxBundleBuildAssetCount)"
Assert-True ([bool]$schema.sdkAuthoringAudit.bundleBuildAssetDiscoveryUsesBoundedEnumeration) 'Schema sdkAuthoringAudit must record bounded bundle asset discovery.'
Assert-True ($modLoaderSource.Contains('TryValidateModIdentifier(manifest.Id')) 'ModLoader does not validate canonical manifest mod IDs before package path/hash use.'
Assert-True ($modLoaderSource.Contains('TryValidateManifestDependencies(manifest.Dependencies')) 'ModLoader does not validate manifest dependency IDs.'
Assert-True ([regex]::IsMatch($modLoaderSource, 'TryValidateEntryAssemblyFileName\s*\(\s*manifest\.EntryAssembly', 'Singleline')) 'ModLoader does not restrict EntryAssembly to a package-local DLL file name.'
Assert-True ($modLoaderSource.Contains('manifest.EntryAssembly = string.Empty')) 'ModLoader must clear invalid EntryAssembly before any path combine or metadata scan.'
Assert-True ($modLoaderSource.Contains('EntryAssembly must be a package-local DLL file name, not a path.')) 'ModLoader missing explicit EntryAssembly path rejection reason.'
Assert-True ($modLoaderSource.Contains('ContainsReservedModIdentifierSegment')) 'ModLoader missing reserved filesystem device segment guard for mod IDs.'
Assert-True ([bool]$schema.loaderSaveAudit.modExecutionScopeRejectsAnonymousOwner) 'Schema loaderSaveAudit must record ModExecutionScope anonymous-owner rejection.'
Assert-True ($modBuilderWindowSource.Contains('string modId = _modId.Trim();')) 'ModBuilderWindow does not use a canonical trimmed mod ID for output paths and manifest.'
Assert-True ($modBuilderWindowSource.Contains('Path.Combine(modsRoot, modId)')) 'ModBuilderWindow output path does not use canonical mod ID.'
Assert-True ($modBuilderWindowSource.Contains('Id = modId')) 'ModBuilderWindow manifest does not use canonical mod ID.'
Assert-True ($modBuilderWindowSource.Contains('Dependency ID is invalid')) 'ModBuilderWindow does not validate dependency IDs.'
Assert-True ($modBuilderWindowSource.Contains('ContainsReservedModIdentifierSegment')) 'ModBuilderWindow missing reserved filesystem device segment guard for mod IDs.'
Assert-True ($modBuilderWindowSource.Contains('RequiredAPIVersion = _requiredApiVersion')) 'ModBuilder manifest does not assign RequiredAPIVersion from validated UI state.'
Assert-True ($modBuilderWindowSource.Contains('ModPriority = _modPriority')) 'ModBuilder manifest does not assign ModPriority from UI state.'
Assert-True ($modRuntimeStateSource.Contains('Mod execution scope requires a non-empty owner id.')) 'ModExecutionScope must reject blank or anonymous active owners.'
Assert-True ($modRuntimeStateSource.Contains('Mod execution scope requires a non-zero owner hash.')) 'ModExecutionScope must reject zero owner hashes.'
Assert-True ($modRuntimeStateSource.Contains('_currentModHash != 0u')) 'ModExecutionScope HasActiveMod must require a non-zero owner hash.'
Assert-True ($modRuntimeStateSource.Contains('CurrentModId => _currentModId ?? string.Empty')) 'ModExecutionScope CurrentModId must not synthesize anonymous owners.'
Assert-True (-not $modRuntimeStateSource.Contains('_currentModId = string.IsNullOrWhiteSpace(modId) ? "anonymous" : modId')) 'ModExecutionScope still synthesizes anonymous active owners.'
Assert-True ($modLoaderSource.Contains('ReservedAssemblyNamePrefix = "Hecton8."')) 'ModLoader missing reserved Hecton8 assembly-name guard.'
Assert-True ([regex]::IsMatch($modLoaderSource, 'TryValidateManagedAssemblyIdentity\s*\(\s*manifest\.EntryAssembly\s*,\s*assemblyPath\s*,\s*managedAssemblyIdentityScanPaths', 'Singleline')) 'ModLoader does not validate manifest managed assembly identity with package DLL scan paths.'
Assert-True ($modLoaderSource.Contains('ResolveManagedAssemblyIdentityScanPaths(') -and $modLoaderSource.Contains('out string managedAssemblyDiscoveryError')) 'ModLoader does not collect package DLL paths for managed assembly identity validation.'
Assert-True ($modLoaderSource.Contains('managedAssemblyIdentityScanPaths.Length > 0')) 'ModLoader does not classify top-level package DLLs as managed-entry candidates.'
Assert-True $managedAssemblyIdentityScanUsesBoundedEnumeration 'ModLoader does not use bounded top-level package DLL scan for identity validation.'
Assert-True ($modLoaderSource.Contains('IsReservedFactoryLoadedFromModsRoot(factory)')) 'ModLoader does not block reserved friend-assembly factories loaded from Mods root.'
Assert-True ($modLoaderSource.Contains('AssemblyName.GetAssemblyName(assemblyPath)')) 'ModLoader does not inspect managed assembly metadata identity.'
Assert-True ($modBuilderWindowSource.Contains('ReservedAssemblyNamePrefix = "Hecton8."')) 'ModBuilderWindow missing reserved Hecton8 assembly-name guard.'
Assert-True ($modBuilderWindowSource.Contains('TryValidateManagedAssemblyIdentity(path')) 'ModBuilderWindow does not validate managed assembly identity before copy.'
Assert-True ($modBuilderWindowSource.Contains('AssemblyName.GetAssemblyName(path)')) 'ModBuilderWindow does not inspect managed assembly metadata identity.'
Assert-True $bundleBuildAssetDiscoveryUsesBoundedEnumeration 'ModBuilderWindow bundle asset discovery drifted from bounded enumeration.'
Assert-True ($modBuilderWindowSource.Contains('RemoveStaleAssemblies(outputDirectory, copiedAssemblies)')) 'ModBuilderWindow does not clean stale managed DLLs from package output.'
Assert-True $builderStaleDllCleanupUsesBoundedEnumeration 'ModBuilderWindow stale managed DLL cleanup must remain bounded.'
Assert-True ([bool]$schema.loaderSaveAudit.managedAssemblyIdentityReservedNamesBlocked) 'Schema loaderSaveAudit must record reserved managed assembly identity blocking.'
Assert-True ([bool]$schema.loaderSaveAudit.managedAssemblyIdentityScansAllPackageDlls) 'Schema loaderSaveAudit must record package DLL identity scanning.'
Assert-True ([bool]$schema.loaderSaveAudit.managedAssemblyIdentityScanUsesBoundedEnumeration) 'Schema loaderSaveAudit must record bounded package DLL identity scanning.'
Assert-True ([int]$schema.loaderSaveAudit.maxTopLevelManagedAssemblyCount -eq $maxTopLevelManagedAssemblyCount) "Top-level managed assembly cap drift. Source=$maxTopLevelManagedAssemblyCount Schema=$($schema.loaderSaveAudit.maxTopLevelManagedAssemblyCount)"
Assert-True ([bool]$schema.loaderSaveAudit.excessTopLevelManagedAssembliesDisablePackage) 'Schema loaderSaveAudit must record managed DLL over-cap package disable.'
Assert-True ([int]$schema.loaderSaveAudit.maxTopLevelBundleCount -eq $maxTopLevelBundleCount) "Top-level bundle cap drift. Source=$maxTopLevelBundleCount Schema=$($schema.loaderSaveAudit.maxTopLevelBundleCount)"
Assert-True ([int]$schema.loaderSaveAudit.maxLocalizationFileCount -eq $maxLocalizationFileCount) "Top-level localization file cap drift. Source=$maxLocalizationFileCount Schema=$($schema.loaderSaveAudit.maxLocalizationFileCount)"
Assert-True ([bool]$schema.loaderSaveAudit.topLevelContentDiscoveryUsesBoundedEnumeration) 'Schema loaderSaveAudit must record bounded top-level content discovery.'
Assert-True ([bool]$schema.loaderSaveAudit.modIdentifierCanonicalForm) 'Schema loaderSaveAudit must record canonical mod identifier validation.'
Assert-True ([bool]$schema.loaderSaveAudit.dependencyIdentifiersValidated) 'Schema loaderSaveAudit must record dependency identifier validation.'
Assert-True ([bool]$schema.loaderSaveAudit.entryAssemblyPathRestrictedToFileName) 'Schema loaderSaveAudit must record EntryAssembly file-name-only restriction.'
Assert-True ([bool]$schema.loaderSaveAudit.modRuntimeInfoMembersInternalOnly) 'Schema loaderSaveAudit must record internal-only ModRuntimeInfo members.'
Assert-True ($modMetadataFields.Count -eq [int]$schema.loaderSaveAudit.modMetadataFieldCount) "ModMetadata field count drift. Source=$($modMetadataFields.Count) Schema=$($schema.loaderSaveAudit.modMetadataFieldCount)"
Assert-True ($modRuntimeInfoFields.Count -eq [int]$schema.loaderSaveAudit.modRuntimeInfoFieldCount) "ModRuntimeInfo field count drift. Source=$($modRuntimeInfoFields.Count) Schema=$($schema.loaderSaveAudit.modRuntimeInfoFieldCount)"
Assert-True ($lifecycleMethods.Count -eq [int]$schema.loaderSaveAudit.lifecycleMethodCount) "IHectonMod lifecycle count drift. Source=$($lifecycleMethods.Count) Schema=$($schema.loaderSaveAudit.lifecycleMethodCount)"
Assert-True ($versionedProperties.Count -eq [int]$schema.loaderSaveAudit.versionedInterfacePropertyCount) "IHectonVersionedMod property count drift. Source=$($versionedProperties.Count) Schema=$($schema.loaderSaveAudit.versionedInterfacePropertyCount)"
Assert-True ($saveStatePublicMethods.Count -eq [int]$schema.loaderSaveAudit.saveStatePublicMethodCount) "SaveState public method count drift. Source=$($saveStatePublicMethods.Count) Schema=$($schema.loaderSaveAudit.saveStatePublicMethodCount)"
Assert-True ($saveDictionaryPrefix -eq $schema.loaderSaveAudit.saveDictionaryPrefix) "Save dictionary prefix drift. Source=$saveDictionaryPrefix Schema=$($schema.loaderSaveAudit.saveDictionaryPrefix)"
Assert-True ([bool]$schema.loaderSaveAudit.saveStateStoreRequiresScopedOrEngineOwner) 'Schema loaderSaveAudit must record SaveState store scoped-or-engine owner proof.'
Assert-True ([string]$schema.loaderSaveAudit.engineSaveStateKeyPrefix -eq 'hecton.internal.') 'Schema loaderSaveAudit must record engine SaveState key prefix.'
Assert-True ([string]$schema.loaderSaveAudit.engineSaveStateOwnerId -eq 'hecton.internal.engine_save_owner') 'Schema loaderSaveAudit must record engine SaveState owner id.'
Assert-True ($modPayloadBlockSize -eq [int]$schema.loaderSaveAudit.modPayloadBlockSizeBytes) "Mod payload block size drift. Source=$modPayloadBlockSize Schema=$($schema.loaderSaveAudit.modPayloadBlockSizeBytes)"
Assert-True ($modPayloadHeaderSize -eq [int]$schema.loaderSaveAudit.modPayloadHeaderSizeBytes) "Mod payload header size drift. Source=$modPayloadHeaderSize Schema=$($schema.loaderSaveAudit.modPayloadHeaderSizeBytes)"
Assert-True ($modPayloadMaxBytes -eq [int]$schema.loaderSaveAudit.modPayloadMaxBytes) "Mod payload max size drift. Source=$modPayloadMaxBytes Schema=$($schema.loaderSaveAudit.modPayloadMaxBytes)"
Assert-True ($publicEventMethodNames.Count -eq [int]$schema.eventSubscriptionAudit.publicEventMethodCount) "Public event method count drift. Source=$($publicEventMethodNames.Count) Schema=$($schema.eventSubscriptionAudit.publicEventMethodCount)"
Assert-True ($nativeEventKindNames.Count -eq [int]$schema.eventSubscriptionAudit.nativeEventKindCount) "Native event kind count drift. Source=$($nativeEventKindNames.Count) Schema=$($schema.eventSubscriptionAudit.nativeEventKindCount)"
Assert-True ($projectedEventKindNames.Count -eq [int]$schema.eventSubscriptionAudit.projectedEventKindCountIncludingNone) "Projected event kind count drift. Source=$($projectedEventKindNames.Count) Schema=$($schema.eventSubscriptionAudit.projectedEventKindCountIncludingNone)"
Assert-True ($nativeBridgePublishLanes.Count -eq [int]$schema.eventSubscriptionAudit.nativeQueueBridgePublishLaneCount) "Native queue bridge publish lane count drift. Source=$($nativeBridgePublishLanes.Count) Schema=$($schema.eventSubscriptionAudit.nativeQueueBridgePublishLaneCount)"
Assert-True ($maxDispatchDepth -eq [int]$schema.eventSubscriptionAudit.maxEventDispatchDepth) "Max event dispatch depth drift. Source=$maxDispatchDepth Schema=$($schema.eventSubscriptionAudit.maxEventDispatchDepth)"
Assert-True ([math]::Abs($callbackWatchdogMilliseconds - [double]$schema.eventSubscriptionAudit.callbackWatchdogMilliseconds) -lt 0.001) "Callback watchdog drift. Source=$callbackWatchdogMilliseconds Schema=$($schema.eventSubscriptionAudit.callbackWatchdogMilliseconds)"
Assert-True ($projectionLowCap -eq [int]$schema.projectionBridge.lowTierProjectionCapPerFrame) "Projected event low cap drift. Source=$projectionLowCap Schema=$($schema.projectionBridge.lowTierProjectionCapPerFrame)"
Assert-True ($projectionHighCap -eq [int]$schema.projectionBridge.highTierProjectionCapPerFrame) "Projected event high cap drift. Source=$projectionHighCap Schema=$($schema.projectionBridge.highTierProjectionCapPerFrame)"
Assert-True ([string]$schema.projectionBridge.projectionCapCurve -like '*smoothstep*') 'Schema projectionBridge must record smoothstep cap curve.'
Assert-True ([string]$schema.projectionBridge.projectionCapFormula -like '*round(lerp(10,50,smoothstep(q)))*') 'Schema projectionBridge must record projected event cap formula.'
Assert-True $subscriptionTokenHasIsActive 'HectonEventSubscription missing IsActive property.'
Assert-True $subscriptionTokenHasDispose 'HectonEventSubscription missing Dispose method.'
Assert-True $subscriptionTokenConstructorRequiresOwnerScope 'HectonEventSubscription constructor must carry owner-scope requirement for mod-owned tokens.'
Assert-True $subscriptionTokenStoresOwnerScope 'HectonEventSubscription must store the owner-scope requirement.'
Assert-True $subscriptionTokenDisposeChecksOwnerScope 'HectonEventSubscription.Dispose must validate owner scope before channel unsubscribe.'
Assert-True $subscriptionTokenOwnerScopeUsesActiveMod 'HectonEventSubscription owner-scope check must require active ModExecutionScope and ordinal owner match.'
Assert-True ($subscriptionTokenConstructorCallCount -eq $subscriptionTokenConstructorCallOwnerScopeCount) "Every HectonEventSubscription constructor call must pass ModExecutionScope.HasActiveMod. Calls=$subscriptionTokenConstructorCallCount OwnerScopeCalls=$subscriptionTokenConstructorCallOwnerScopeCount"
Assert-True ([bool]$schema.eventSubscriptionAudit.projectedEventBridgeRejectsAnonymousSubscribers) 'Schema must record projected event bridge anonymous-subscriber rejection.'
Assert-True ([bool]$schema.eventSubscriptionAudit.eventChannelsRejectAnonymousSubscribers) 'Schema must record private event channel anonymous-subscriber rejection.'
Assert-True ([bool]$schema.eventSubscriptionAudit.publishEngineOwnedPayloadsForbidden) 'Schema must record Events.Publish engine-owned payload rejection.'
Assert-True ([bool]$schema.eventSubscriptionAudit.publicEventFacadesRequireScopeBeforeEnvelopeOnly) 'Schema must record public event facade scope-before-quarantine ordering.'
Assert-True ([bool]$schema.eventSubscriptionAudit.hectonEventBusPublicStaticMembersForbidden) 'Schema must record internal-only HectonEventBus public-static member closure.'
Assert-True ([bool]$schema.eventSubscriptionAudit.hectonModHooksPublicStaticMembersForbidden) 'Schema must record internal-only HectonModHooks publication method closure.'
Assert-True ([bool]$schema.eventSubscriptionAudit.gameEventPayloadMembersInternalOnly) 'Schema must record internal-only HectonGameEvents managed payload members.'
Assert-True ([bool]$schema.eventSubscriptionAudit.nativeBytePayloadLayoutsChecked) 'Schema must record native byte payload layout checking.'
Assert-True ($engineOwnedPublishPayloads.Count -eq [int]$schema.eventSubscriptionAudit.engineOwnedPublishForbiddenPayloadCount) "Engine-owned publish payload count drift. Source=$($engineOwnedPublishPayloads.Count) Schema=$($schema.eventSubscriptionAudit.engineOwnedPublishForbiddenPayloadCount)"

$expectedNativeBytePayloadLayouts = @{
    Interaction = [pscustomobject]@{
        Payload = 'InteractionEventPayload'
        SizeBytes = $interactionEventPayloadSize
        SourceFile = 'Assets/_Project/Scripts/Interaction/InteractionEvents.cs'
    }
    Crafting = [pscustomobject]@{
        Payload = 'CraftingEventPayload'
        SizeBytes = $craftingEventPayloadSize
        SourceFile = 'Assets/_Project/Scripts/CraftingEvents.cs'
    }
}

foreach ($kind in $expectedNativeBytePayloadLayouts.Keys) {
    $expected = $expectedNativeBytePayloadLayouts[$kind]
    $schemaLayout = @($schema.eventSubscriptionAudit.nativeBytePayloadLayouts | Where-Object { $_.kind -eq $kind })
    Assert-True ($schemaLayout.Count -eq 1) "Schema eventSubscriptionAudit.nativeBytePayloadLayouts missing or duplicated kind: $kind"
    Assert-True ($schemaLayout[0].payload -eq $expected.Payload) "Native byte payload name drift for ${kind}. Source=$($expected.Payload) Schema=$($schemaLayout[0].payload)"
    Assert-True ($schemaLayout[0].layout -eq 'Explicit') "Native byte payload layout must be Explicit for ${kind}. Actual=$($schemaLayout[0].layout)"
    Assert-True ([int]$schemaLayout[0].sizeBytes -eq [int]$expected.SizeBytes) "Native byte payload size drift for ${kind}. Source=$($expected.SizeBytes) Schema=$($schemaLayout[0].sizeBytes)"
    Assert-True ($schemaLayout[0].sourceFile -eq $expected.SourceFile) "Native byte payload source file drift for ${kind}. Expected=$($expected.SourceFile) Schema=$($schemaLayout[0].sourceFile)"

    $schemaEvent = @($schema.nativeBytePayloadEvents | Where-Object { $_.kind -eq $kind })
    Assert-True ($schemaEvent.Count -eq 1) "Schema nativeBytePayloadEvents missing or duplicated kind: $kind"
    Assert-True ($schemaEvent[0].sourcePayload -eq $expected.Payload) "nativeBytePayloadEvents sourcePayload drift for ${kind}. Source=$($expected.Payload) Schema=$($schemaEvent[0].sourcePayload)"
    Assert-True ([int]$schemaEvent[0].payloadSizeBytes -eq [int]$expected.SizeBytes) "nativeBytePayloadEvents payload size drift for ${kind}. Source=$($expected.SizeBytes) Schema=$($schemaEvent[0].payloadSizeBytes)"
}
$schemaEngineOwnedPublishPayloads = @($schema.eventSubscriptionAudit.engineOwnedPublishForbiddenPayloads | Sort-Object -Unique)
$missingEngineOwnedPublishPayloads = @($engineOwnedPublishPayloads | Where-Object { $schemaEngineOwnedPublishPayloads -notcontains $_ })
Assert-True ($missingEngineOwnedPublishPayloads.Count -eq 0) "Engine-owned publish payloads missing from schema: $($missingEngineOwnedPublishPayloads -join ', ')"
Assert-True ([bool]$schema.staticValidation.lastStaticValidationSnapshot.publishRejectsEngineOwnedPayloads) 'Static validation snapshot must record Events.Publish engine-owned payload rejection.'
Assert-True ([bool]$schema.staticValidation.lastStaticValidationSnapshot.publicEventFacadesRequireScopeBeforeEnvelopeOnly) 'Static validation snapshot must record public event facade scope-before-quarantine ordering.'
Assert-True ([bool]$schema.staticValidation.lastStaticValidationSnapshot.gameEventPayloadMembersInternalOnly) 'Static validation snapshot must record internal-only HectonGameEvents managed payload members.'
Assert-True ([bool]$schema.staticValidation.lastStaticValidationSnapshot.projectedEventBridgeRejectsAnonymousSubscribers) 'Static validation snapshot must record projected event bridge anonymous-subscriber rejection.'
Assert-True ([bool]$schema.staticValidation.lastStaticValidationSnapshot.eventChannelsRejectAnonymousSubscribers) 'Static validation snapshot must record private event channel anonymous-subscriber rejection.'
Assert-True ([bool]$schema.staticValidation.lastStaticValidationSnapshot.projectedEventCapUsesSmoothContinuousCurve) 'Static validation snapshot must record projected event smooth continuous cap curve.'
Assert-True ($engineOwnedPublishPayloads.Count -eq [int]$schema.staticValidation.lastStaticValidationSnapshot.engineOwnedPublishForbiddenPayloadCount) "Static validation snapshot engine-owned publish payload count drift. Source=$($engineOwnedPublishPayloads.Count) Schema=$($schema.staticValidation.lastStaticValidationSnapshot.engineOwnedPublishForbiddenPayloadCount)"
Assert-True ($publicResourceMethodNames.Count -eq [int]$schema.resourceContentAudit.publicResourceMethodCount) "Public resource method count drift. Source=$($publicResourceMethodNames.Count) Schema=$($schema.resourceContentAudit.publicResourceMethodCount)"
Assert-True ($resourceKindNames.Count -eq [int]$schema.resourceContentAudit.resourceKindCount) "Resource kind count drift. Source=$($resourceKindNames.Count) Schema=$($schema.resourceContentAudit.resourceKindCount)"
Assert-True ($resourceRegistryCapacity -eq [int]$schema.resourceContentAudit.resourceRegistryCapacity) "Resource registry capacity drift. Source=$resourceRegistryCapacity Schema=$($schema.resourceContentAudit.resourceRegistryCapacity)"
Assert-True ($internalAssetLoaderNames.Count -eq [int]$schema.resourceContentAudit.internalForbiddenAssetLoaderCount) "Internal asset loader count drift. Source=$($internalAssetLoaderNames.Count) Schema=$($schema.resourceContentAudit.internalForbiddenAssetLoaderCount)"
Assert-True ($rawTextureMaxBytes -eq [long]$schema.resourceContentAudit.rawTextureMaxBytes) "Raw texture byte cap drift. Source=$rawTextureMaxBytes Schema=$($schema.resourceContentAudit.rawTextureMaxBytes)"
Assert-True ($rawTextureMaxDimension -eq [int]$schema.resourceContentAudit.rawTextureMaxDimension) "Raw texture dimension cap drift. Source=$rawTextureMaxDimension Schema=$($schema.resourceContentAudit.rawTextureMaxDimension)"
Assert-True ([bool]$schema.resourceContentAudit.rawTextureByteCapEnforcedBeforeRead) "Schema resourceContentAudit must record raw texture byte cap before File.ReadAllBytes."
Assert-True ([bool]$schema.resourceContentAudit.rawTextureReadFailsClosed) "Schema resourceContentAudit must record fail-closed raw texture file reads."
Assert-True ([bool]$schema.resourceContentAudit.assetBundleSuffixFallbackDisabled) "Schema resourceContentAudit must record disabled AssetBundle suffix fallback."
Assert-True ([bool]$schema.resourceContentAudit.assetBundleGetAllAssetNamesForbidden) "Schema resourceContentAudit must record forbidden AssetBundle.GetAllAssetNames lookup."
Assert-True ([bool]$schema.resourceContentAudit.resourceRegistryRejectsForgedOwner) "Schema resourceContentAudit must record resource registry owner-id match enforcement."
Assert-True ($publicContentMethodNames.Count -eq [int]$schema.resourceContentAudit.publicContentMethodCount) "Public content method count drift. Source=$($publicContentMethodNames.Count) Schema=$($schema.resourceContentAudit.publicContentMethodCount)"
Assert-True ([int]$schema.sdkAuthoringAudit.maxManagedAssemblyInputCount -eq $maxManagedAssemblyInputCount) "SDK authoring managed assembly input cap drift. Source=$maxManagedAssemblyInputCount Schema=$($schema.sdkAuthoringAudit.maxManagedAssemblyInputCount)"
Assert-True ([int]$schema.sdkAuthoringAudit.maxStaleAssemblyCleanupScanCount -eq $maxStaleAssemblyCleanupScanCount) "SDK authoring stale DLL cleanup scan cap drift. Source=$maxStaleAssemblyCleanupScanCount Schema=$($schema.sdkAuthoringAudit.maxStaleAssemblyCleanupScanCount)"
Assert-True ([bool]$schema.sdkAuthoringAudit.builderManagedAssemblyInputCapMatchesLoader) 'Schema sdkAuthoringAudit must record builder DLL input cap parity with loader.'
Assert-True ([bool]$schema.sdkAuthoringAudit.builderSkipsExpensiveValidationDuringOnGUI) 'Schema sdkAuthoringAudit must record shallow OnGUI validation.'
Assert-True ([bool]$schema.sdkAuthoringAudit.staleDllCleanupUsesBoundedEnumeration) 'Schema sdkAuthoringAudit must record bounded stale DLL cleanup.'
Assert-True ([bool]$schema.sdkAuthoringAudit.builderRejectsDuplicateManagedAssemblyFileNames) 'Schema sdkAuthoringAudit must record duplicate DLL filename rejection.'

$schemaNativeEventKinds = @($schema.eventSubscriptionAudit.nativeEventKinds | Sort-Object -Unique)
$missingNativeKinds = @($nativeEventKindNames | Where-Object { $schemaNativeEventKinds -notcontains $_ })
Assert-True ($missingNativeKinds.Count -eq 0) "Native event kinds missing from schema: $($missingNativeKinds -join ', ')"

$schemaProjectedEventKinds = @($schema.eventSubscriptionAudit.projectedEventKinds | Sort-Object -Unique)
$missingProjectedKinds = @($projectedEventKindNames | Where-Object { $schemaProjectedEventKinds -notcontains $_ })
Assert-True ($missingProjectedKinds.Count -eq 0) "Projected event kinds missing from schema: $($missingProjectedKinds -join ', ')"

$missingBridgeLanesInSchema = @($nativeBridgePublishLanes | Where-Object { $schemaNativeEventKinds -notcontains $_ })
Assert-True ($missingBridgeLanesInSchema.Count -eq 0) "Native bridge publish lanes missing from schema: $($missingBridgeLanesInSchema -join ', ')"

$schemaResourceKinds = @($schema.resourceContentAudit.resourceKinds | Sort-Object -Unique)
$missingResourceKinds = @($resourceKindNames | Where-Object { $schemaResourceKinds -notcontains $_ })
Assert-True ($missingResourceKinds.Count -eq 0) "Resource kinds missing from schema: $($missingResourceKinds -join ', ')"

$schemaResourceMethods = @($schema.resourceContentAudit.publicResourceMethods | Sort-Object -Unique)
$missingResourceMethods = @($publicResourceMethodNames | Where-Object { $schemaResourceMethods -notcontains $_ })
Assert-True ($missingResourceMethods.Count -eq 0) "Public resource methods missing from schema: $($missingResourceMethods -join ', ')"

$missingSchemaOpcodes = @($acceptedOpcodes | Where-Object { $schemaOpcodes -notcontains $_ })
Assert-True ($missingSchemaOpcodes.Count -eq 0) "Accepted source opcodes missing from schema: $($missingSchemaOpcodes -join ', ')"

$extraSchemaOpcodes = @($schemaOpcodes | Where-Object { $acceptedOpcodes -notcontains $_ })
Assert-True ($extraSchemaOpcodes.Count -eq 0) "Schema opcodes missing from source: $($extraSchemaOpcodes -join ', ')"

$missingAllowedInSource = @($allowedSignals | Where-Object { $uniqueSignals -notcontains $_ })
Assert-True ($missingAllowedInSource.Count -eq 0) "Allowed schema lanes not found in GlobalSignalPayloads source files: $($missingAllowedInSource -join ', ')"

$missingBridgeInSchema = @($uniqueBridgeSignals | Where-Object { $allowedSignals -notcontains $_ })
Assert-True ($missingBridgeInSchema.Count -eq 0) "Projection bridge uses SignalBus lanes missing from schema: $($missingBridgeInSchema -join ', ')"

$missingSchemaInBridge = @($allowedSignals | Where-Object { $uniqueBridgeSignals -notcontains $_ })
Assert-True ($missingSchemaInBridge.Count -eq 0) "Schema allows SignalBus lanes not used by projection bridge: $($missingSchemaInBridge -join ', ')"

foreach ($signal in $allowedSignals) {
    $markdownCell = '| `' + $signal + '`'
    Assert-True ($auditText.Contains($markdownCell)) "Allowed signal missing from audit table: $signal"
}

foreach ($signal in $uniqueSignals) {
    Assert-True ($auditText.Contains($signal)) "Signal missing from audit matrix: $signal"
}

foreach ($opcode in $acceptedOpcodes) {
    Assert-True ($commandAuditText.Contains($opcode)) "Opcode missing from command audit matrix: $opcode"
}

foreach ($reason in $rejectReasonNames) {
    Assert-True ($commandAuditText.Contains($reason)) "Reject reason missing from command audit matrix: $reason"
}

foreach ($surface in $apiSurfaceNames) {
    Assert-True ($apiSurfaceAuditText.Contains($surface)) "API surface missing from API surface audit matrix: $surface"
}

foreach ($method in $publicApiMethods) {
    Assert-True ($apiSurfaceAuditText.Contains($method)) "Public API method missing from API surface audit matrix: $method"
}

foreach ($property in $publicApiProperties) {
    Assert-True ($apiSurfaceAuditText.Contains($property)) "Public API property missing from API surface audit matrix: $property"
}

foreach ($method in $internalApiMethods) {
    Assert-True ($apiSurfaceAuditText.Contains($method)) "Internal forbidden API method missing from API surface audit matrix: $method"
}

foreach ($field in $modEventDtoOffsets) {
    Assert-True ($payloadLayoutAuditText.Contains($field.Name)) "ModEventDto field missing from payload audit matrix: $($field.Name)"
    Assert-True ($payloadLayoutAuditText.Contains([string]$field.Offset)) "ModEventDto field offset missing from payload audit matrix: $($field.Name) offset $($field.Offset)"
}

Assert-True ($payloadLayoutAuditText.Contains('ModEventDto')) 'Payload audit missing ModEventDto.'
Assert-True ($payloadLayoutAuditText.Contains('ModCommand')) 'Payload audit missing ModCommand.'
Assert-True ($payloadLayoutAuditText.Contains('ModPlayerSpawnedEvent')) 'Payload audit missing ModPlayerSpawnedEvent.'
Assert-True ($payloadLayoutAuditText.Contains('ModBiomeChangedEvent')) 'Payload audit missing ModBiomeChangedEvent.'
Assert-True ($payloadLayoutAuditText.Contains('ModAupCommand')) 'Payload audit missing ModAupCommand.'
Assert-True ($payloadLayoutAuditText.Contains('ModAupResponse')) 'Payload audit missing ModAupResponse.'
Assert-True ($payloadLayoutAuditText.Contains('ModRenderInstanceCommand')) 'Payload audit missing ModRenderInstanceCommand.'
Assert-True ($payloadLayoutAuditText.Contains('ModRaycastResultPayload')) 'Payload audit missing ModRaycastResultPayload.'
Assert-True ($payloadLayoutAuditText.Contains('ModInteractionRejectedPayload')) 'Payload audit missing ModInteractionRejectedPayload.'
Assert-True ($payloadLayoutAuditText.Contains('ModCriticalMemoryEvictionPayload')) 'Payload audit missing ModCriticalMemoryEvictionPayload.'

foreach ($field in $manifestFields) {
    Assert-True ($loaderSaveAuditText.Contains($field)) "Manifest field missing from loader/save audit matrix: $field"
}

foreach ($field in $modMetadataFields) {
    Assert-True ($loaderSaveAuditText.Contains($field)) "ModMetadata field missing from loader/save audit matrix: $field"
}

foreach ($field in $modRuntimeInfoFields) {
    Assert-True ($loaderSaveAuditText.Contains($field)) "ModRuntimeInfo field missing from loader/save audit matrix: $field"
}

foreach ($method in $lifecycleMethods) {
    Assert-True ($loaderSaveAuditText.Contains($method)) "Lifecycle method missing from loader/save audit matrix: $method"
}

foreach ($method in $saveStatePublicMethods) {
    Assert-True ($loaderSaveAuditText.Contains($method)) "SaveState method missing from loader/save audit matrix: $method"
}

Assert-True ($loaderSaveAuditText.Contains($manifestFileName)) 'Loader/save audit missing manifest file name.'
Assert-True ($loaderSaveAuditText.Contains([string]$currentApiVersion)) 'Loader/save audit missing current API version.'
Assert-True ($loaderSaveAuditText.Contains($saveDictionaryPrefix)) 'Loader/save audit missing save dictionary prefix.'
Assert-True ($loaderSaveAuditText.Contains([string]$modPayloadMaxBytes)) 'Loader/save audit missing mod payload max bytes.'
Assert-True ($loaderSaveAuditText.Contains('Reserved managed assembly identities')) 'Loader/save audit missing reserved managed assembly identity rule.'
Assert-True ($loaderSaveAuditText.Contains('Package DLL identity scan')) 'Loader/save audit missing top-level package DLL identity scan rule.'
Assert-True ($loaderSaveAuditText.Contains('Canonical mod IDs')) 'Loader/save audit missing canonical mod ID rule.'
Assert-True ($loaderSaveAuditText.Contains('EntryAssembly file name only')) 'Loader/save audit missing EntryAssembly filename-only rule.'
Assert-True ($loaderSaveAuditText.Contains('Scope owner proof')) 'Loader/save audit missing ModExecutionScope owner-proof rule.'
Assert-True ($loaderSaveAuditText.Contains('ModRuntimeInfo members internal-only')) 'Loader/save audit missing ModRuntimeInfo internal-only member rule.'
Assert-True ($loaderSaveAuditText.Contains('SaveState store owner proof')) 'Loader/save audit missing SaveState store owner-proof rule.'

foreach ($method in $publicEventMethodNames) {
    Assert-True ($eventSubscriptionAuditText.Contains($method)) "Public event method missing from event subscription audit matrix: $method"
}

foreach ($kind in $nativeEventKindNames) {
    Assert-True ($eventSubscriptionAuditText.Contains($kind)) "Native event kind missing from event subscription audit matrix: $kind"
}

foreach ($payloadName in @('InteractionEventPayload', 'CraftingEventPayload')) {
    Assert-True ($payloadLayoutAuditText.Contains($payloadName)) "Native byte payload missing from payload layout audit matrix: $payloadName"
    Assert-True ($eventSubscriptionAuditText.Contains($payloadName)) "Native byte payload missing from event subscription audit matrix: $payloadName"
}

foreach ($kind in $projectedEventKindNames) {
    Assert-True ($eventSubscriptionAuditText.Contains($kind)) "Projected event kind missing from event subscription audit matrix: $kind"
}

foreach ($lane in $nativeBridgePublishLanes) {
    Assert-True ($eventSubscriptionAuditText.Contains($lane)) "Native bridge lane missing from event subscription audit matrix: $lane"
}

Assert-True ($eventSubscriptionAuditText.Contains([string]$maxDispatchDepth)) 'Event subscription audit missing dispatch depth cap.'
Assert-True ($eventSubscriptionAuditText.Contains('2.0 ms')) 'Event subscription audit missing callback watchdog.'
Assert-True ($eventSubscriptionAuditText.Contains('HectonEventSubscription')) 'Event subscription audit missing subscription token.'
Assert-True ($eventSubscriptionAuditText.Contains('IsActive')) 'Event subscription audit missing IsActive token property.'
Assert-True ($eventSubscriptionAuditText.Contains('HectonGameEvents') -and $eventSubscriptionAuditText.Contains('internal-only')) 'Event subscription audit missing internal-only HectonGameEvents payload rule.'
Assert-True ($eventSubscriptionAuditText.Contains('Dispose validates active mod ownership')) 'Event subscription audit missing direct Dispose ownership guard.'
Assert-True ($eventSubscriptionAuditText.Contains('engine-owned payload types')) 'Event subscription audit missing engine-owned publish payload guard.'
Assert-True ($eventSubscriptionAuditText.Contains('private channel implementations reject anonymous subscribers before token creation')) 'Event subscription audit missing private event channel anonymous-subscriber rejection.'

foreach ($method in $publicResourceMethodNames) {
    Assert-True ($resourceContentAuditText.Contains($method)) "Resource/content audit missing public resource method: $method"
}

foreach ($kind in $resourceKindNames) {
    Assert-True ($resourceContentAuditText.Contains($kind)) "Resource/content audit missing resource kind: $kind"
}

foreach ($method in $publicContentMethodNames) {
    $displayName = $method.Replace('RegisterSettingBool', 'RegisterSetting').Replace('RegisterSettingFloat', 'RegisterSetting')
    Assert-True ($resourceContentAuditText.Contains($displayName)) "Resource/content audit missing public content method: $method"
}

foreach ($method in $internalAssetLoaderNames) {
    Assert-True ($resourceContentAuditText.Contains($method)) "Resource/content audit missing internal asset loader: $method"
}

Assert-True ($resourceContentAuditText.Contains([string]$resourceRegistryCapacity)) 'Resource/content audit missing resource registry capacity.'
Assert-True ($resourceContentAuditText.Contains([string]$rawTextureMaxBytes)) 'Resource/content audit missing raw texture byte cap.'
Assert-True ($resourceContentAuditText.Contains([string]$rawTextureMaxDimension)) 'Resource/content audit missing raw texture dimension cap.'
Assert-True ($resourceContentAuditText.Contains('Raw PNG read failure') -and $resourceContentAuditText.Contains('fail closed')) 'Resource/content audit missing fail-closed raw texture read contract.'
Assert-True ($resourceContentAuditText.Contains('AssetBundle lookup') -and $resourceContentAuditText.Contains('exact asset names only')) 'Resource/content audit missing exact AssetBundle lookup contract.'
Assert-True ($resourceContentAuditText.Contains('No public Unity asset reference returned to mods')) 'Resource/content audit missing Unity object return prohibition.'
Assert-True ($resourceContentAuditText.Contains('No resource registration under a `modId` different from the active `ModExecutionScope`.')) 'Resource/content audit missing forged owner prohibition.'

$requiredChecklistLinks = @(
    'Signal_Audit_Matrix.md',
    'Command_Audit_Matrix.md',
    'API_Surface_Audit_Matrix.md',
    'Payload_Layout_Audit_Matrix.md',
    'Loader_Save_Audit_Matrix.md',
    'Event_Subscription_Audit_Matrix.md',
    'Resource_Content_Audit_Matrix.md',
    'Runtime_Verification_Playbook.md',
    'Sample_InfiniteO2_Mod.md',
    'allowed_opcodes.csv',
    'kernel_tuning_profiles.csv',
    'Validate_Mod_API_Static.ps1'
)

foreach ($requiredLink in $requiredChecklistLinks) {
    Assert-True ($changeControlChecklistText.Contains($requiredLink)) "Change control checklist missing required link: $requiredLink"
}

$requiredChecklistPhrases = @(
    'Add/remove any `ISignal` struct',
    'Add projected `SignalBus<T>` for mods',
    'Add native byte event kind',
    'Add unmanaged public event payload',
    'Add command opcode or target',
    'Change public `HectonAPI` facade',
    'Change resource/content registration',
    'Change payload byte layout',
    'Change loader manifest or lifecycle',
    'scope owner proof',
    'Change managed assembly identity gate',
    'Change mod save payload boundary',
    'Change runtime verification criteria',
    'Change sample mod spec',
    'Change future command envelope allowlist or kernel tuning CSV',
    'Schema-only expansion is invalid',
    'Markdown-only expansion is invalid',
    'Runtime-verified status is invalid'
)

foreach ($requiredPhrase in $requiredChecklistPhrases) {
    Assert-True ($changeControlChecklistText.Contains($requiredPhrase)) "Change control checklist missing required phrase: $requiredPhrase"
}

$requiredIndexLinks = @(
    'Signal_Schema.json',
    'Mod_API_Specification.md',
    'Validate_Mod_API_Static.ps1',
    'Runtime_Verification_Playbook.md',
    'Change_Control_Checklist.md',
    'Sample_InfiniteO2_Mod.md',
    'Signal_Audit_Matrix.md',
    'Command_Audit_Matrix.md',
    'API_Surface_Audit_Matrix.md',
    'Payload_Layout_Audit_Matrix.md',
    'Loader_Save_Audit_Matrix.md',
    'Event_Subscription_Audit_Matrix.md',
    'Resource_Content_Audit_Matrix.md',
    'SDK_Authoring_Interface_Plan.md',
    'SDK_Product_Blueprint.md',
    'External_Starter_Kit_File_Contract.md',
    'allowed_opcodes.csv',
    'kernel_tuning_profiles.csv'
)

foreach ($requiredIndexLink in $requiredIndexLinks) {
    Assert-True ($contractIndexText.Contains($requiredIndexLink)) "Contract index missing required link: $requiredIndexLink"
}

$expectedSchemaRevisionText = 'Schema revision: `' + [string]$schema.schemaRevision + '`'
Assert-True ($contractIndexText.Contains($expectedSchemaRevisionText)) 'Contract index missing schema revision.'
$expectedSpecClosureText = 'Current static closure: `Signal_Schema.json` schema revision `' + [string]$schema.schemaRevision + '`'
$modApiSpecCurrentClosureRevisionMatchesSchema = $specText.Contains($expectedSpecClosureText)
Assert-True $modApiSpecCurrentClosureRevisionMatchesSchema 'Spec current static closure schema revision must match Signal_Schema.json.'
Assert-True ($apiSurfaceAuditText.Contains('scope-less engine payloads use the explicit internal engine route only')) 'API surface audit missing SaveState internal engine route rule.'
Assert-True ($specText.Contains('SaveState store owner proof')) 'Spec missing SaveState store owner proof.'
Assert-True ($contractIndexText.Contains('Hecton/Modding/SDK Hub')) 'Contract index missing SDK Hub entry point.'
Assert-True ($contractIndexText.Contains('Assets/_Project/Scripts/Editor/ModdingSDK/ModdingSdkHubWindow.cs')) 'Contract index missing SDK Hub source path.'
Assert-True ($contractIndexText.Contains('Create External Starter Kit')) 'Contract index missing external starter kit action.'
Assert-True ($contractIndexText.Contains('ModdingSDK/ExternalStarterKit')) 'Contract index missing external starter kit output path.'
Assert-True ($specText.Contains('ModdingSdkHubWindow.cs')) 'Spec missing SDK Hub source path.'
Assert-True ($specText.Contains('external starter kit file contract') -and $specText.Contains('ModdingSDK/ExternalStarterKit')) 'Spec missing external starter kit contract.'
Assert-True ($sdkAuthoringPlanText.Contains('Hecton/Modding/SDK Hub')) 'SDK authoring plan missing current Unity Editor entry point.'
Assert-True ($sdkAuthoringPlanText.Contains('Create External Starter Kit') -and $sdkAuthoringPlanText.Contains('ModdingSDK/ExternalStarterKit')) 'SDK authoring plan missing external starter kit workflow.'
Assert-True ($sdkProductBlueprintText.Contains('HECTON Mod Workbench')) 'SDK product blueprint missing Workbench product surface.'
Assert-True ($sdkProductBlueprintText.Contains('ExternalStarterKit') -and $sdkProductBlueprintText.Contains('no full Unity project')) 'SDK product blueprint missing current external starter kit surface.'
Assert-True ($sdkAuthoringPlanText.Contains('required starter-file health') -and $sdkAuthoringPlanText.Contains('direct structure validation')) 'SDK authoring plan missing starter workbench health/direct validation surface.'
Assert-True ($sdkProductBlueprintText.Contains('current External Starter Kit required-file health') -and $sdkProductBlueprintText.Contains('Tools/validate_structure.ps1')) 'SDK product blueprint missing starter workbench health/validator surface.'
Assert-True ($sdkAuthoringPlanText.Contains('asynchronous starter tool execution') -and $sdkProductBlueprintText.Contains('async starter tool status')) 'SDK authoring docs missing async starter tool surface.'
Assert-True ($sdkAuthoringPlanText.Contains('review manifest freshness') -and $sdkProductBlueprintText.Contains('review freshness status')) 'SDK authoring docs missing review freshness surface.'
Assert-True ($externalStarterKitContractText.Contains('Required Files') -and $externalStarterKitContractText.Contains('mod.h8manifest.json') -and $externalStarterKitContractText.Contains('mod.json')) 'External starter kit contract missing required file layout.'
Assert-True ($externalStarterKitContractText.Contains('Content/Assets/README.md') -and $externalStarterKitContractText.Contains('Tools/create_asset_entry_snippet.ps1') -and $externalStarterKitContractText.Contains('Tools/apply_asset_entry_snippet.ps1')) 'External starter kit contract missing content asset snippet/apply file rules.'
Assert-True ($externalStarterKitContractText.Contains('required-file health') -and $externalStarterKitContractText.Contains('Tools/validate_structure.ps1') -and $externalStarterKitContractText.Contains('opens the core file/API contracts')) 'External starter kit contract missing workbench health/direct validator/doc-link rules.'
Assert-True ($externalStarterKitContractText.Contains('runs starter tools asynchronously')) 'External starter kit contract missing async Workbench tool rule.'
Assert-True ($externalStarterKitContractText.Contains('review manifest freshness') -and $externalStarterKitContractText.Contains('Generated/') -and $externalStarterKitContractText.Contains('Reports/')) 'External starter kit contract missing review freshness rule.'
Assert-True ($externalStarterKitContractText.Contains('validate_structure.ps1') -and $externalStarterKitContractText.Contains('Compatibility.Runtime = envelope-only') -and $externalStarterKitContractText.Contains('empty `EntryAssembly`') -and $externalStarterKitContractText.Contains('matching authoring/runtime IDs')) 'External starter kit contract missing local structure validator rules.'
Assert-True ($externalStarterKitContractText.Contains('build_review_manifest.ps1') -and $externalStarterKitContractText.Contains('Reports/review_manifest.json') -and $externalStarterKitContractText.Contains('SHA-256')) 'External starter kit contract missing review manifest builder rules.'
Assert-True ($externalStarterKitContractText.Contains('set_mod_identity.ps1') -and $externalStarterKitContractText.Contains('canonical mod id') -and $externalStarterKitContractText.Contains('both manifests')) 'External starter kit contract missing identity helper rules.'
Assert-True ($externalStarterKitContractText.Contains('prepare_mod.ps1') -and $externalStarterKitContractText.Contains('one-command') -and $externalStarterKitContractText.Contains('Without identity arguments') -and $externalStarterKitContractText.Contains('pwsh')) 'External starter kit contract missing one-command prepare existing-manifest and cross-platform shell guidance.'
Assert-True ($externalStarterKitContractText.Contains('Schemas/') -and $externalStarterKitContractText.Contains('.vscode/settings.json') -and $externalStarterKitContractText.Contains('JSON Schemas')) 'External starter kit contract missing JSON Schema/editor mapping rules.'
Assert-True ($sdkAuthoringPlanText.Contains('Content Asset Snippet') -and $sdkProductBlueprintText.Contains('content asset snippets') -and $specText.Contains('Content/assets.h8manifest.json')) 'Modding docs missing content asset snippet/apply authoring route.'
Assert-True ($contractIndexText.Contains('Tools/configure_manifest_contract.ps1') -and $specText.Contains('manifest-contract') -and $sdkAuthoringPlanText.Contains('Manifest Contract') -and $sdkProductBlueprintText.Contains('Manifest Contract') -and $externalStarterKitContractText.Contains('Tools/configure_manifest_contract.ps1')) 'Modding docs missing manifest contract capability/budget authoring route.'
$expectedSourceSignalText = 'Source `ISignal` structs: `' + [string]$schema.sourceSignalInventory.uniqueISignalStructCount + '`'
Assert-True ($contractIndexText.Contains($expectedSourceSignalText)) 'Contract index missing current signal count.'
$expectedDeniedSignalText = 'Denied-by-default `ISignal` structs: `' + [string]$schema.sourceSignalInventory.deniedByDefaultISignalCount + '`'
Assert-True ($contractIndexText.Contains($expectedDeniedSignalText)) 'Contract index missing denied signal count.'
Assert-True ($contractIndexText.Contains('Runtime proof: `PENDING`')) 'Contract index missing runtime proof boundary.'

$lastStaticValidation = $schema.staticValidation.lastStaticValidationSnapshot
Assert-True ($null -ne $lastStaticValidation) "Schema lastStaticValidationSnapshot missing."
Assert-True ([string]$lastStaticValidation.runtimeProof -eq "PENDING_VERIFICATION") "Schema lastStaticValidationSnapshot must not imply runtime proof."
Assert-True ([int]$lastStaticValidation.sourceSignals -eq $uniqueSignals.Count) "Schema lastStaticValidationSnapshot sourceSignals drift. Source=$($uniqueSignals.Count) SchemaLastKnown=$($lastStaticValidation.sourceSignals)"
Assert-True ([int]$lastStaticValidation.allowedProjectedSignals -eq $allowedSignals.Count) "Schema lastStaticValidationSnapshot allowedProjectedSignals drift. Source=$($allowedSignals.Count) SchemaLastKnown=$($lastStaticValidation.allowedProjectedSignals)"
Assert-True ([int]$lastStaticValidation.deniedByDefaultSignals -eq [int]$schema.sourceSignalInventory.deniedByDefaultISignalCount) "Schema lastStaticValidationSnapshot deniedByDefaultSignals drift. SchemaInventory=$($schema.sourceSignalInventory.deniedByDefaultISignalCount) SchemaLastKnown=$($lastStaticValidation.deniedByDefaultSignals)"
Assert-True ($lastStaticValidation.futureCommandSandboxValidatorPublic -eq $false) "Schema lastStaticValidationSnapshot must record internal FutureCommandSandboxValidator."
Assert-True ($lastStaticValidation.futureCommandSandboxPublicStaticMembersForbidden -eq $true) "Schema lastStaticValidationSnapshot must record internal-only sandbox control-plane static methods."
Assert-True ($lastStaticValidation.mockModQueueMembersInternalOnly -eq $true) "Schema lastStaticValidationSnapshot must record internal-only MockModQueue queue handle and instance members."
Assert-True ($lastStaticValidation.futureCommandSandboxConstantsPublic -eq $false) "Schema lastStaticValidationSnapshot must record internal FutureCommandSandboxConstants."
Assert-True ($lastStaticValidation.futureCommandEnvelopeExposesSizeBytes -eq $true) "Schema lastStaticValidationSnapshot must record public FutureCommandEnvelope.SizeBytes."
Assert-True ($lastStaticValidation.hectonModHooksPublic -eq $false) "Schema lastStaticValidationSnapshot must record internal HectonModHooks."
Assert-True ($lastStaticValidation.hectonModHooksPublicStaticMembersForbidden -eq $true) "Schema lastStaticValidationSnapshot must record internal-only HectonModHooks publication methods."
Assert-True ($lastStaticValidation.modCommandKernelInterfacePublic -eq $false) "Schema lastStaticValidationSnapshot must record internal IModCommandKernel."
Assert-True ($lastStaticValidation.modCommandDispatcherPublicStaticMembersForbidden -eq $true) "Schema lastStaticValidationSnapshot must record internal-only ModCommandDispatcher static methods."
Assert-True ($lastStaticValidation.hectonEventBusPublicStaticMembersForbidden -eq $true) "Schema lastStaticValidationSnapshot must record internal-only HectonEventBus public-static member closure."
Assert-True ($lastStaticValidation.modLoadStatusPublic -eq $false) "Schema lastStaticValidationSnapshot must record internal ModLoadStatus."
Assert-True ($lastStaticValidation.modRuntimeInfoPublic -eq $false) "Schema lastStaticValidationSnapshot must record internal ModRuntimeInfo."
Assert-True ($lastStaticValidation.modRuntimeInfoMembersInternalOnly -eq $true) "Schema lastStaticValidationSnapshot must record internal-only ModRuntimeInfo members."
Assert-True ($lastStaticValidation.modWorldPersistenceRuntimePublic -eq $false) "Schema lastStaticValidationSnapshot must record internal ModWorldPersistenceManager and registry route."
Assert-True ($lastStaticValidation.modRegistryEventTypesPublic -eq $false) "Schema lastStaticValidationSnapshot must record internal mod registry event DTOs."
Assert-True ($lastStaticValidation.modRegistryListenersUsePrivateAdapters -eq $true) "Schema lastStaticValidationSnapshot must record private adapters for internal mod registry listeners."
Assert-True ($lastStaticValidation.managedAssemblyIdentityReservedNamesBlocked -eq $true) "Schema lastStaticValidationSnapshot must record reserved managed assembly identity blocking."
Assert-True ($lastStaticValidation.managedAssemblyIdentityScansAllPackageDlls -eq $true) "Schema lastStaticValidationSnapshot must record package DLL identity scanning."
Assert-True ($lastStaticValidation.managedAssemblyIdentityScanUsesBoundedEnumeration -eq $true) "Schema lastStaticValidationSnapshot must record bounded package DLL identity scanning."
Assert-True ([int]$lastStaticValidation.maxTopLevelManagedAssemblyCount -eq $maxTopLevelManagedAssemblyCount) "Schema lastStaticValidationSnapshot top-level managed assembly cap drift. Source=$maxTopLevelManagedAssemblyCount SchemaLastKnown=$($lastStaticValidation.maxTopLevelManagedAssemblyCount)"
Assert-True ($lastStaticValidation.excessTopLevelManagedAssembliesDisablePackage -eq $true) "Schema lastStaticValidationSnapshot must record managed DLL over-cap package disable."
Assert-True ([int]$lastStaticValidation.maxTopLevelBundleCount -eq $maxTopLevelBundleCount) "Schema lastStaticValidationSnapshot top-level bundle cap drift. Source=$maxTopLevelBundleCount SchemaLastKnown=$($lastStaticValidation.maxTopLevelBundleCount)"
Assert-True ([int]$lastStaticValidation.maxLocalizationFileCount -eq $maxLocalizationFileCount) "Schema lastStaticValidationSnapshot top-level localization cap drift. Source=$maxLocalizationFileCount SchemaLastKnown=$($lastStaticValidation.maxLocalizationFileCount)"
Assert-True ($lastStaticValidation.topLevelContentDiscoveryUsesBoundedEnumeration -eq $true) "Schema lastStaticValidationSnapshot must record bounded top-level content discovery."
Assert-True ($lastStaticValidation.modIdentifierCanonicalForm -eq $true) "Schema lastStaticValidationSnapshot must record canonical mod identifier validation."
Assert-True ($lastStaticValidation.dependencyIdentifiersValidated -eq $true) "Schema lastStaticValidationSnapshot must record dependency identifier validation."
Assert-True ($lastStaticValidation.entryAssemblyPathRestrictedToFileName -eq $true) "Schema lastStaticValidationSnapshot must record EntryAssembly file-name-only restriction."
Assert-True ($lastStaticValidation.modExecutionScopeRejectsAnonymousOwner -eq $true) "Schema lastStaticValidationSnapshot must record ModExecutionScope anonymous-owner rejection."
Assert-True ([long]$lastStaticValidation.manifestMaxBytes -eq $manifestMaxBytes) "Schema lastStaticValidationSnapshot manifest byte cap drift. Source=$manifestMaxBytes SchemaLastKnown=$($lastStaticValidation.manifestMaxBytes)"
Assert-True ($lastStaticValidation.manifestByteCapEnforcedBeforeRead -eq $true) "Schema lastStaticValidationSnapshot must record manifest byte cap before read."
Assert-True ([int]$lastStaticValidation.manifestDiscoveryMaxCount -eq $manifestDiscoveryMaxCount) "Schema lastStaticValidationSnapshot manifest discovery cap drift. Source=$manifestDiscoveryMaxCount SchemaLastKnown=$($lastStaticValidation.manifestDiscoveryMaxCount)"
Assert-True ($lastStaticValidation.manifestDiscoveryUsesBoundedEnumeration -eq $true) "Schema lastStaticValidationSnapshot must record bounded manifest discovery."
Assert-True ($lastStaticValidation.eventChannelsRejectAnonymousSubscribers -eq $true) "Schema lastStaticValidationSnapshot must record private event channel anonymous-subscriber rejection."
Assert-True ($lastStaticValidation.modApiSpecCurrentClosureRevisionMatchesSchema -eq $true) "Schema lastStaticValidationSnapshot must record Mod API spec closure revision parity."
Assert-True ($lastStaticValidation.futureSubtitleCueAliasesReserved -eq $true) "Schema lastStaticValidationSnapshot must record reserved subtitle cue opcode aliases."
Assert-True ($lastStaticValidation.editorRuntimeOpcodeTunersRejectReservedSubtitleAliases -eq $true) "Schema lastStaticValidationSnapshot must record editor runtime opcode tool rejection for reserved subtitle cue aliases."
Assert-True ($lastStaticValidation.nativeBytePayloadLayoutsChecked -eq $true) "Schema lastStaticValidationSnapshot must record native byte payload layout checking."
Assert-True ([int]$lastStaticValidation.nativeInteractionEventPayloadSizeBytes -eq $interactionEventPayloadSize) "Schema lastStaticValidationSnapshot InteractionEventPayload size drift."
Assert-True ([int]$lastStaticValidation.nativeCraftingEventPayloadSizeBytes -eq $craftingEventPayloadSize) "Schema lastStaticValidationSnapshot CraftingEventPayload size drift."
Assert-True ($lastStaticValidation.modSettingViewTypesPublic -eq $false) "Schema lastStaticValidationSnapshot must record internal mod settings UI DTOs."
Assert-True ($lastStaticValidation.futureCommandOutputSignalTypesPublic -eq $false) "Schema lastStaticValidationSnapshot must record internal FutureCommand output signal DTOs."
Assert-True ($lastStaticValidation.requestFutureRequiresActiveScope -eq $true) "Schema lastStaticValidationSnapshot must record RequestFuture active-scope ownership."
Assert-True ($lastStaticValidation.legacyCommandFacadesRequireActiveScope -eq $true) "Schema lastStaticValidationSnapshot must record legacy command facade active-scope quarantine."
Assert-True ([bool]$schema.commandApi.legacyCommandFacadesRequireActiveScope) "Schema commandApi must record legacy command facade active-scope quarantine."
Assert-True ($schema.commandApi.futureCommandSandboxConstantsPublic -eq $false) "Schema commandApi must record FutureCommandSandboxConstants as internal control-plane constants."
Assert-True ($schema.commandApi.futureCommandSandboxPublicStaticMembersForbidden -eq $true) "Schema commandApi must record internal-only sandbox control-plane static methods."
Assert-True ($schema.commandApi.mockModQueueMembersInternalOnly -eq $true) "Schema commandApi must record internal-only MockModQueue queue handle and instance members."
Assert-True ($schema.commandApi.modCommandDispatcherPublicStaticMembersForbidden -eq $true) "Schema commandApi must record internal-only ModCommandDispatcher static methods."
Assert-True ([bool]$schema.commandApi.futureCommandEnvelopeExposesSizeBytes) "Schema commandApi must record public FutureCommandEnvelope.SizeBytes as the only public sandbox size constant."
Assert-True ([bool]$schema.commandApi.futureSubtitleCueAliasesReserved) "Schema commandApi must record reserved subtitle cue opcode aliases."
Assert-True ([bool]$schema.commandApi.editorRuntimeOpcodeTunersRejectReservedSubtitleAliases) "Schema commandApi must record editor runtime opcode tools reject reserved subtitle cue aliases."
Assert-True (@($schema.commandApi.runtimeForbiddenFutureCommandOpcodes) -contains 'TriggerSubtitleCue') "Schema commandApi must include TriggerSubtitleCue in runtime forbidden future command opcodes."
Assert-True ($lastStaticValidation.resourceFacadeRequiresActiveScope -eq $true) "Schema lastStaticValidationSnapshot must record resource facade active-scope ownership."
Assert-True ($lastStaticValidation.resourceProxyRequiresActiveScope -eq $true) "Schema lastStaticValidationSnapshot must record resource proxy active-scope ownership."
Assert-True ($lastStaticValidation.resourceRegistryRejectsForgedOwner -eq $true) "Schema lastStaticValidationSnapshot must record resource registry owner-id match enforcement."
Assert-True ($lastStaticValidation.rawTextureByteCapEnforcedBeforeRead -eq $true) "Schema lastStaticValidationSnapshot must record raw texture byte cap before File.ReadAllBytes."
Assert-True ($lastStaticValidation.rawTextureReadFailsClosed -eq $true) "Schema lastStaticValidationSnapshot must record fail-closed raw texture file reads."
Assert-True ($lastStaticValidation.assetBundleSuffixFallbackDisabled -eq $true) "Schema lastStaticValidationSnapshot must record disabled AssetBundle suffix fallback."
Assert-True ($lastStaticValidation.assetBundleGetAllAssetNamesForbidden -eq $true) "Schema lastStaticValidationSnapshot must record forbidden AssetBundle.GetAllAssetNames lookup."
Assert-True ($lastStaticValidation.moddingSdkHubPresent -eq $true) "Schema lastStaticValidationSnapshot must record SDK hub presence."
Assert-True ($lastStaticValidation.moddingSdkHubOpensBuilder -eq $true) "Schema lastStaticValidationSnapshot must record SDK hub builder launch."
Assert-True ($lastStaticValidation.moddingSdkHubOpensStarterWorkbench -eq $true) "Schema lastStaticValidationSnapshot must record SDK hub starter workbench launch."
Assert-True ($lastStaticValidation.moddingSdkHubPrioritizesExternalStarterKit -eq $true) "Schema lastStaticValidationSnapshot must record SDK hub external starter kit priority."
Assert-True ($lastStaticValidation.moddingSdkHubGatesLegacyBuilder -eq $true) "Schema lastStaticValidationSnapshot must record SDK hub legacy builder warning gate."
Assert-True ($lastStaticValidation.modBuilderMenuIsInternalLegacy -eq $true) "Schema lastStaticValidationSnapshot must record internal legacy builder menu."
Assert-True ($lastStaticValidation.moddingSdkHubLinksCoreDocs -eq $true) "Schema lastStaticValidationSnapshot must record SDK hub doc links."
Assert-True ($lastStaticValidation.moddingSdkHubRunsStaticValidator -eq $true) "Schema lastStaticValidationSnapshot must record SDK hub static validator action."
Assert-True ($lastStaticValidation.moddingSdkHubRunsStaticValidatorAsync -eq $true) "Schema lastStaticValidationSnapshot must record async SDK hub static validator execution."
Assert-True ($lastStaticValidation.moddingSdkHubShowsValidatorFailuresAsErrors -eq $true) "Schema lastStaticValidationSnapshot must record SDK Hub validator failure error UI."
Assert-True ($lastStaticValidation.moddingSdkHubShowsEnvelopeOnlyBoundary -eq $true) "Schema lastStaticValidationSnapshot must record SDK hub envelope-only warning."
Assert-True ($lastStaticValidation.externalStarterKitWorkbenchPresent -eq $true) "Schema lastStaticValidationSnapshot must record starter workbench presence."
Assert-True ($lastStaticValidation.externalStarterKitWorkbenchUsesIdentityTool -eq $true) "Schema lastStaticValidationSnapshot must record starter workbench identity tool route."
Assert-True ($lastStaticValidation.externalStarterKitWorkbenchUsesPrepareTool -eq $true) "Schema lastStaticValidationSnapshot must record starter workbench prepare tool route."
Assert-True ($lastStaticValidation.externalStarterKitWorkbenchCanRefreshStarterKit -eq $true) "Schema lastStaticValidationSnapshot must record starter workbench generator reuse."
Assert-True ($lastStaticValidation.externalStarterKitWorkbenchListsOpcodes -eq $true) "Schema lastStaticValidationSnapshot must record starter workbench opcode discovery."
Assert-True ($lastStaticValidation.externalStarterKitWorkbenchShowsReviewSummary -eq $true) "Schema lastStaticValidationSnapshot must record starter workbench review summary."
Assert-True ($lastStaticValidation.externalStarterKitWorkbenchShowsEnvelopeBoundary -eq $true) "Schema lastStaticValidationSnapshot must record starter workbench envelope-only warning."
Assert-True ($lastStaticValidation.externalStarterKitWorkbenchShowsStarterHealth -eq $true) "Schema lastStaticValidationSnapshot must record starter workbench health panel."
Assert-True ($lastStaticValidation.externalStarterKitWorkbenchShowsCapabilityMatrix -eq $true) "Schema lastStaticValidationSnapshot must record starter workbench capability matrix."
Assert-True ($lastStaticValidation.externalStarterKitWorkbenchRequiredFileListMatchesValidator -eq $true) "Schema lastStaticValidationSnapshot must record Workbench required-file list parity with validator."
Assert-True ($lastStaticValidation.externalStarterKitWorkbenchRunsStructureValidator -eq $true) "Schema lastStaticValidationSnapshot must record starter workbench structure validator route."
Assert-True ($lastStaticValidation.externalStarterKitWorkbenchLinksCoreDocs -eq $true) "Schema lastStaticValidationSnapshot must record starter workbench core doc links."
Assert-True ($lastStaticValidation.externalStarterKitWorkbenchOpensVsCodeTasks -eq $true) "Schema lastStaticValidationSnapshot must record starter workbench VS Code task/settings file access."
Assert-True ($lastStaticValidation.externalStarterKitWorkbenchRunsToolsAsync -eq $true) "Schema lastStaticValidationSnapshot must record async starter tool execution."
Assert-True ($lastStaticValidation.externalStarterKitWorkbenchShowsToolFailuresAsErrors -eq $true) "Schema lastStaticValidationSnapshot must record starter workbench tool failure error UI."
Assert-True ($lastStaticValidation.externalStarterKitWorkbenchShowsReviewFreshness -eq $true) "Schema lastStaticValidationSnapshot must record starter workbench review freshness."
Assert-True ($lastStaticValidation.externalStarterKitWorkbenchShowsGraphContractPreview -eq $true) "Schema lastStaticValidationSnapshot must record starter workbench graph contract preview."
Assert-True ($lastStaticValidation.externalStarterKitWorkbenchGeneratesGraphNodeSnippet -eq $true) "Schema lastStaticValidationSnapshot must record starter workbench graph node snippet generation."
Assert-True ($lastStaticValidation.externalStarterKitWorkbenchHasGraphOpcodePicker -eq $true) "Schema lastStaticValidationSnapshot must record starter workbench graph opcode picker."
Assert-True ($lastStaticValidation.externalStarterKitWorkbenchEditsGraphNodeParameters -eq $true) "Schema lastStaticValidationSnapshot must record starter workbench graph node parameter controls."
Assert-True ($lastStaticValidation.externalStarterKitWorkbenchAppliesGraphNodeSnippet -eq $true) "Schema lastStaticValidationSnapshot must record starter workbench graph node snippet apply route."
Assert-True ($lastStaticValidation.externalStarterKitWorkbenchGeneratesAuthoringSnippets -eq $true) "Schema lastStaticValidationSnapshot must record starter workbench settings/locale snippet generation."
Assert-True ($lastStaticValidation.externalStarterKitWorkbenchAppliesAuthoringSnippets -eq $true) "Schema lastStaticValidationSnapshot must record starter workbench settings/locale snippet apply route."
Assert-True ($lastStaticValidation.externalStarterKitWorkbenchBuildsSubmissionPackage -eq $true) "Schema lastStaticValidationSnapshot must record starter workbench submission package generation."
Assert-True ($lastStaticValidation.externalStarterKitWorkbenchShowsSubmissionPackageStatus -eq $true) "Schema lastStaticValidationSnapshot must record starter workbench submission package status/freshness."
Assert-True ($lastStaticValidation.externalStarterKitWorkbenchShowsSubmissionZipIntegrity -eq $true) "Schema lastStaticValidationSnapshot must record starter workbench submission zip integrity display."
Assert-True ($lastStaticValidation.externalStarterKitWorkbenchUsesCaseExactSubmissionZipIntegrity -eq $true) "Schema lastStaticValidationSnapshot must record starter workbench case-exact submission zip integrity."
Assert-True ($lastStaticValidation.externalStarterKitWorkbenchRunsDoctor -eq $true) "Schema lastStaticValidationSnapshot must record starter workbench package doctor route."
Assert-True ($lastStaticValidation.externalStarterKitWorkbenchShowsAuthoringDataPreview -eq $true) "Schema lastStaticValidationSnapshot must record starter workbench settings/locale authoring data preview."
Assert-True ($lastStaticValidation.externalStarterKitWorkbenchShowsContentAssetPreview -eq $true) "Schema lastStaticValidationSnapshot must record starter workbench content asset manifest preview."
Assert-True ($lastStaticValidation.externalStarterKitWorkbenchGeneratesAssetEntrySnippet -eq $true) "Schema lastStaticValidationSnapshot must record starter workbench content asset snippet generation."
Assert-True ($lastStaticValidation.externalStarterKitWorkbenchAppliesAssetEntrySnippet -eq $true) "Schema lastStaticValidationSnapshot must record starter workbench content asset snippet apply route."
Assert-True ($lastStaticValidation.externalStarterKitWorkbenchConfiguresManifestContract -eq $true) "Schema lastStaticValidationSnapshot must record starter workbench manifest contract configuration."
Assert-True ($lastStaticValidation.externalStarterKitWorkbenchConfiguresDependencies -eq $true) "Schema lastStaticValidationSnapshot must record starter workbench dependency configuration."
Assert-True ($lastStaticValidation.externalStarterKitWorkbenchChecksRootLauncher -eq $true) "Schema lastStaticValidationSnapshot must record starter workbench root launcher health/file access."
Assert-True ($lastStaticValidation.externalStarterKitWorkbenchUsesBoundedPreviewReads -eq $true) "Schema lastStaticValidationSnapshot must record starter workbench bounded preview reads."
Assert-True ($lastStaticValidation.externalStarterKitWorkbenchUsesStreamingCappedPreviewReads -eq $true) "Schema lastStaticValidationSnapshot must record starter workbench streaming capped preview reads."
Assert-True ($lastStaticValidation.externalStarterKitWorkbenchRejectsInvalidUtf8PreviewReads -eq $true) "Schema lastStaticValidationSnapshot must record starter workbench strict UTF-8 preview reads."
Assert-True ($lastStaticValidation.externalStarterKitGeneratorPresent -eq $true) "Schema lastStaticValidationSnapshot must record external starter kit generator presence."
Assert-True ($lastStaticValidation.externalStarterKitWritesRootLauncher -eq $true) "Schema lastStaticValidationSnapshot must record root no-Unity launcher output."
Assert-True ($lastStaticValidation.externalStarterKitWritesAuthoringManifest -eq $true) "Schema lastStaticValidationSnapshot must record external starter kit authoring manifest output."
Assert-True ($lastStaticValidation.externalStarterKitWritesRuntimeManifest -eq $true) "Schema lastStaticValidationSnapshot must record external starter kit runtime manifest output."
Assert-True ($lastStaticValidation.externalStarterKitWritesFolderReadmes -eq $true) "Schema lastStaticValidationSnapshot must record external starter kit folder README output."
Assert-True ($lastStaticValidation.externalStarterKitWritesCapabilityGuide -eq $true) "Schema lastStaticValidationSnapshot must record external starter kit capability guide output."
Assert-True ($lastStaticValidation.externalStarterKitCopiesOpcodeReferences -eq $true) "Schema lastStaticValidationSnapshot must record copied opcode references."
Assert-True ($lastStaticValidation.externalStarterKitDocumentsNoUnityProjectRequirement -eq $true) "Schema lastStaticValidationSnapshot must record no-full-Unity-project guidance."
Assert-True ($lastStaticValidation.externalStarterKitDocumentsEnvelopeOnlyBoundary -eq $true) "Schema lastStaticValidationSnapshot must record envelope-only starter kit guidance."
Assert-True ($lastStaticValidation.externalStarterKitWritesLocalStructureValidator -eq $true) "Schema lastStaticValidationSnapshot must record local starter kit structure validator output."
Assert-True ($lastStaticValidation.externalStarterKitValidatorChecksRequiredFiles -eq $true) "Schema lastStaticValidationSnapshot must record starter validator required-file checks."
Assert-True ($lastStaticValidation.externalStarterKitValidatorChecksCapabilityGuide -eq $true) "Schema lastStaticValidationSnapshot must record starter validator capability guide checks."
Assert-True ($lastStaticValidation.externalStarterKitValidatorChecksRootLauncher -eq $true) "Schema lastStaticValidationSnapshot must record starter validator root launcher check."
Assert-True ($lastStaticValidation.externalStarterKitValidatorChecksEnvelopeOnly -eq $true) "Schema lastStaticValidationSnapshot must record starter validator envelope-only checks."
Assert-True ($lastStaticValidation.externalStarterKitValidatorChecksManagedEntryDisabled -eq $true) "Schema lastStaticValidationSnapshot must record starter validator managed-entry rejection."
Assert-True ($lastStaticValidation.externalStarterKitValidatorChecksCanonicalIds -eq $true) "Schema lastStaticValidationSnapshot must record starter validator canonical ID checks."
Assert-True ($lastStaticValidation.externalStarterKitValidatorChecksManifestIdParity -eq $true) "Schema lastStaticValidationSnapshot must record starter validator manifest ID parity checks."
Assert-True ($lastStaticValidation.externalStarterKitValidatorChecksDependencyIds -eq $true) "Schema lastStaticValidationSnapshot must record starter validator dependency ID checks."
Assert-True ($lastStaticValidation.externalStarterKitValidatorChecksGraphOpcodes -eq $true) "Schema lastStaticValidationSnapshot must record starter graph opcode allowlist checks."
Assert-True ($lastStaticValidation.externalStarterKitValidatorChecksGraphBudget -eq $true) "Schema lastStaticValidationSnapshot must record starter graph budget parity checks."
Assert-True ($lastStaticValidation.externalStarterKitValidatorRejectsInvalidGraphOpcode -eq $true) "Schema lastStaticValidationSnapshot must record invalid graph opcode rejection."
Assert-True ($lastStaticValidation.externalStarterKitWritesReviewManifestBuilder -eq $true) "Schema lastStaticValidationSnapshot must record starter review manifest builder output."
Assert-True ($lastStaticValidation.externalStarterKitWritesIdentityTool -eq $true) "Schema lastStaticValidationSnapshot must record starter identity helper output."
Assert-True ($lastStaticValidation.externalStarterKitWritesPrepareTool -eq $true) "Schema lastStaticValidationSnapshot must record starter prepare tool output."
Assert-True ($lastStaticValidation.externalStarterKitWritesAllowedOpcodeListTool -eq $true) "Schema lastStaticValidationSnapshot must record starter allowed opcode list helper output."
Assert-True ($lastStaticValidation.externalStarterKitAllowedOpcodeListToolPasses -eq $true) "Schema lastStaticValidationSnapshot must record starter allowed opcode list helper pass."
Assert-True ($lastStaticValidation.externalStarterKitAllowedOpcodeListToolSupportsJson -eq $true) "Schema lastStaticValidationSnapshot must record starter allowed opcode list JSON output."
Assert-True ($lastStaticValidation.externalStarterKitWritesGraphNodeSnippetTool -eq $true) "Schema lastStaticValidationSnapshot must record starter graph node snippet helper output."
Assert-True ($lastStaticValidation.externalStarterKitGraphNodeSnippetToolPasses -eq $true) "Schema lastStaticValidationSnapshot must record starter graph node snippet helper pass."
Assert-True ($lastStaticValidation.externalStarterKitGraphNodeSnippetToolSupportsJson -eq $true) "Schema lastStaticValidationSnapshot must record starter graph node snippet JSON output."
Assert-True ($lastStaticValidation.externalStarterKitGraphNodeSnippetSupportsParameters -eq $true) "Schema lastStaticValidationSnapshot must record starter graph node snippet parameter/disabled support."
Assert-True ($lastStaticValidation.externalStarterKitRootLauncherSupportsGraphNodeSnippet -eq $true) "Schema lastStaticValidationSnapshot must record starter root launcher graph node snippet route."
Assert-True ($lastStaticValidation.externalStarterKitRootLauncherSupportsGraphNodeParameters -eq $true) "Schema lastStaticValidationSnapshot must record starter root launcher graph node parameter route."
Assert-True ($lastStaticValidation.externalStarterKitWritesGraphNodeApplyTool -eq $true) "Schema lastStaticValidationSnapshot must record starter graph node apply helper output."
Assert-True ($lastStaticValidation.externalStarterKitGraphNodeApplyToolPasses -eq $true) "Schema lastStaticValidationSnapshot must record starter graph node apply helper pass."
Assert-True ($lastStaticValidation.externalStarterKitGraphNodeApplyToolRejectsDuplicateWithoutReplace -eq $true) "Schema lastStaticValidationSnapshot must record starter graph node apply duplicate rejection."
Assert-True ($lastStaticValidation.externalStarterKitRootLauncherSupportsGraphNodeApply -eq $true) "Schema lastStaticValidationSnapshot must record starter root launcher graph node apply route."
Assert-True ($lastStaticValidation.externalStarterKitWritesSettingsRowSnippetTool -eq $true) "Schema lastStaticValidationSnapshot must record starter settings row snippet helper output."
Assert-True ($lastStaticValidation.externalStarterKitSettingsRowSnippetToolPasses -eq $true) "Schema lastStaticValidationSnapshot must record starter settings row snippet helper pass."
Assert-True ($lastStaticValidation.externalStarterKitSettingsRowSnippetToolSupportsJson -eq $true) "Schema lastStaticValidationSnapshot must record starter settings row snippet JSON output."
Assert-True ($lastStaticValidation.externalStarterKitWritesSettingsRowApplyTool -eq $true) "Schema lastStaticValidationSnapshot must record starter settings row apply helper output."
Assert-True ($lastStaticValidation.externalStarterKitSettingsRowApplyToolPasses -eq $true) "Schema lastStaticValidationSnapshot must record starter settings row apply helper pass."
Assert-True ($lastStaticValidation.externalStarterKitSettingsRowApplyToolRejectsDuplicateWithoutReplace -eq $true) "Schema lastStaticValidationSnapshot must record starter settings row apply duplicate rejection."
Assert-True ($lastStaticValidation.externalStarterKitWritesLocaleEntrySnippetTool -eq $true) "Schema lastStaticValidationSnapshot must record starter locale entry snippet helper output."
Assert-True ($lastStaticValidation.externalStarterKitLocaleEntrySnippetToolPasses -eq $true) "Schema lastStaticValidationSnapshot must record starter locale entry snippet helper pass."
Assert-True ($lastStaticValidation.externalStarterKitLocaleEntrySnippetToolSupportsJson -eq $true) "Schema lastStaticValidationSnapshot must record starter locale entry snippet JSON output."
Assert-True ($lastStaticValidation.externalStarterKitWritesLocaleEntryApplyTool -eq $true) "Schema lastStaticValidationSnapshot must record starter locale entry apply helper output."
Assert-True ($lastStaticValidation.externalStarterKitLocaleEntryApplyToolPasses -eq $true) "Schema lastStaticValidationSnapshot must record starter locale entry apply helper pass."
Assert-True ($lastStaticValidation.externalStarterKitLocaleEntryApplyToolRejectsDuplicateWithoutReplace -eq $true) "Schema lastStaticValidationSnapshot must record starter locale entry apply duplicate rejection."
Assert-True ($lastStaticValidation.externalStarterKitWritesAssetEntrySnippetTool -eq $true) "Schema lastStaticValidationSnapshot must record starter asset entry snippet helper output."
Assert-True ($lastStaticValidation.externalStarterKitAssetEntrySnippetToolPasses -eq $true) "Schema lastStaticValidationSnapshot must record starter asset entry snippet helper pass."
Assert-True ($lastStaticValidation.externalStarterKitAssetEntrySnippetToolSupportsJson -eq $true) "Schema lastStaticValidationSnapshot must record starter asset entry snippet JSON output."
Assert-True ($lastStaticValidation.externalStarterKitWritesAssetEntryApplyTool -eq $true) "Schema lastStaticValidationSnapshot must record starter asset entry apply helper output."
Assert-True ($lastStaticValidation.externalStarterKitAssetEntryApplyToolPasses -eq $true) "Schema lastStaticValidationSnapshot must record starter asset entry apply helper pass."
Assert-True ($lastStaticValidation.externalStarterKitAssetEntryApplyToolRejectsDuplicateWithoutReplace -eq $true) "Schema lastStaticValidationSnapshot must record starter asset entry apply duplicate rejection."
Assert-True ($lastStaticValidation.externalStarterKitSnippetPathsRequireStrictJsonRelativePaths -eq $true) "Schema lastStaticValidationSnapshot must record starter snippet strict JSON path gates."
Assert-True ($lastStaticValidation.externalStarterKitSnippetApplyUsesBoundedJsonReads -eq $true) "Schema lastStaticValidationSnapshot must record starter snippet apply bounded JSON reads."
Assert-True ($lastStaticValidation.externalStarterKitAssetSnippetRejectsNonPortableAssetPaths -eq $true) "Schema lastStaticValidationSnapshot must record starter asset snippet portable path gate."
Assert-True ($lastStaticValidation.externalStarterKitGraphNodeSnippetCapsParametersJson -eq $true) "Schema lastStaticValidationSnapshot must record graph node snippet ParametersJson cap."
Assert-True ($lastStaticValidation.externalStarterKitValidatorCapsRootJsonReads -eq $true) "Schema lastStaticValidationSnapshot must record structure validator bounded root JSON/text reads."
Assert-True ($lastStaticValidation.externalStarterKitValidatorRejectsNonPortableAssetManifestPaths -eq $true) "Schema lastStaticValidationSnapshot must record structure validator portable asset manifest path gate."
Assert-True ($lastStaticValidation.externalStarterKitCoreToolsUseBoundedJsonReads -eq $true) "Schema lastStaticValidationSnapshot must record public starter core tool bounded JSON reads."
Assert-True ($lastStaticValidation.externalStarterKitCoreToolsUseStreamingStrictUtf8JsonReads -eq $true) "Schema lastStaticValidationSnapshot must record public starter shared streaming strict UTF-8 JSON/text reads."
Assert-True ($lastStaticValidation.externalStarterKitWritesManifestContractTool -eq $true) "Schema lastStaticValidationSnapshot must record starter manifest contract helper output."
Assert-True ($lastStaticValidation.externalStarterKitManifestContractToolPasses -eq $true) "Schema lastStaticValidationSnapshot must record starter manifest contract helper pass."
Assert-True ($lastStaticValidation.externalStarterKitManifestContractRejectsUnknownCapability -eq $true) "Schema lastStaticValidationSnapshot must record starter manifest contract unknown capability rejection."
Assert-True ($lastStaticValidation.externalStarterKitWritesFirstModTool -eq $true) "Schema lastStaticValidationSnapshot must record starter first playable mod helper output."
Assert-True ($lastStaticValidation.externalStarterKitWritesInstallLocalTool -eq $true) "Schema lastStaticValidationSnapshot must record starter local discovery install helper output."
Assert-True ($lastStaticValidation.externalStarterKitWritesDiagnoseLocalTool -eq $true) "Schema lastStaticValidationSnapshot must record starter local Mods diagnosis helper output."
Assert-True ($lastStaticValidation.externalStarterKitDiagnoseLocalUsesRecursiveManifestDiscovery -eq $true) "Schema lastStaticValidationSnapshot must record recursive local Mods manifest discovery."
Assert-True ($lastStaticValidation.externalStarterKitDiagnoseLocalChecksDependencyGraph -eq $true) "Schema lastStaticValidationSnapshot must record local Mods dependency graph diagnosis."
Assert-True ($lastStaticValidation.externalStarterKitWritesDependenciesTool -eq $true) "Schema lastStaticValidationSnapshot must record starter dependency helper output."
Assert-True ($lastStaticValidation.externalStarterKitDependencyToolMirrorsBothManifests -eq $true) "Schema lastStaticValidationSnapshot must record starter dependency helper authoring/runtime manifest parity."
Assert-True ($lastStaticValidation.externalStarterKitDependenciesToolWritesStrictUtf8NoBom -eq $true) "Schema lastStaticValidationSnapshot must record starter dependency helper strict UTF-8 no-BOM writes."
Assert-True ($lastStaticValidation.externalStarterKitWritesDoctorTool -eq $true) "Schema lastStaticValidationSnapshot must record starter package doctor helper output."
Assert-True ($lastStaticValidation.externalStarterKitDoctorToolIsReadOnly -eq $true) "Schema lastStaticValidationSnapshot must record starter package doctor read-only contract."
Assert-True ($lastStaticValidation.externalStarterKitDoctorVerifiesSubmissionZipContents -eq $true) "Schema lastStaticValidationSnapshot must record starter package doctor submission zip content proof."
Assert-True ($lastStaticValidation.externalStarterKitDoctorRejectsUnsafeZipEntries -eq $true) "Schema lastStaticValidationSnapshot must record starter package doctor unsafe zip entry rejection."
Assert-True ($lastStaticValidation.externalStarterKitDoctorUsesCaseExactSubmissionZipIntegrity -eq $true) "Schema lastStaticValidationSnapshot must record starter package doctor case-exact submission zip integrity."
Assert-True ($lastStaticValidation.externalStarterKitDoctorRejectsReservedTopLevelCaseVariants -eq $true) "Schema lastStaticValidationSnapshot must record starter package doctor reserved top-level case-variant rejection."
Assert-True ($lastStaticValidation.externalStarterKitDoctorFailsNonReadyExit -eq $true) "Schema lastStaticValidationSnapshot must record starter package doctor non-ready nonzero exit contract."
Assert-True ($lastStaticValidation.externalStarterKitRootLauncherPreservesToolExitCodes -eq $true) "Schema lastStaticValidationSnapshot must record starter root launcher delegated exit-code preservation."
Assert-True ($lastStaticValidation.externalStarterKitReviewManifestRejectsCaseFoldSourceDuplicates -eq $true) "Schema lastStaticValidationSnapshot must record starter review manifest case-fold duplicate rejection."
Assert-True ($lastStaticValidation.externalStarterKitValidatorRequiresExactPathCasing -eq $true) "Schema lastStaticValidationSnapshot must record starter validator exact path casing."
Assert-True ($lastStaticValidation.externalStarterKitRejectsReservedTopLevelCaseVariants -eq $true) "Schema lastStaticValidationSnapshot must record reserved starter top-level case-variant rejection."
Assert-True ($lastStaticValidation.externalStarterKitReviewManifestUsesExactReservedOutputFolders -eq $true) "Schema lastStaticValidationSnapshot must record exact reserved output folder handling."
Assert-True ($lastStaticValidation.externalStarterKitPackageProofRequiresLowercaseSha256 -eq $true) "Schema lastStaticValidationSnapshot must record lowercase SHA-256 package proof."
Assert-True ($lastStaticValidation.externalStarterKitRootLauncherSupportsAuthoringSnippets -eq $true) "Schema lastStaticValidationSnapshot must record starter root launcher settings/locale snippet route."
Assert-True ($lastStaticValidation.externalStarterKitRootLauncherSupportsAuthoringSnippetApply -eq $true) "Schema lastStaticValidationSnapshot must record starter root launcher settings/locale apply route."
Assert-True ($lastStaticValidation.externalStarterKitRootLauncherSupportsAssetEntrySnippet -eq $true) "Schema lastStaticValidationSnapshot must record starter root launcher asset snippet route."
Assert-True ($lastStaticValidation.externalStarterKitRootLauncherSupportsAssetEntryApply -eq $true) "Schema lastStaticValidationSnapshot must record starter root launcher asset apply route."
Assert-True ($lastStaticValidation.externalStarterKitRootLauncherSupportsManifestContract -eq $true) "Schema lastStaticValidationSnapshot must record starter root launcher manifest contract route."
Assert-True ($lastStaticValidation.externalStarterKitRootLauncherSupportsCapabilities -eq $true) "Schema lastStaticValidationSnapshot must record starter root launcher capability guide route."
Assert-True ($lastStaticValidation.externalStarterKitRootLauncherUsesStrictCapabilityGuideRead -eq $true) "Schema lastStaticValidationSnapshot must record strict capped capability guide reads in the root launcher."
Assert-True ($lastStaticValidation.externalStarterKitHubScriptsRequireCheckedInTemplates -eq $true) "Schema lastStaticValidationSnapshot must record checked-in template enforcement for starter root/tool scripts."
Assert-True ($lastStaticValidation.externalStarterKitHubExecutableFallbackBodiesRemoved -eq $true) "Schema lastStaticValidationSnapshot must record removal of embedded executable fallback bodies from the starter Hub."
Assert-True ($lastStaticValidation.externalStarterKitWritesSubmissionPackageTool -eq $true) "Schema lastStaticValidationSnapshot must record starter submission package helper output."
Assert-True ($lastStaticValidation.externalStarterKitSubmissionPackagePreservesPreviousOutputUntilSuccess -eq $true) "Schema lastStaticValidationSnapshot must record starter submission package atomic handoff."
Assert-True ($lastStaticValidation.externalStarterKitSubmissionPackageRefreshesTimestampAfterReplace -eq $true) "Schema lastStaticValidationSnapshot must record starter submission package timestamp freshness after replace."
Assert-True ($lastStaticValidation.externalStarterKitSubmissionPackageToolPasses -eq $true) "Schema lastStaticValidationSnapshot must record starter submission package helper pass."
Assert-True ($lastStaticValidation.externalStarterKitSubmissionPackageIncludesReviewManifest -eq $true) "Schema lastStaticValidationSnapshot must record starter submission package review manifest inclusion."
Assert-True ($lastStaticValidation.externalStarterKitSubmissionPackageUsesCaseExactSourceEntries -eq $true) "Schema lastStaticValidationSnapshot must record starter submission package exact source entry contract."
Assert-True ($lastStaticValidation.externalStarterKitWorkbenchReportsReservedFolderCaseVariants -eq $true) "Schema lastStaticValidationSnapshot must record starter Workbench reserved folder case-variant reporting."
Assert-True ($lastStaticValidation.externalStarterKitRootLauncherSupportsSubmissionPackage -eq $true) "Schema lastStaticValidationSnapshot must record starter root launcher submission package route."
Assert-True ($lastStaticValidation.externalStarterKitRootLauncherSupportsFirstMod -eq $true) "Schema lastStaticValidationSnapshot must record starter root launcher first-mod route."
Assert-True ($lastStaticValidation.externalStarterKitRootLauncherSupportsInstallLocal -eq $true) "Schema lastStaticValidationSnapshot must record starter root launcher install-local route."
Assert-True ($lastStaticValidation.externalStarterKitRootLauncherSupportsDiagnoseLocal -eq $true) "Schema lastStaticValidationSnapshot must record starter root launcher diagnose-local route."
Assert-True ($lastStaticValidation.externalStarterKitRootLauncherSupportsDoctor -eq $true) "Schema lastStaticValidationSnapshot must record starter root launcher doctor route."
Assert-True ($lastStaticValidation.externalStarterKitRootLauncherSupportsDependencies -eq $true) "Schema lastStaticValidationSnapshot must record starter root launcher dependencies route."
Assert-True ($lastStaticValidation.externalStarterKitNestedToolsPreserveChildExitCodes -eq $true) "Schema lastStaticValidationSnapshot must record nested starter child exit-code preservation."
Assert-True ($lastStaticValidation.externalStarterKitIdentityToolValidatesCanonicalId -eq $true) "Schema lastStaticValidationSnapshot must record starter identity helper canonical ID validation."
Assert-True ($lastStaticValidation.externalStarterKitValidatorChecksSemver -eq $true) "Schema lastStaticValidationSnapshot must record starter semantic version validation."
Assert-True ($lastStaticValidation.externalStarterKitValidatorChecksManifestIdentityTextParity -eq $true) "Schema lastStaticValidationSnapshot must record starter identity text parity validation."
Assert-True ($lastStaticValidation.externalStarterKitIdentityToolRejectsInvalidVersion -eq $true) "Schema lastStaticValidationSnapshot must record starter invalid version rejection."
Assert-True ($lastStaticValidation.externalStarterKitIdentityToolRollsBackBothManifestsOnValidationFailure -eq $true) "Schema lastStaticValidationSnapshot must record starter identity rollback on validation failure."
Assert-True ($lastStaticValidation.externalStarterKitToolsAvoidNestedPowerShell -eq $true) "Schema lastStaticValidationSnapshot must record starter tools in-process script chaining."
Assert-True ($lastStaticValidation.externalStarterKitToolsUsePortableJoinPath -eq $true) "Schema lastStaticValidationSnapshot must record starter portable path composition."
Assert-True ($lastStaticValidation.externalStarterKitWritesJsonSchemas -eq $true) "Schema lastStaticValidationSnapshot must record starter JSON Schema outputs."
Assert-True ($lastStaticValidation.externalStarterKitValidatorChecksJsonSchemas -eq $true) "Schema lastStaticValidationSnapshot must record starter JSON Schema validator checks."
Assert-True ($lastStaticValidation.externalStarterKitValidatorChecksEditorSchemaMappings -eq $true) "Schema lastStaticValidationSnapshot must record exact starter editor schema mapping checks."
Assert-True ($lastStaticValidation.externalStarterKitWritesVsCodeTasks -eq $true) "Schema lastStaticValidationSnapshot must record starter VS Code task output."
Assert-True ($lastStaticValidation.externalStarterKitVsCodeTasksPresent -eq $true) "Schema lastStaticValidationSnapshot must record versioned starter VS Code task surface."
Assert-True ($lastStaticValidation.externalStarterKitVsCodeTasksSupportDisabledAndReplace -eq $true) "Schema lastStaticValidationSnapshot must record starter VS Code disabled-node and replace task surface."
Assert-True ($lastStaticValidation.externalStarterKitVsCodeTasksSupportLocalInstall -eq $true) "Schema lastStaticValidationSnapshot must record starter VS Code local discovery install task surface."
Assert-True ($lastStaticValidation.externalStarterKitVsCodeTasksSupportLocalDiagnose -eq $true) "Schema lastStaticValidationSnapshot must record starter VS Code local Mods diagnosis task surface."
Assert-True ($lastStaticValidation.externalStarterKitVsCodeTasksSupportDependencies -eq $true) "Schema lastStaticValidationSnapshot must record starter VS Code dependency task surface."
Assert-True ($lastStaticValidation.externalStarterKitVsCodeTasksSupportDoctor -eq $true) "Schema lastStaticValidationSnapshot must record starter VS Code package doctor task surface."
Assert-True ($lastStaticValidation.externalStarterKitValidatorChecksVsCodeTasks -eq $true) "Schema lastStaticValidationSnapshot must record starter VS Code task validation."
Assert-True ($lastStaticValidation.externalStarterKitValidatorChecksSettingsAndLocaleContracts -eq $true) "Schema lastStaticValidationSnapshot must record starter settings/locale contract validation."
Assert-True ($lastStaticValidation.externalStarterKitValidatorChecksAssetManifestContracts -eq $true) "Schema lastStaticValidationSnapshot must record starter content asset manifest validation."
Assert-True ($lastStaticValidation.externalStarterKitValidatorChecksManifestCapabilities -eq $true) "Schema lastStaticValidationSnapshot must record starter manifest capability/budget validation."
Assert-True ($lastStaticValidation.externalStarterKitTemplateVersioned -eq $true) "Schema lastStaticValidationSnapshot must record versioned starter kit template."
Assert-True ($lastStaticValidation.externalStarterKitTemplatePassesLocalValidator -eq $true) "Schema lastStaticValidationSnapshot must record starter template local validator pass."
Assert-True ($lastStaticValidation.externalStarterKitRootLauncherPasses -eq $true) "Schema lastStaticValidationSnapshot must record root launcher validation route pass."
Assert-True ($lastStaticValidation.externalStarterKitTemplateReferenceCsvsMatchSource -eq $true) "Schema lastStaticValidationSnapshot must record starter template reference CSV source parity."
Assert-True ($lastStaticValidation.externalStarterKitReviewManifestPasses -eq $true) "Schema lastStaticValidationSnapshot must record starter review manifest pass."
Assert-True ($lastStaticValidation.externalStarterKitReviewManifestHashesFiles -eq $true) "Schema lastStaticValidationSnapshot must record starter review manifest hash proof."
Assert-True ($lastStaticValidation.externalStarterKitReviewManifestIncludesIdentity -eq $true) "Schema lastStaticValidationSnapshot must record starter review manifest identity summary."
Assert-True ($lastStaticValidation.externalStarterKitReviewManifestIdentityMatchesRuntimeManifest -eq $true) "Schema lastStaticValidationSnapshot must record starter review manifest identity parity."
Assert-True ($lastStaticValidation.externalStarterKitReviewManifestExcludesReports -eq $true) "Schema lastStaticValidationSnapshot must record starter review manifest report/output exclusion."
Assert-True ($lastStaticValidation.externalStarterKitReviewManifestHasLimits -eq $true) "Schema lastStaticValidationSnapshot must record starter review manifest count/byte limits."
Assert-True ($lastStaticValidation.externalStarterKitReviewManifestRejectsOversizedFile -eq $true) "Schema lastStaticValidationSnapshot must record starter review manifest oversized-file rejection."
Assert-True ($lastStaticValidation.externalStarterKitLocalInstallRequiresExactReviewProof -eq $true) "Schema lastStaticValidationSnapshot must record starter local install exact review proof."
Assert-True ($lastStaticValidation.externalStarterKitDiagnoseLocalRequiresExactReviewProof -eq $true) "Schema lastStaticValidationSnapshot must record starter local diagnosis exact review proof."
Assert-True ($lastStaticValidation.externalStarterKitIdentityToolPasses -eq $true) "Schema lastStaticValidationSnapshot must record starter identity helper pass."
Assert-True ($lastStaticValidation.externalStarterKitPrepareToolPasses -eq $true) "Schema lastStaticValidationSnapshot must record starter prepare tool pass."
Assert-True ($lastStaticValidation.externalStarterKitPrepareToolSupportsExistingManifest -eq $true) "Schema lastStaticValidationSnapshot must record starter prepare existing-manifest rerun proof."
Assert-True ($lastStaticValidation.externalStarterKitTemplateJsonSchemasVersioned -eq $true) "Schema lastStaticValidationSnapshot must record versioned starter JSON Schemas."
Assert-True ($lastStaticValidation.externalStarterKitTemplateJsonSchemasParse -eq $true) "Schema lastStaticValidationSnapshot must record starter JSON Schema parse proof."
Assert-True ($lastStaticValidation.externalStarterKitEditorSchemaMappingPresent -eq $true) "Schema lastStaticValidationSnapshot must record editor JSON schema mapping."
Assert-True ($lastStaticValidation.externalStarterKitGeneratorUsesCheckedInTemplates -eq $true) "Schema lastStaticValidationSnapshot must record checked-in starter template generator parity."
Assert-True ([int]$lastStaticValidation.maxBundleBuildAssetCount -eq $maxBundleBuildAssetCount) "Schema lastStaticValidationSnapshot bundle build asset cap drift. Source=$maxBundleBuildAssetCount SchemaLastKnown=$($lastStaticValidation.maxBundleBuildAssetCount)"
Assert-True ($lastStaticValidation.bundleBuildAssetDiscoveryUsesBoundedEnumeration -eq $true) "Schema lastStaticValidationSnapshot must record bounded builder asset discovery."
Assert-True ([int]$lastStaticValidation.maxManagedAssemblyInputCount -eq $maxManagedAssemblyInputCount) "Schema lastStaticValidationSnapshot managed assembly input cap drift. Source=$maxManagedAssemblyInputCount SchemaLastKnown=$($lastStaticValidation.maxManagedAssemblyInputCount)"
Assert-True ([int]$lastStaticValidation.maxStaleAssemblyCleanupScanCount -eq $maxStaleAssemblyCleanupScanCount) "Schema lastStaticValidationSnapshot stale DLL cleanup scan cap drift. Source=$maxStaleAssemblyCleanupScanCount SchemaLastKnown=$($lastStaticValidation.maxStaleAssemblyCleanupScanCount)"
Assert-True ($lastStaticValidation.builderManagedAssemblyInputCapMatchesLoader -eq $true) "Schema lastStaticValidationSnapshot must record builder DLL input cap parity."
Assert-True ($lastStaticValidation.builderSkipsExpensiveValidationDuringOnGUI -eq $true) "Schema lastStaticValidationSnapshot must record shallow OnGUI validation."
Assert-True ($lastStaticValidation.staleDllCleanupUsesBoundedEnumeration -eq $true) "Schema lastStaticValidationSnapshot must record bounded stale DLL cleanup."
Assert-True ($lastStaticValidation.builderRejectsDuplicateManagedAssemblyFileNames -eq $true) "Schema lastStaticValidationSnapshot must record duplicate DLL filename rejection."
Assert-True ($lastStaticValidation.publicPropertyRoutesRequireActiveScope -eq $true) "Schema lastStaticValidationSnapshot must record public property route active-scope ownership."
Assert-True ($lastStaticValidation.subscriptionDisposeRequiresOwnerScope -eq $true) "Schema lastStaticValidationSnapshot must record direct subscription Dispose owner-scope ownership."
Assert-True ($lastStaticValidation.telemetryFacadeRequiresActiveScope -eq $true) "Schema lastStaticValidationSnapshot must record telemetry facade active-scope ownership."
Assert-True ($lastStaticValidation.localizationFacadeRequiresActiveScope -eq $true) "Schema lastStaticValidationSnapshot must record localization facade active-scope ownership."
Assert-True ($lastStaticValidation.saveStateFacadeRequiresActiveScope -eq $true) "Schema lastStaticValidationSnapshot must record save-state facade active-scope ownership."
Assert-True ($lastStaticValidation.saveStateStoreRequiresScopedOrEngineOwner -eq $true) "Schema lastStaticValidationSnapshot must record SaveState store scoped-or-engine owner proof."
if ($lastStaticValidation.PSObject.Properties.Name -contains 'futureCommandAllowedOpcodeCount') {
    Assert-True ([int]$lastStaticValidation.futureCommandAllowedOpcodeCount -eq $allowedOpcodeCsvHexes.Count) "Schema lastStaticValidationSnapshot futureCommandAllowedOpcodeCount drift. Csv=$($allowedOpcodeCsvHexes.Count) SchemaLastKnown=$($lastStaticValidation.futureCommandAllowedOpcodeCount)"
}
if ($lastStaticValidation.PSObject.Properties.Name -contains 'kernelTuningProfileCount') {
    Assert-True ([int]$lastStaticValidation.kernelTuningProfileCount -eq $kernelTuningCsvHexes.Count) "Schema lastStaticValidationSnapshot kernelTuningProfileCount drift. Csv=$($kernelTuningCsvHexes.Count) SchemaLastKnown=$($lastStaticValidation.kernelTuningProfileCount)"
}

$lastStaticPayloadSizes = @{
    modPlayerSpawnedEventSizeBytes = $modPlayerSpawnedEventSize
    modBiomeChangedEventSizeBytes = $modBiomeChangedEventSize
    modAupCommandSizeBytes = $modAupCommandSize
    modAupResponseSizeBytes = $modAupResponseSize
    modRenderInstanceCommandSizeBytes = $modRenderInstanceCommandSize
    modRaycastResultPayloadSizeBytes = $modRaycastResultPayloadSize
    modInteractionRejectedPayloadSizeBytes = $modInteractionRejectedPayloadSize
    modCriticalMemoryEvictionPayloadSizeBytes = $modCriticalMemoryEvictionPayloadSize
}

foreach ($propertyName in $lastStaticPayloadSizes.Keys) {
    Assert-True ($lastStaticValidation.PSObject.Properties.Name -contains $propertyName) "Schema lastStaticValidationSnapshot missing payload size: $propertyName"
    $lastStaticPayloadSize = $lastStaticValidation.PSObject.Properties[$propertyName].Value
    Assert-True ([int]$lastStaticPayloadSize -eq [int]$lastStaticPayloadSizes[$propertyName]) "Schema lastStaticValidationSnapshot payload size drift for ${propertyName}. Source=$($lastStaticPayloadSizes[$propertyName]) SchemaLastKnown=$lastStaticPayloadSize"
}

$requiredSamplePhrases = @(
    'RequiredAPIVersion": 2',
    'IHectonVersionedMod',
    'HectonAPI.SaveState.GetModString',
    'HectonAPI.SaveState.SetModString',
    'HectonAPI.UI.RegisterSetting',
    'HectonAPI.Events.SubscribeProjected',
    'HectonAPI.Events.Subscribe<ModInteractionRejectedPayload>',
    'OnUnload',
    '_projectionSub?.Dispose()',
    '_rejectSub?.Dispose()',
    'Current API has no SurvivalOverride opcode',
    'No direct player oxygen',
    'No `SignalBus<T>`',
    'Future Kernel Required'
)

foreach ($requiredSamplePhrase in $requiredSamplePhrases) {
    Assert-True ($sampleModSpecText.Contains($requiredSamplePhrase)) "Sample Infinite O2 spec missing required phrase: $requiredSamplePhrase"
}

Assert-True ($specText.Contains('Signal_Audit_Matrix.md')) 'Spec does not link Signal_Audit_Matrix.md.'
Assert-True ($specText.Contains('Command_Audit_Matrix.md')) 'Spec does not link Command_Audit_Matrix.md.'
Assert-True ($specText.Contains('API_Surface_Audit_Matrix.md')) 'Spec does not link API_Surface_Audit_Matrix.md.'
Assert-True ($specText.Contains('Payload_Layout_Audit_Matrix.md')) 'Spec does not link Payload_Layout_Audit_Matrix.md.'
Assert-True ($specText.Contains('Loader_Save_Audit_Matrix.md')) 'Spec does not link Loader_Save_Audit_Matrix.md.'
Assert-True ($specText.Contains('Event_Subscription_Audit_Matrix.md')) 'Spec does not link Event_Subscription_Audit_Matrix.md.'
Assert-True ($specText.Contains('Resource_Content_Audit_Matrix.md')) 'Spec does not link Resource_Content_Audit_Matrix.md.'
Assert-True ($specText.Contains('Change_Control_Checklist.md')) 'Spec does not link Change_Control_Checklist.md.'
Assert-True ($specText.Contains('Sample_InfiniteO2_Mod.md')) 'Spec does not link Sample_InfiniteO2_Mod.md.'
Assert-True ($specText.Contains('Runtime_Verification_Playbook.md')) 'Spec does not link Runtime_Verification_Playbook.md.'
Assert-True ($specText.Contains('Package DLL identity scan')) 'Spec missing package DLL identity scan contract.'
Assert-True ($specText.Contains('shallow OnGUI validation') -and $specText.Contains('duplicate selected DLL filename rejection')) 'Spec missing SDK builder shallow validation and duplicate DLL contract.'
Assert-True ($specText.Contains('Canonical mod IDs')) 'Spec missing canonical mod ID contract.'
Assert-True ($specText.Contains('Scope owner proof')) 'Spec missing ModExecutionScope owner-proof contract.'
Assert-True ($specText.Contains('TriggerSubtitleCue')) 'Spec missing TriggerSubtitleCue reserved alias contract.'
Assert-True ($specText.Contains('HectonGameEvents') -and $specText.Contains('internal-only')) 'Spec missing internal-only HectonGameEvents payload contract.'
Assert-True ($commandAuditText.Contains('TriggerSubtitleCue') -and $commandAuditText.Contains('reserved subtitle alias')) 'Command audit matrix missing reserved TriggerSubtitleCue alias note.'
Assert-True ($runtimePlaybookText.Contains('Pass Criteria')) 'Runtime playbook missing pass criteria.'
Assert-True ($runtimePlaybookText.Contains('GC hot-path projection dispatch is 0 B/frame')) 'Runtime playbook missing GC pass criterion.'
Assert-True ($runtimePlaybookText.Contains('Only `CombatDamageSignal` and `WeatherChangedSignal` reach `SubscribeProjected`')) 'Runtime playbook missing projected-lane pass criterion.'
Assert-True ($runtimePlaybookText.Contains('top-level package DLL')) 'Runtime playbook missing top-level package DLL identity scan evidence.'
Assert-True ($runtimePlaybookText.Contains('canonical mod id')) 'Runtime playbook missing canonical mod id evidence.'
Assert-True ($runtimePlaybookText.Contains('anonymous execution scope')) 'Runtime playbook missing anonymous execution scope rejection evidence.'
Assert-True ($runtimePlaybookText.Contains('TriggerSubtitleCue') -and $runtimePlaybookText.Contains('reserved subtitle')) 'Runtime playbook missing reserved subtitle alias evidence.'
Assert-True ($runtimePlaybookText.Contains('ModRuntimeInfoMembersInternalOnly = True')) 'Runtime playbook missing ModRuntimeInfo internal-only member evidence.'
Assert-True ($runtimePlaybookText.Contains('GameEventPayloadMembersInternalOnly = True')) 'Runtime playbook missing HectonGameEvents internal-only evidence.'
Assert-True ($runtimePlaybookText.Contains('HectonEventBusPublicStaticMembersForbidden = True')) 'Runtime playbook missing HectonEventBus public-static member closure evidence.'
Assert-True ($runtimePlaybookText.Contains('FutureCommandSandboxPublicStaticMembersForbidden = True')) 'Runtime playbook missing FutureCommandSandbox public-static member closure evidence.'
Assert-True ($runtimePlaybookText.Contains('MockModQueueMembersInternalOnly = True')) 'Runtime playbook missing MockModQueue member visibility closure evidence.'
Assert-True ($runtimePlaybookText.Contains('ResourceRegistryRejectsForgedOwner = True')) 'Runtime playbook missing resource registry forged-owner closure evidence.'
Assert-True ($runtimePlaybookText.Contains('RawTextureByteCapEnforcedBeforeRead = True')) 'Runtime playbook missing raw texture byte cap before read evidence.'
Assert-True ($runtimePlaybookText.Contains('RawTextureReadFailsClosed = True')) 'Runtime playbook missing fail-closed raw texture read evidence.'
Assert-True ($runtimePlaybookText.Contains('AssetBundleSuffixFallbackDisabled = True')) 'Runtime playbook missing AssetBundle suffix fallback closure evidence.'
Assert-True ($runtimePlaybookText.Contains('AssetBundleGetAllAssetNamesForbidden = True')) 'Runtime playbook missing AssetBundle.GetAllAssetNames closure evidence.'
Assert-True ($runtimePlaybookText.Contains('ModdingSdkHubPresent = True')) 'Runtime playbook missing SDK Hub presence evidence.'
Assert-True ($runtimePlaybookText.Contains('ModdingSdkHubOpensBuilder = True')) 'Runtime playbook missing SDK Hub builder action evidence.'
Assert-True ($runtimePlaybookText.Contains('ModdingSdkHubPrioritizesExternalStarterKit = True')) 'Runtime playbook missing SDK Hub external starter priority evidence.'
Assert-True ($runtimePlaybookText.Contains('ModdingSdkHubGatesLegacyBuilder = True')) 'Runtime playbook missing SDK Hub legacy builder gate evidence.'
Assert-True ($runtimePlaybookText.Contains('ModBuilderMenuIsInternalLegacy = True')) 'Runtime playbook missing internal legacy builder menu evidence.'
Assert-True ($runtimePlaybookText.Contains('ModdingSdkHubLinksCoreDocs = True')) 'Runtime playbook missing SDK Hub docs link evidence.'
Assert-True ($runtimePlaybookText.Contains('ModdingSdkHubRunsStaticValidator = True')) 'Runtime playbook missing SDK Hub validator action evidence.'
Assert-True ($runtimePlaybookText.Contains('ModdingSdkHubRunsStaticValidatorAsync = True')) 'Runtime playbook missing SDK Hub async validator execution evidence.'
Assert-True ($runtimePlaybookText.Contains('ModdingSdkHubShowsValidatorFailuresAsErrors = True')) 'Runtime playbook missing SDK Hub validator failure error UI evidence.'
Assert-True ($runtimePlaybookText.Contains('ModdingSdkHubOpensStarterWorkbench = True')) 'Runtime playbook missing SDK Hub starter workbench action evidence.'
Assert-True ($runtimePlaybookText.Contains('ModdingSdkHubShowsEnvelopeOnlyBoundary = True')) 'Runtime playbook missing SDK Hub envelope-only boundary evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitWorkbenchPresent = True')) 'Runtime playbook missing starter workbench presence evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitWorkbenchUsesIdentityTool = True')) 'Runtime playbook missing starter workbench identity tool evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitWorkbenchUsesPrepareTool = True')) 'Runtime playbook missing starter workbench prepare tool evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitWorkbenchCanRefreshStarterKit = True')) 'Runtime playbook missing starter workbench generator reuse evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitWorkbenchListsOpcodes = True')) 'Runtime playbook missing starter workbench opcode discovery evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitWorkbenchShowsReviewSummary = True')) 'Runtime playbook missing starter workbench review summary evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitWorkbenchShowsEnvelopeBoundary = True')) 'Runtime playbook missing starter workbench envelope-only boundary evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitWorkbenchShowsStarterHealth = True')) 'Runtime playbook missing starter workbench health panel evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitWorkbenchShowsCapabilityMatrix = True')) 'Runtime playbook missing starter workbench capability matrix evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitWorkbenchRequiredFileListMatchesValidator = True')) 'Runtime playbook missing starter workbench required-file list parity evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitWorkbenchRunsStructureValidator = True')) 'Runtime playbook missing starter workbench structure validator evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitWorkbenchLinksCoreDocs = True')) 'Runtime playbook missing starter workbench doc link evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitWorkbenchOpensVsCodeTasks = True')) 'Runtime playbook missing starter workbench VS Code task/settings evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitWorkbenchRunsToolsAsync = True')) 'Runtime playbook missing starter workbench async tool evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitWorkbenchShowsToolFailuresAsErrors = True')) 'Runtime playbook missing starter workbench tool failure error UI evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitWorkbenchShowsReviewFreshness = True')) 'Runtime playbook missing starter workbench review freshness evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitWorkbenchShowsGraphContractPreview = True')) 'Runtime playbook missing starter workbench graph contract preview evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitWorkbenchGeneratesGraphNodeSnippet = True')) 'Runtime playbook missing starter workbench graph node snippet evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitWorkbenchHasGraphOpcodePicker = True')) 'Runtime playbook missing starter workbench graph opcode picker evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitWorkbenchEditsGraphNodeParameters = True')) 'Runtime playbook missing starter workbench graph node parameter evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitWorkbenchAppliesGraphNodeSnippet = True')) 'Runtime playbook missing starter workbench graph node snippet apply evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitWorkbenchGeneratesAuthoringSnippets = True')) 'Runtime playbook missing starter workbench settings/locale snippet evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitWorkbenchAppliesAuthoringSnippets = True')) 'Runtime playbook missing starter workbench settings/locale snippet apply evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitWorkbenchBuildsSubmissionPackage = True')) 'Runtime playbook missing starter workbench submission package evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitWorkbenchShowsSubmissionPackageStatus = True')) 'Runtime playbook missing starter workbench submission package status evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitWorkbenchShowsSubmissionZipIntegrity = True')) 'Runtime playbook missing starter workbench submission zip integrity evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitWorkbenchUsesCaseExactSubmissionZipIntegrity = True')) 'Runtime playbook missing starter workbench case-exact submission zip integrity evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitWorkbenchReportsReservedFolderCaseVariants = True')) 'Runtime playbook missing starter workbench reserved folder case-variant evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitWorkbenchRunsDoctor = True')) 'Runtime playbook missing starter workbench package doctor evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitWorkbenchShowsAuthoringDataPreview = True')) 'Runtime playbook missing starter workbench authoring data preview evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitWorkbenchShowsContentAssetPreview = True')) 'Runtime playbook missing starter workbench content asset preview evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitWorkbenchGeneratesAssetEntrySnippet = True')) 'Runtime playbook missing starter workbench content asset snippet generation evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitWorkbenchAppliesAssetEntrySnippet = True')) 'Runtime playbook missing starter workbench content asset apply evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitWorkbenchConfiguresManifestContract = True')) 'Runtime playbook missing starter workbench manifest contract evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitWorkbenchConfiguresDependencies = True')) 'Runtime playbook missing starter workbench dependency contract evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitWorkbenchChecksRootLauncher = True')) 'Runtime playbook missing starter workbench root launcher evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitWorkbenchUsesBoundedPreviewReads = True')) 'Runtime playbook missing starter workbench bounded preview-read evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitWorkbenchUsesStreamingCappedPreviewReads = True')) 'Runtime playbook missing starter workbench streaming capped preview-read evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitWorkbenchRejectsInvalidUtf8PreviewReads = True')) 'Runtime playbook missing starter workbench strict UTF-8 preview-read evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitWritesRootLauncher = True')) 'Runtime playbook missing starter root launcher generator evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitValidatorChecksRootLauncher = True')) 'Runtime playbook missing starter validator root launcher evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitRootLauncherPasses = True')) 'Runtime playbook missing starter root launcher pass evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitGeneratorPresent = True')) 'Runtime playbook missing external starter kit generator evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitWritesAuthoringManifest = True')) 'Runtime playbook missing external starter kit authoring manifest evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitWritesRuntimeManifest = True')) 'Runtime playbook missing external starter kit runtime manifest evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitWritesFolderReadmes = True')) 'Runtime playbook missing external starter kit folder README evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitWritesCapabilityGuide = True')) 'Runtime playbook missing external starter kit capability guide evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitCopiesOpcodeReferences = True')) 'Runtime playbook missing external starter kit opcode reference evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitDocumentsNoUnityProjectRequirement = True')) 'Runtime playbook missing no-full-Unity-project evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitDocumentsEnvelopeOnlyBoundary = True')) 'Runtime playbook missing external starter kit envelope-only evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitWritesLocalStructureValidator = True')) 'Runtime playbook missing external starter kit local validator evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitValidatorChecksRequiredFiles = True')) 'Runtime playbook missing starter validator required-file evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitValidatorChecksCapabilityGuide = True')) 'Runtime playbook missing starter validator capability guide evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitValidatorChecksEnvelopeOnly = True')) 'Runtime playbook missing starter validator envelope-only evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitValidatorChecksManagedEntryDisabled = True')) 'Runtime playbook missing starter validator managed-entry rejection evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitValidatorChecksCanonicalIds = True')) 'Runtime playbook missing starter validator canonical ID evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitValidatorChecksManifestIdParity = True')) 'Runtime playbook missing starter validator manifest ID parity evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitValidatorChecksDependencyIds = True')) 'Runtime playbook missing starter validator dependency ID evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitValidatorChecksGraphOpcodes = True')) 'Runtime playbook missing starter graph opcode allowlist evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitValidatorChecksGraphBudget = True')) 'Runtime playbook missing starter graph budget parity evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitValidatorRejectsInvalidGraphOpcode = True')) 'Runtime playbook missing starter invalid graph opcode rejection evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitWritesReviewManifestBuilder = True')) 'Runtime playbook missing starter review manifest builder evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitWritesIdentityTool = True')) 'Runtime playbook missing starter identity helper evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitWritesPrepareTool = True')) 'Runtime playbook missing starter prepare tool evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitWritesAllowedOpcodeListTool = True')) 'Runtime playbook missing starter allowed opcode list helper evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitAllowedOpcodeListToolPasses = True')) 'Runtime playbook missing starter allowed opcode list helper pass evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitAllowedOpcodeListToolSupportsJson = True')) 'Runtime playbook missing starter allowed opcode list JSON evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitWritesGraphNodeSnippetTool = True')) 'Runtime playbook missing starter graph node snippet helper evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitGraphNodeSnippetToolPasses = True')) 'Runtime playbook missing starter graph node snippet helper pass evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitGraphNodeSnippetToolSupportsJson = True')) 'Runtime playbook missing starter graph node snippet helper JSON evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitGraphNodeSnippetSupportsParameters = True')) 'Runtime playbook missing starter graph node snippet parameter evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitRootLauncherSupportsGraphNodeSnippet = True')) 'Runtime playbook missing starter root launcher graph node snippet evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitRootLauncherSupportsGraphNodeParameters = True')) 'Runtime playbook missing starter root launcher graph node parameter evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitWritesGraphNodeApplyTool = True')) 'Runtime playbook missing starter graph node apply helper evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitGraphNodeApplyToolPasses = True')) 'Runtime playbook missing starter graph node apply helper pass evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitGraphNodeApplyToolRejectsDuplicateWithoutReplace = True')) 'Runtime playbook missing starter graph node apply duplicate rejection evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitRootLauncherSupportsGraphNodeApply = True')) 'Runtime playbook missing starter root launcher graph node apply evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitWritesSettingsRowSnippetTool = True')) 'Runtime playbook missing starter settings row snippet helper evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitSettingsRowSnippetToolPasses = True')) 'Runtime playbook missing starter settings row snippet helper pass evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitSettingsRowSnippetToolSupportsJson = True')) 'Runtime playbook missing starter settings row snippet helper JSON evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitWritesSettingsRowApplyTool = True')) 'Runtime playbook missing starter settings row apply helper evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitSettingsRowApplyToolPasses = True')) 'Runtime playbook missing starter settings row apply helper pass evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitSettingsRowApplyToolRejectsDuplicateWithoutReplace = True')) 'Runtime playbook missing starter settings row apply duplicate rejection evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitWritesLocaleEntrySnippetTool = True')) 'Runtime playbook missing starter locale entry snippet helper evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitLocaleEntrySnippetToolPasses = True')) 'Runtime playbook missing starter locale entry snippet helper pass evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitLocaleEntrySnippetToolSupportsJson = True')) 'Runtime playbook missing starter locale entry snippet helper JSON evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitWritesLocaleEntryApplyTool = True')) 'Runtime playbook missing starter locale entry apply helper evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitLocaleEntryApplyToolPasses = True')) 'Runtime playbook missing starter locale entry apply helper pass evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitLocaleEntryApplyToolRejectsDuplicateWithoutReplace = True')) 'Runtime playbook missing starter locale entry apply duplicate rejection evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitRootLauncherSupportsAuthoringSnippets = True')) 'Runtime playbook missing starter root launcher settings/locale snippet evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitRootLauncherSupportsAuthoringSnippetApply = True')) 'Runtime playbook missing starter root launcher settings/locale apply evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitWritesAssetEntrySnippetTool = True')) 'Runtime playbook missing starter content asset snippet helper evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitAssetEntrySnippetToolPasses = True')) 'Runtime playbook missing starter content asset snippet helper pass evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitAssetEntrySnippetToolSupportsJson = True')) 'Runtime playbook missing starter content asset snippet helper JSON evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitWritesAssetEntryApplyTool = True')) 'Runtime playbook missing starter content asset apply helper evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitAssetEntryApplyToolPasses = True')) 'Runtime playbook missing starter content asset apply pass evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitAssetEntryApplyToolRejectsDuplicateWithoutReplace = True')) 'Runtime playbook missing starter content asset apply duplicate rejection evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitSnippetPathsRequireStrictJsonRelativePaths = True')) 'Runtime playbook missing starter snippet strict JSON path evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitSnippetApplyUsesBoundedJsonReads = True')) 'Runtime playbook missing starter snippet bounded JSON read evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitAssetSnippetRejectsNonPortableAssetPaths = True')) 'Runtime playbook missing starter asset snippet portable path evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitGraphNodeSnippetCapsParametersJson = True')) 'Runtime playbook missing graph node ParametersJson cap evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitRootLauncherSupportsAssetEntrySnippet = True')) 'Runtime playbook missing starter root launcher content asset snippet evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitRootLauncherSupportsAssetEntryApply = True')) 'Runtime playbook missing starter root launcher content asset apply evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitWritesManifestContractTool = True')) 'Runtime playbook missing starter manifest contract helper evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitManifestContractToolPasses = True')) 'Runtime playbook missing starter manifest contract helper pass evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitManifestContractRejectsUnknownCapability = True')) 'Runtime playbook missing starter manifest contract unknown capability rejection evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitWritesFirstModTool = True')) 'Runtime playbook missing starter first playable mod helper evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitWritesInstallLocalTool = True')) 'Runtime playbook missing starter local discovery install helper evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitWritesDiagnoseLocalTool = True')) 'Runtime playbook missing starter local Mods diagnosis helper evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitLocalInstallRequiresExactReviewProof = True')) 'Runtime playbook missing starter local install exact review proof evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitDiagnoseLocalRequiresExactReviewProof = True')) 'Runtime playbook missing starter local diagnosis exact review proof evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitDiagnoseLocalUsesRecursiveManifestDiscovery = True')) 'Runtime playbook missing recursive local Mods manifest discovery evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitDiagnoseLocalChecksDependencyGraph = True')) 'Runtime playbook missing local Mods dependency graph diagnosis evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitWritesDependenciesTool = True')) 'Runtime playbook missing starter dependency helper evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitDependencyToolMirrorsBothManifests = True')) 'Runtime playbook missing starter dependency helper manifest parity evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitDependenciesToolWritesStrictUtf8NoBom = True')) 'Runtime playbook missing starter dependency helper strict UTF-8 no-BOM evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitWritesDoctorTool = True')) 'Runtime playbook missing starter package doctor helper evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitDoctorToolIsReadOnly = True')) 'Runtime playbook missing starter package doctor read-only evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitDoctorVerifiesSubmissionZipContents = True')) 'Runtime playbook missing starter package doctor submission zip content evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitDoctorRejectsUnsafeZipEntries = True')) 'Runtime playbook missing starter package doctor unsafe zip entry evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitDoctorUsesCaseExactSubmissionZipIntegrity = True')) 'Runtime playbook missing starter package doctor case-exact submission zip integrity evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitDoctorRejectsReservedTopLevelCaseVariants = True')) 'Runtime playbook missing starter package doctor reserved top-level case-variant evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitDoctorFailsNonReadyExit = True')) 'Runtime playbook missing starter package doctor non-ready nonzero exit evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitRootLauncherPreservesToolExitCodes = True')) 'Runtime playbook missing starter root launcher delegated exit-code preservation evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitReviewManifestRejectsCaseFoldSourceDuplicates = True')) 'Runtime playbook missing starter review manifest case-fold duplicate rejection evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitValidatorRequiresExactPathCasing = True')) 'Runtime playbook missing starter validator exact path casing evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitRejectsReservedTopLevelCaseVariants = True')) 'Runtime playbook missing reserved starter top-level case-variant rejection evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitReviewManifestUsesExactReservedOutputFolders = True')) 'Runtime playbook missing exact reserved output folder evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitPackageProofRequiresLowercaseSha256 = True')) 'Runtime playbook missing lowercase SHA-256 package proof evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitRootLauncherSupportsManifestContract = True')) 'Runtime playbook missing starter root launcher manifest contract evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitRootLauncherSupportsCapabilities = True')) 'Runtime playbook missing starter root launcher capability guide evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitRootLauncherUsesStrictCapabilityGuideRead = True')) 'Runtime playbook missing starter root launcher strict capability guide read evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitHubScriptsRequireCheckedInTemplates = True')) 'Runtime playbook missing starter Hub checked-in script template evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitHubExecutableFallbackBodiesRemoved = True')) 'Runtime playbook missing starter Hub executable fallback body removal evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitWritesSubmissionPackageTool = True')) 'Runtime playbook missing starter submission package helper evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitSubmissionPackagePreservesPreviousOutputUntilSuccess = True')) 'Runtime playbook missing starter submission package atomic handoff evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitSubmissionPackageRefreshesTimestampAfterReplace = True')) 'Runtime playbook missing starter submission package timestamp freshness evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitSubmissionPackageToolPasses = True')) 'Runtime playbook missing starter submission package helper pass evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitSubmissionPackageIncludesReviewManifest = True')) 'Runtime playbook missing starter submission package review manifest evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitSubmissionPackageUsesCaseExactSourceEntries = True')) 'Runtime playbook missing starter submission package exact source entry evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitRootLauncherSupportsSubmissionPackage = True')) 'Runtime playbook missing starter root launcher submission package evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitRootLauncherSupportsFirstMod = True')) 'Runtime playbook missing starter root launcher first-mod evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitRootLauncherSupportsInstallLocal = True')) 'Runtime playbook missing starter root launcher install-local evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitRootLauncherSupportsDiagnoseLocal = True')) 'Runtime playbook missing starter root launcher diagnose-local evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitRootLauncherSupportsDoctor = True')) 'Runtime playbook missing starter root launcher doctor evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitRootLauncherSupportsDependencies = True')) 'Runtime playbook missing starter root launcher dependencies evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitIdentityToolValidatesCanonicalId = True')) 'Runtime playbook missing starter identity helper canonical ID evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitValidatorChecksSemver = True')) 'Runtime playbook missing starter semver validation evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitValidatorChecksManifestIdentityTextParity = True')) 'Runtime playbook missing starter identity text parity evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitIdentityToolRejectsInvalidVersion = True')) 'Runtime playbook missing starter invalid version rejection evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitIdentityToolRollsBackBothManifestsOnValidationFailure = True')) 'Runtime playbook missing starter identity rollback evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitToolsAvoidNestedPowerShell = True')) 'Runtime playbook missing starter cross-platform tool chaining evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitToolsUsePortableJoinPath = True')) 'Runtime playbook missing starter portable path composition evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitWritesJsonSchemas = True')) 'Runtime playbook missing starter JSON Schema output evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitGeneratorUsesCheckedInTemplates = True')) 'Runtime playbook missing checked-in starter template generator parity evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitValidatorChecksJsonSchemas = True')) 'Runtime playbook missing starter JSON Schema validator evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitValidatorChecksEditorSchemaMappings = True')) 'Runtime playbook missing starter exact editor schema mapping evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitWritesVsCodeTasks = True')) 'Runtime playbook missing starter VS Code task output evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitVsCodeTasksPresent = True')) 'Runtime playbook missing versioned starter VS Code task evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitVsCodeTasksSupportDisabledAndReplace = True')) 'Runtime playbook missing starter VS Code disabled-node and replace task evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitVsCodeTasksSupportLocalInstall = True')) 'Runtime playbook missing starter VS Code local discovery install task evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitVsCodeTasksSupportLocalDiagnose = True')) 'Runtime playbook missing starter VS Code local Mods diagnosis task evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitVsCodeTasksSupportDependencies = True')) 'Runtime playbook missing starter VS Code dependency task evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitVsCodeTasksSupportDoctor = True')) 'Runtime playbook missing starter VS Code package doctor task evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitValidatorChecksVsCodeTasks = True')) 'Runtime playbook missing starter VS Code task validation evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitValidatorChecksSettingsAndLocaleContracts = True')) 'Runtime playbook missing starter settings/locale contract validation evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitValidatorChecksAssetManifestContracts = True')) 'Runtime playbook missing starter content asset manifest validation evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitValidatorChecksManifestCapabilities = True')) 'Runtime playbook missing starter manifest capability/budget validation evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitTemplateVersioned = True')) 'Runtime playbook missing versioned starter kit evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitTemplatePassesLocalValidator = True')) 'Runtime playbook missing starter template local validator evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitTemplateReferenceCsvsMatchSource = True')) 'Runtime playbook missing starter template reference CSV source parity evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitReviewManifestPasses = True')) 'Runtime playbook missing starter review manifest pass evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitReviewManifestHashesFiles = True')) 'Runtime playbook missing starter review manifest hash evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitReviewManifestIncludesIdentity = True')) 'Runtime playbook missing starter review manifest identity evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitReviewManifestIdentityMatchesRuntimeManifest = True')) 'Runtime playbook missing starter review manifest identity parity evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitReviewManifestExcludesReports = True')) 'Runtime playbook missing starter review manifest output-exclusion evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitReviewManifestHasLimits = True')) 'Runtime playbook missing starter review manifest count/byte limit evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitReviewManifestRejectsOversizedFile = True')) 'Runtime playbook missing starter review manifest oversized-file rejection evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitIdentityToolPasses = True')) 'Runtime playbook missing starter identity helper pass evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitPrepareToolPasses = True')) 'Runtime playbook missing starter prepare tool pass evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitPrepareToolSupportsExistingManifest = True')) 'Runtime playbook missing starter prepare existing-manifest rerun evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitTemplateJsonSchemasVersioned = True')) 'Runtime playbook missing starter JSON Schema template evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitTemplateJsonSchemasParse = True')) 'Runtime playbook missing starter JSON Schema parse evidence.'
Assert-True ($runtimePlaybookText.Contains('ExternalStarterKitEditorSchemaMappingPresent = True')) 'Runtime playbook missing starter editor schema mapping evidence.'
Assert-True ($runtimePlaybookText.Contains('MaxBundleBuildAssetCount = 512')) 'Runtime playbook missing builder bundle asset cap evidence.'
Assert-True ($runtimePlaybookText.Contains('BundleBuildAssetDiscoveryUsesBoundedEnumeration = True')) 'Runtime playbook missing bounded builder asset discovery evidence.'
Assert-True ($runtimePlaybookText.Contains('MaxManagedAssemblyInputCount = 32')) 'Runtime playbook missing builder managed assembly input cap evidence.'
Assert-True ($runtimePlaybookText.Contains('MaxStaleAssemblyCleanupScanCount = 128')) 'Runtime playbook missing builder stale DLL cleanup scan cap evidence.'
Assert-True ($runtimePlaybookText.Contains('BuilderManagedAssemblyInputCapMatchesLoader = True')) 'Runtime playbook missing builder DLL cap parity evidence.'
Assert-True ($runtimePlaybookText.Contains('BuilderSkipsExpensiveValidationDuringOnGUI = True')) 'Runtime playbook missing shallow OnGUI validation evidence.'
Assert-True ($runtimePlaybookText.Contains('StaleDllCleanupUsesBoundedEnumeration = True')) 'Runtime playbook missing bounded stale DLL cleanup evidence.'
Assert-True ($runtimePlaybookText.Contains('BuilderRejectsDuplicateManagedAssemblyFileNames = True')) 'Runtime playbook missing duplicate DLL filename rejection evidence.'
Assert-True ($runtimePlaybookText.Contains('SaveStateStoreRequiresScopedOrEngineOwner = True')) 'Runtime playbook missing SaveState store scoped-or-engine owner proof evidence.'
Assert-True ($runtimePlaybookText.Contains('ManifestByteCapEnforcedBeforeRead = True')) 'Runtime playbook missing manifest byte cap before read evidence.'
Assert-True ($runtimePlaybookText.Contains('ManifestDiscoveryMaxCount = 64')) 'Runtime playbook missing manifest discovery cap evidence.'
Assert-True ($runtimePlaybookText.Contains('ManifestDiscoveryUsesBoundedEnumeration = True')) 'Runtime playbook missing bounded manifest discovery evidence.'
Assert-True ($runtimePlaybookText.Contains('ManagedAssemblyIdentityScanUsesBoundedEnumeration = True')) 'Runtime playbook missing bounded managed assembly identity scan evidence.'
Assert-True ($runtimePlaybookText.Contains('MaxTopLevelManagedAssemblyCount = 32')) 'Runtime playbook missing top-level managed assembly cap evidence.'
Assert-True ($runtimePlaybookText.Contains('ExcessTopLevelManagedAssembliesDisablePackage = True')) 'Runtime playbook missing managed assembly over-cap disable evidence.'
Assert-True ($runtimePlaybookText.Contains('MaxTopLevelBundleCount = 4')) 'Runtime playbook missing top-level bundle cap evidence.'
Assert-True ($runtimePlaybookText.Contains('MaxLocalizationFileCount = 16')) 'Runtime playbook missing top-level localization cap evidence.'
Assert-True ($runtimePlaybookText.Contains('TopLevelContentDiscoveryUsesBoundedEnumeration = True')) 'Runtime playbook missing bounded top-level content discovery evidence.'
Assert-True ($loaderSaveAuditText.Contains('Manifest byte cap') -and $loaderSaveAuditText.Contains('32768')) 'Loader/save audit missing manifest byte cap contract.'
Assert-True ($loaderSaveAuditText.Contains('Manifest discovery cap') -and $loaderSaveAuditText.Contains('64')) 'Loader/save audit missing manifest discovery cap contract.'
Assert-True ($loaderSaveAuditText.Contains('Package DLL identity scan') -and $loaderSaveAuditText.Contains('max `32`')) 'Loader/save audit missing top-level DLL identity scan cap contract.'
Assert-True ($loaderSaveAuditText.Contains('caps selected managed DLLs at the loader') -and $loaderSaveAuditText.Contains('OnGUI')) 'Loader/save audit missing SDK builder DLL cap and shallow validation contract.'
Assert-True ($loaderSaveAuditText.Contains('Legacy bundle discovery cap') -and $loaderSaveAuditText.Contains('`4`')) 'Loader/save audit missing top-level bundle discovery cap contract.'
Assert-True ($loaderSaveAuditText.Contains('Legacy localization discovery cap') -and $loaderSaveAuditText.Contains('`16`')) 'Loader/save audit missing top-level localization discovery cap contract.'
Assert-True ($specText.Contains('Manifest byte cap') -and $specText.Contains('File.ReadAllText')) 'Mod API spec missing manifest byte cap contract.'
Assert-True ($specText.Contains('Manifest discovery cap') -and $specText.Contains('64')) 'Mod API spec missing manifest discovery cap contract.'
Assert-True ($specText.Contains('max `32` top-level `.dll` files') -and $specText.Contains('Legacy bundle discovery cap') -and $specText.Contains('Legacy localization discovery cap')) 'Mod API spec missing bounded top-level package file discovery contract.'
Assert-True ($runtimePlaybookText.Contains('HectonModHooksPublicStaticMembersForbidden = True')) 'Runtime playbook missing HectonModHooks public-static member closure evidence.'
Assert-True ($runtimePlaybookText.Contains('ModCommandDispatcherPublicStaticMembersForbidden = True')) 'Runtime playbook missing ModCommandDispatcher public-static member closure evidence.'
Assert-True ($runtimePlaybookText.Contains('ProjectedEventBridgeRejectsAnonymousSubscribers = True')) 'Runtime playbook missing projected event bridge anonymous-subscriber rejection evidence.'
Assert-True ($runtimePlaybookText.Contains('EventChannelsRejectAnonymousSubscribers = True')) 'Runtime playbook missing private event channel anonymous-subscriber rejection evidence.'
Assert-True ($runtimePlaybookText.Contains('ProjectedEventCapUsesSmoothContinuousCurve = True')) 'Runtime playbook missing projected event cap curve validator evidence.'
Assert-True ($runtimePlaybookText.Contains('round(lerp(10, 50, smoothstep(saturate(GlobalQualityWeight01))))')) 'Runtime playbook missing smooth continuous projected event cap formula.'
Assert-True ($eventSubscriptionAuditText.Contains('Projected event cap curve') -and $eventSubscriptionAuditText.Contains('smoothstep(saturate(GlobalQualityWeight01))')) 'Event subscription audit missing smooth continuous projected event cap formula.'
Assert-True ($runtimePlaybookText.Contains('Command_Audit_Matrix.md')) 'Runtime playbook does not link Command_Audit_Matrix.md.'
Assert-True ($runtimePlaybookText.Contains('API_Surface_Audit_Matrix.md')) 'Runtime playbook does not link API_Surface_Audit_Matrix.md.'
Assert-True ($runtimePlaybookText.Contains('Payload_Layout_Audit_Matrix.md')) 'Runtime playbook does not link Payload_Layout_Audit_Matrix.md.'
Assert-True ($runtimePlaybookText.Contains('Loader_Save_Audit_Matrix.md')) 'Runtime playbook does not link Loader_Save_Audit_Matrix.md.'
Assert-True ($runtimePlaybookText.Contains('Event_Subscription_Audit_Matrix.md')) 'Runtime playbook does not link Event_Subscription_Audit_Matrix.md.'
Assert-True ($runtimePlaybookText.Contains('Resource_Content_Audit_Matrix.md')) 'Runtime playbook does not link Resource_Content_Audit_Matrix.md.'
Assert-True ($runtimePlaybookText.Contains('Change_Control_Checklist.md')) 'Runtime playbook does not link Change_Control_Checklist.md.'
Assert-True ($runtimePlaybookText.Contains('Sample_InfiniteO2_Mod.md')) 'Runtime playbook does not link Sample_InfiniteO2_Mod.md.'

$result = [pscustomobject]@{
    Status = 'PASS'
    SchemaRevision = $schema.schemaRevision
    SourceSignals = $uniqueSignals.Count
    AllowedProjectedSignals = $allowedSignals.Count
    DeniedByDefaultSignals = $uniqueSignals.Count - $allowedSignals.Count
    AcceptedCommandOpcodes = $acceptedOpcodes.Count
    FutureCommandAllowedOpcodeCount = $allowedOpcodeCsvHexes.Count
    KernelTuningProfileCount = $kernelTuningCsvHexes.Count
    CommandRejectReasons = $rejectReasonNames.Count
    PublicApiSurfaces = $apiSurfaceNames.Count
    PublicApiMethods = $publicApiMethods.Count
    PublicApiProperties = $publicApiProperties.Count
    InternalForbiddenApiMethods = $internalApiMethods.Count
    ModEventDtoSizeBytes = $modEventDtoSize
    ModEventDtoFieldOffsets = $modEventDtoOffsets.Count
    ModCommandSizeBytes = $modCommandSize
    ModPlayerSpawnedEventSizeBytes = $modPlayerSpawnedEventSize
    ModBiomeChangedEventSizeBytes = $modBiomeChangedEventSize
    ModAupCommandSizeBytes = $modAupCommandSize
    ModAupResponseSizeBytes = $modAupResponseSize
    ModRenderInstanceCommandSizeBytes = $modRenderInstanceCommandSize
    ModRaycastResultPayloadSizeBytes = $modRaycastResultPayloadSize
    ModInteractionRejectedPayloadSizeBytes = $modInteractionRejectedPayloadSize
    ModCriticalMemoryEvictionPayloadSizeBytes = $modCriticalMemoryEvictionPayloadSize
    NativeInteractionEventPayloadSizeBytes = $interactionEventPayloadSize
    NativeCraftingEventPayloadSizeBytes = $craftingEventPayloadSize
    CurrentApiVersion = $currentApiVersion
    ManifestFieldCount = $manifestFields.Count
    ManifestMaxBytes = $manifestMaxBytes
    ManifestByteCapEnforcedBeforeRead = $manifestByteCapEnforcedBeforeRead
    ManifestDiscoveryMaxCount = $manifestDiscoveryMaxCount
    ManifestDiscoveryUsesBoundedEnumeration = $manifestDiscoveryUsesBoundedEnumeration
    SdkBuilderManifestFieldCount = $modBuilderManifestFields.Count
    SdkBuilderManifestMatchesLoader = $true
    ModMetadataFieldCount = $modMetadataFields.Count
    ModRuntimeInfoFieldCount = $modRuntimeInfoFields.Count
    LifecycleMethodCount = $lifecycleMethods.Count
    SaveStatePublicMethods = $saveStatePublicMethods.Count
    SaveStateStoreRequiresScopedOrEngineOwner = $saveStateStoreRequiresScopedOrEngineOwner
    ModPayloadMaxBytes = $modPayloadMaxBytes
    PublicEventMethodCount = $publicEventMethodNames.Count
    NativeEventKindCount = $nativeEventKindNames.Count
    ProjectedEventKindCountIncludingNone = $projectedEventKindNames.Count
    NativeQueueBridgePublishLaneCount = $nativeBridgePublishLanes.Count
    MaxEventDispatchDepth = $maxDispatchDepth
    CallbackWatchdogMilliseconds = $callbackWatchdogMilliseconds
    PublishRejectsEngineOwnedPayloads = [bool]$schema.eventSubscriptionAudit.publishEngineOwnedPayloadsForbidden
    PublicEventFacadesRequireScopeBeforeEnvelopeOnly = [bool]$schema.eventSubscriptionAudit.publicEventFacadesRequireScopeBeforeEnvelopeOnly
    HectonEventBusPublicStaticMembersForbidden = $hectonEventBusPublicStaticMembersForbidden
    HectonModHooksPublicStaticMembersForbidden = $hectonModHooksPublicStaticMembersForbidden
    GameEventPayloadMembersInternalOnly = $gameEventPayloadMembersInternalOnly
    ProjectedEventBridgeRejectsAnonymousSubscribers = $projectedEventBridgeRejectsAnonymousSubscribers
    EventChannelsRejectAnonymousSubscribers = $eventChannelsRejectAnonymousSubscribers
    ProjectedEventCapUsesSmoothContinuousCurve = $projectedEventCapUsesSmoothContinuousCurve
    ModApiSpecCurrentClosureRevisionMatchesSchema = $modApiSpecCurrentClosureRevisionMatchesSchema
    EngineOwnedPublishForbiddenPayloadCount = $engineOwnedPublishPayloads.Count
    FutureCommandSandboxPublicStaticMembersForbidden = $futureCommandSandboxPublicStaticMembersForbidden
    MockModQueueMembersInternalOnly = $mockModQueueMembersInternalOnly
    ModCommandDispatcherPublicStaticMembersForbidden = $modCommandDispatcherPublicStaticMembersForbidden
    FutureCommandSandboxConstantsPublic = $lastStaticValidation.futureCommandSandboxConstantsPublic
    FutureCommandEnvelopeExposesSizeBytes = $lastStaticValidation.futureCommandEnvelopeExposesSizeBytes
    LegacyCommandFacadesRequireActiveScope = $lastStaticValidation.legacyCommandFacadesRequireActiveScope
    ModRegistryListenersUsePrivateAdapters = [bool]$lastStaticValidation.modRegistryListenersUsePrivateAdapters
    ManagedAssemblyIdentityReservedNamesBlocked = [bool]$lastStaticValidation.managedAssemblyIdentityReservedNamesBlocked
    ManagedAssemblyIdentityScansAllPackageDlls = [bool]$lastStaticValidation.managedAssemblyIdentityScansAllPackageDlls
    ManagedAssemblyIdentityScanUsesBoundedEnumeration = $managedAssemblyIdentityScanUsesBoundedEnumeration
    MaxTopLevelManagedAssemblyCount = $maxTopLevelManagedAssemblyCount
    ExcessTopLevelManagedAssembliesDisablePackage = $excessTopLevelManagedAssembliesDisablePackage
    MaxTopLevelBundleCount = $maxTopLevelBundleCount
    MaxLocalizationFileCount = $maxLocalizationFileCount
    TopLevelContentDiscoveryUsesBoundedEnumeration = $topLevelPackageFileDiscoveryUsesBoundedEnumeration
    ModIdentifierCanonicalForm = [bool]$lastStaticValidation.modIdentifierCanonicalForm
    DependencyIdentifiersValidated = [bool]$lastStaticValidation.dependencyIdentifiersValidated
    EntryAssemblyPathRestrictedToFileName = [bool]$lastStaticValidation.entryAssemblyPathRestrictedToFileName
    ModExecutionScopeRejectsAnonymousOwner = [bool]$lastStaticValidation.modExecutionScopeRejectsAnonymousOwner
    FutureSubtitleCueAliasesReserved = [bool]$lastStaticValidation.futureSubtitleCueAliasesReserved
    EditorRuntimeOpcodeTunersRejectReservedSubtitleAliases = $editorRuntimeOpcodeTunersRejectReservedSubtitleAliases
    ModRuntimeInfoMembersInternalOnly = $modRuntimeInfoMembersInternalOnly
    NativeBytePayloadLayoutsChecked = [bool]$lastStaticValidation.nativeBytePayloadLayoutsChecked
    ProjectionBridgeSignals = ($uniqueBridgeSignals -join ',')
    ContractIndexPath = $schema.staticValidation.contractIndex
    AuditPath = $schema.sourceSignalInventory.auditPath
    CommandAuditPath = $schema.commandApi.auditPath
    ApiSurfaceAuditPath = $schema.apiSurfaceAudit.auditPath
    PayloadLayoutAuditPath = $schema.payloadLayoutAudit.auditPath
    LoaderSaveAuditPath = $schema.loaderSaveAudit.auditPath
    EventSubscriptionAuditPath = $schema.eventSubscriptionAudit.auditPath
    ChangeControlChecklistPath = $schema.staticValidation.changeControlChecklist
    SampleModSpecPath = $schema.staticValidation.sampleModSpec
    ResourceContentAuditPath = $schema.resourceContentAudit.auditPath
    PublicResourceMethodCount = $publicResourceMethodNames.Count
    ResourceKindCount = $resourceKindNames.Count
    ResourceRegistryCapacity = $resourceRegistryCapacity
    InternalAssetLoaderCount = $internalAssetLoaderNames.Count
    RawTextureMaxBytes = $rawTextureMaxBytes
    RawTextureMaxDimension = $rawTextureMaxDimension
    RawTextureByteCapEnforcedBeforeRead = $rawTextureByteCapEnforcedBeforeRead
    RawTextureReadFailsClosed = $rawTextureReadFailsClosed
    AssetBundleSuffixFallbackDisabled = $assetBundleSuffixFallbackDisabled
    AssetBundleGetAllAssetNamesForbidden = $assetBundleGetAllAssetNamesForbidden
    ModdingSdkHubPresent = $moddingSdkHubPresent
    ModdingSdkHubOpensBuilder = $moddingSdkHubOpensBuilder
    ModdingSdkHubOpensStarterWorkbench = $moddingSdkHubOpensStarterWorkbench
    ModdingSdkHubPrioritizesExternalStarterKit = $moddingSdkHubPrioritizesExternalStarterKit
    ModdingSdkHubGatesLegacyBuilder = $moddingSdkHubGatesLegacyBuilder
    ModBuilderMenuIsInternalLegacy = $modBuilderMenuIsInternalLegacy
    ModdingSdkHubLinksCoreDocs = $moddingSdkHubLinksCoreDocs
    ModdingSdkHubRunsStaticValidator = $moddingSdkHubRunsStaticValidator
    ModdingSdkHubRunsStaticValidatorAsync = $moddingSdkHubRunsStaticValidatorAsync
    ModdingSdkHubShowsValidatorFailuresAsErrors = $moddingSdkHubShowsValidatorFailuresAsErrors
    ModdingSdkHubShowsEnvelopeOnlyBoundary = $moddingSdkHubShowsEnvelopeOnlyBoundary
    ExternalStarterKitWorkbenchPresent = $externalStarterKitWorkbenchPresent
    ExternalStarterKitWorkbenchUsesIdentityTool = $externalStarterKitWorkbenchUsesIdentityTool
    ExternalStarterKitWorkbenchUsesPrepareTool = $externalStarterKitWorkbenchUsesPrepareTool
    ExternalStarterKitWorkbenchCanRefreshStarterKit = $externalStarterKitWorkbenchCanRefreshStarterKit
    ExternalStarterKitWorkbenchListsOpcodes = $externalStarterKitWorkbenchListsOpcodes
    ExternalStarterKitWorkbenchShowsReviewSummary = $externalStarterKitWorkbenchShowsReviewSummary
    ExternalStarterKitWorkbenchShowsEnvelopeBoundary = $externalStarterKitWorkbenchShowsEnvelopeBoundary
    ExternalStarterKitWorkbenchShowsStarterHealth = $externalStarterKitWorkbenchShowsStarterHealth
    ExternalStarterKitWorkbenchShowsCapabilityMatrix = $externalStarterKitWorkbenchShowsCapabilityMatrix
    ExternalStarterKitWorkbenchRequiredFileListMatchesValidator = $externalStarterKitWorkbenchUsesCurrentSchemaHealthList
    ExternalStarterKitWorkbenchRunsStructureValidator = $externalStarterKitWorkbenchRunsStructureValidator
    ExternalStarterKitWorkbenchLinksCoreDocs = $externalStarterKitWorkbenchLinksCoreDocs
    ExternalStarterKitWorkbenchOpensVsCodeTasks = $externalStarterKitWorkbenchOpensVsCodeTasks
    ExternalStarterKitWorkbenchRunsToolsAsync = $externalStarterKitWorkbenchRunsToolsAsync
    ExternalStarterKitWorkbenchShowsToolFailuresAsErrors = $externalStarterKitWorkbenchShowsToolFailuresAsErrors
    ExternalStarterKitWorkbenchShowsReviewFreshness = $externalStarterKitWorkbenchShowsReviewFreshness
    ExternalStarterKitWorkbenchShowsGraphContractPreview = $externalStarterKitWorkbenchShowsGraphContractPreview
    ExternalStarterKitWorkbenchGeneratesGraphNodeSnippet = $externalStarterKitWorkbenchGeneratesGraphNodeSnippet
    ExternalStarterKitWorkbenchHasGraphOpcodePicker = $externalStarterKitWorkbenchHasGraphOpcodePicker
    ExternalStarterKitWorkbenchEditsGraphNodeParameters = $externalStarterKitWorkbenchEditsGraphNodeParameters
    ExternalStarterKitWorkbenchAppliesGraphNodeSnippet = $externalStarterKitWorkbenchAppliesGraphNodeSnippet
    ExternalStarterKitWorkbenchGeneratesAuthoringSnippets = $externalStarterKitWorkbenchGeneratesAuthoringSnippets
    ExternalStarterKitWorkbenchAppliesAuthoringSnippets = $externalStarterKitWorkbenchAppliesAuthoringSnippets
    ExternalStarterKitWorkbenchBuildsSubmissionPackage = $externalStarterKitWorkbenchBuildsSubmissionPackage
    ExternalStarterKitWorkbenchShowsSubmissionPackageStatus = $externalStarterKitWorkbenchShowsSubmissionPackageStatus
    ExternalStarterKitWorkbenchShowsSubmissionZipIntegrity = $externalStarterKitWorkbenchShowsSubmissionZipIntegrity
    ExternalStarterKitWorkbenchUsesCaseExactSubmissionZipIntegrity = $externalStarterKitWorkbenchUsesCaseExactSubmissionZipIntegrity
    ExternalStarterKitWorkbenchReportsReservedFolderCaseVariants = $externalStarterKitWorkbenchReportsReservedFolderCaseVariants
    ExternalStarterKitWorkbenchRunsDoctor = $externalStarterKitWorkbenchRunsDoctor
    ExternalStarterKitWorkbenchShowsAuthoringDataPreview = $externalStarterKitWorkbenchShowsAuthoringDataPreview
    ExternalStarterKitWorkbenchShowsContentAssetPreview = $externalStarterKitWorkbenchShowsContentAssetPreview
    ExternalStarterKitWorkbenchGeneratesAssetEntrySnippet = $externalStarterKitWorkbenchGeneratesAssetEntrySnippet
    ExternalStarterKitWorkbenchAppliesAssetEntrySnippet = $externalStarterKitWorkbenchAppliesAssetEntrySnippet
    ExternalStarterKitWorkbenchConfiguresManifestContract = $externalStarterKitWorkbenchConfiguresManifestContract
    ExternalStarterKitWorkbenchConfiguresDependencies = $externalStarterKitWorkbenchConfiguresDependencies
    ExternalStarterKitWorkbenchChecksRootLauncher = $externalStarterKitWorkbenchChecksRootLauncher
    ExternalStarterKitWorkbenchUsesBoundedPreviewReads = $externalStarterKitWorkbenchUsesBoundedPreviewReads
    ExternalStarterKitWorkbenchUsesStreamingCappedPreviewReads = $externalStarterKitWorkbenchUsesStreamingCappedPreviewReads
    ExternalStarterKitWorkbenchRejectsInvalidUtf8PreviewReads = $externalStarterKitWorkbenchRejectsInvalidUtf8PreviewReads
    ExternalStarterKitGeneratorPresent = $externalStarterKitGeneratorPresent
    ExternalStarterKitWritesRootLauncher = $externalStarterKitWritesRootLauncher
    ExternalStarterKitWritesAuthoringManifest = $externalStarterKitWritesAuthoringManifest
    ExternalStarterKitWritesRuntimeManifest = $externalStarterKitWritesRuntimeManifest
    ExternalStarterKitWritesFolderReadmes = $externalStarterKitWritesFolderReadmes
    ExternalStarterKitWritesCapabilityGuide = $externalStarterKitWritesCapabilityGuide
    ExternalStarterKitCopiesOpcodeReferences = $externalStarterKitCopiesOpcodeReferences
    ExternalStarterKitDocumentsNoUnityProjectRequirement = $externalStarterKitDocumentsNoUnityProjectRequirement
    ExternalStarterKitDocumentsEnvelopeOnlyBoundary = $externalStarterKitDocumentsEnvelopeOnlyBoundary
    ExternalStarterKitWritesLocalStructureValidator = $externalStarterKitWritesLocalStructureValidator
    ExternalStarterKitValidatorChecksRequiredFiles = $externalStarterKitValidatorChecksRequiredFiles
    ExternalStarterKitValidatorChecksCapabilityGuide = $externalStarterKitValidatorChecksCapabilityGuide
    ExternalStarterKitValidatorChecksRootLauncher = $externalStarterKitValidatorChecksRootLauncher
    ExternalStarterKitValidatorChecksEnvelopeOnly = $externalStarterKitValidatorChecksEnvelopeOnly
    ExternalStarterKitValidatorChecksManagedEntryDisabled = $externalStarterKitValidatorChecksManagedEntryDisabled
    ExternalStarterKitValidatorChecksCanonicalIds = $externalStarterKitValidatorChecksCanonicalIds
    ExternalStarterKitValidatorChecksManifestIdParity = $externalStarterKitValidatorChecksManifestIdParity
    ExternalStarterKitValidatorChecksDependencyIds = $externalStarterKitValidatorChecksDependencyIds
    ExternalStarterKitValidatorChecksGraphOpcodes = $externalStarterKitValidatorChecksGraphOpcodes
    ExternalStarterKitValidatorChecksGraphBudget = $externalStarterKitValidatorChecksGraphBudget
    ExternalStarterKitValidatorRejectsInvalidGraphOpcode = $externalStarterKitValidatorRejectsInvalidGraphOpcode
    ExternalStarterKitWritesReviewManifestBuilder = $externalStarterKitWritesReviewManifestBuilder
    ExternalStarterKitWritesIdentityTool = $externalStarterKitWritesIdentityTool
    ExternalStarterKitWritesPrepareTool = $externalStarterKitWritesPrepareTool
    ExternalStarterKitWritesAllowedOpcodeListTool = $externalStarterKitWritesAllowedOpcodeListTool
    ExternalStarterKitAllowedOpcodeListToolPasses = $externalStarterKitAllowedOpcodeListToolPasses
    ExternalStarterKitAllowedOpcodeListToolSupportsJson = $externalStarterKitAllowedOpcodeListToolSupportsJson
    ExternalStarterKitWritesGraphNodeSnippetTool = $externalStarterKitWritesGraphNodeSnippetTool
    ExternalStarterKitGraphNodeSnippetToolPasses = $externalStarterKitGraphNodeSnippetToolPasses
    ExternalStarterKitGraphNodeSnippetToolSupportsJson = $externalStarterKitGraphNodeSnippetToolSupportsJson
    ExternalStarterKitGraphNodeSnippetSupportsParameters = $externalStarterKitGraphNodeSnippetSupportsParameters
    ExternalStarterKitRootLauncherSupportsGraphNodeSnippet = $externalStarterKitRootLauncherSupportsGraphNodeSnippet
    ExternalStarterKitRootLauncherSupportsGraphNodeParameters = $externalStarterKitRootLauncherSupportsGraphNodeParameters
    ExternalStarterKitWritesGraphNodeApplyTool = $externalStarterKitWritesGraphNodeApplyTool
    ExternalStarterKitGraphNodeApplyToolPasses = $externalStarterKitGraphNodeApplyToolPasses
    ExternalStarterKitGraphNodeApplyToolRejectsDuplicateWithoutReplace = $externalStarterKitGraphNodeApplyToolRejectsDuplicateWithoutReplace
    ExternalStarterKitRootLauncherSupportsGraphNodeApply = $externalStarterKitRootLauncherSupportsGraphNodeApply
    ExternalStarterKitWritesSettingsRowSnippetTool = $externalStarterKitWritesSettingsRowSnippetTool
    ExternalStarterKitSettingsRowSnippetToolPasses = $externalStarterKitSettingsRowSnippetToolPasses
    ExternalStarterKitSettingsRowSnippetToolSupportsJson = $externalStarterKitSettingsRowSnippetToolSupportsJson
    ExternalStarterKitWritesSettingsRowApplyTool = $externalStarterKitWritesSettingsRowApplyTool
    ExternalStarterKitSettingsRowApplyToolPasses = $externalStarterKitSettingsRowApplyToolPasses
    ExternalStarterKitSettingsRowApplyToolRejectsDuplicateWithoutReplace = $externalStarterKitSettingsRowApplyToolRejectsDuplicateWithoutReplace
    ExternalStarterKitWritesLocaleEntrySnippetTool = $externalStarterKitWritesLocaleEntrySnippetTool
    ExternalStarterKitLocaleEntrySnippetToolPasses = $externalStarterKitLocaleEntrySnippetToolPasses
    ExternalStarterKitLocaleEntrySnippetToolSupportsJson = $externalStarterKitLocaleEntrySnippetToolSupportsJson
    ExternalStarterKitWritesLocaleEntryApplyTool = $externalStarterKitWritesLocaleEntryApplyTool
    ExternalStarterKitLocaleEntryApplyToolPasses = $externalStarterKitLocaleEntryApplyToolPasses
    ExternalStarterKitLocaleEntryApplyToolRejectsDuplicateWithoutReplace = $externalStarterKitLocaleEntryApplyToolRejectsDuplicateWithoutReplace
    ExternalStarterKitWritesAssetEntrySnippetTool = $externalStarterKitWritesAssetEntrySnippetTool
    ExternalStarterKitAssetEntrySnippetToolPasses = $externalStarterKitAssetEntrySnippetToolPasses
    ExternalStarterKitAssetEntrySnippetToolSupportsJson = $externalStarterKitAssetEntrySnippetToolSupportsJson
    ExternalStarterKitWritesAssetEntryApplyTool = $externalStarterKitWritesAssetEntryApplyTool
    ExternalStarterKitAssetEntryApplyToolPasses = $externalStarterKitAssetEntryApplyToolPasses
    ExternalStarterKitAssetEntryApplyToolRejectsDuplicateWithoutReplace = $externalStarterKitAssetEntryApplyToolRejectsDuplicateWithoutReplace
    ExternalStarterKitSnippetPathsRequireStrictJsonRelativePaths = $externalStarterKitSnippetPathsRequireStrictJsonRelativePaths
    ExternalStarterKitSnippetApplyUsesBoundedJsonReads = $externalStarterKitSnippetApplyUsesBoundedJsonReads
    ExternalStarterKitAssetSnippetRejectsNonPortableAssetPaths = $externalStarterKitAssetSnippetRejectsNonPortableAssetPaths
    ExternalStarterKitGraphNodeSnippetCapsParametersJson = $externalStarterKitGraphNodeSnippetCapsParametersJson
    ExternalStarterKitValidatorCapsRootJsonReads = $externalStarterKitValidatorCapsRootJsonReads
    ExternalStarterKitValidatorRejectsNonPortableAssetManifestPaths = $externalStarterKitValidatorRejectsNonPortableAssetManifestPaths
    ExternalStarterKitCoreToolsUseBoundedJsonReads = $externalStarterKitCoreToolsUseBoundedJsonReads
    ExternalStarterKitCoreToolsUseStreamingStrictUtf8JsonReads = $externalStarterKitCoreToolsUseStreamingStrictUtf8JsonReads
    ExternalStarterKitWritesManifestContractTool = $externalStarterKitWritesManifestContractTool
    ExternalStarterKitManifestContractToolPasses = $externalStarterKitManifestContractToolPasses
    ExternalStarterKitManifestContractRejectsUnknownCapability = $externalStarterKitManifestContractRejectsUnknownCapability
    ExternalStarterKitWritesFirstModTool = $externalStarterKitWritesFirstModTool
    ExternalStarterKitWritesInstallLocalTool = $externalStarterKitWritesInstallLocalTool
    ExternalStarterKitWritesDiagnoseLocalTool = $externalStarterKitWritesDiagnoseLocalTool
    ExternalStarterKitLocalInstallRequiresExactReviewProof = $externalStarterKitLocalInstallRequiresExactReviewProof
    ExternalStarterKitDiagnoseLocalRequiresExactReviewProof = $externalStarterKitDiagnoseLocalRequiresExactReviewProof
    ExternalStarterKitDiagnoseLocalUsesRecursiveManifestDiscovery = $externalStarterKitDiagnoseLocalUsesRecursiveManifestDiscovery
    ExternalStarterKitDiagnoseLocalChecksDependencyGraph = $externalStarterKitDiagnoseLocalChecksDependencyGraph
    ExternalStarterKitWritesDependenciesTool = $externalStarterKitWritesDependenciesTool
    ExternalStarterKitDependencyToolMirrorsBothManifests = $externalStarterKitDependencyToolMirrorsBothManifests
    ExternalStarterKitDependenciesToolWritesStrictUtf8NoBom = $externalStarterKitDependenciesToolWritesStrictUtf8NoBom
    ExternalStarterKitWritesDoctorTool = $externalStarterKitWritesDoctorTool
    ExternalStarterKitDoctorToolIsReadOnly = $externalStarterKitDoctorToolIsReadOnly
    ExternalStarterKitDoctorVerifiesSubmissionZipContents = $externalStarterKitDoctorVerifiesSubmissionZipContents
    ExternalStarterKitDoctorRejectsUnsafeZipEntries = $externalStarterKitDoctorRejectsUnsafeZipEntries
    ExternalStarterKitDoctorUsesCaseExactSubmissionZipIntegrity = $externalStarterKitDoctorUsesCaseExactSubmissionZipIntegrity
    ExternalStarterKitDoctorRejectsReservedTopLevelCaseVariants = $externalStarterKitDoctorRejectsReservedTopLevelCaseVariants
    ExternalStarterKitDoctorFailsNonReadyExit = $externalStarterKitDoctorFailsNonReadyExit
    ExternalStarterKitRootLauncherPreservesToolExitCodes = $externalStarterKitRootLauncherPreservesToolExitCodes
    ExternalStarterKitNestedToolsPreserveChildExitCodes = $externalStarterKitNestedToolsPreserveChildExitCodes
    ExternalStarterKitReviewManifestRejectsCaseFoldSourceDuplicates = $externalStarterKitReviewManifestRejectsCaseFoldSourceDuplicates
    ExternalStarterKitValidatorRequiresExactPathCasing = $externalStarterKitValidatorRequiresExactPathCasing
    ExternalStarterKitRejectsReservedTopLevelCaseVariants = $externalStarterKitRejectsReservedTopLevelCaseVariants
    ExternalStarterKitReviewManifestUsesExactReservedOutputFolders = $externalStarterKitReviewManifestUsesExactReservedOutputFolders
    ExternalStarterKitPackageProofRequiresLowercaseSha256 = $externalStarterKitPackageProofRequiresLowercaseSha256
    ExternalStarterKitRootLauncherSupportsAuthoringSnippets = $externalStarterKitRootLauncherSupportsAuthoringSnippets
    ExternalStarterKitRootLauncherSupportsAuthoringSnippetApply = $externalStarterKitRootLauncherSupportsAuthoringSnippetApply
    ExternalStarterKitRootLauncherSupportsAssetEntrySnippet = $externalStarterKitRootLauncherSupportsAssetEntrySnippet
    ExternalStarterKitRootLauncherSupportsAssetEntryApply = $externalStarterKitRootLauncherSupportsAssetEntryApply
    ExternalStarterKitRootLauncherSupportsManifestContract = $externalStarterKitRootLauncherSupportsManifestContract
    ExternalStarterKitRootLauncherSupportsCapabilities = $externalStarterKitRootLauncherSupportsCapabilities
    ExternalStarterKitRootLauncherUsesStrictCapabilityGuideRead = $externalStarterKitRootLauncherUsesStrictCapabilityGuideRead
    ExternalStarterKitHubScriptsRequireCheckedInTemplates = $externalStarterKitHubScriptsRequireCheckedInTemplates
    ExternalStarterKitHubExecutableFallbackBodiesRemoved = $externalStarterKitHubExecutableFallbackBodiesRemoved
    ExternalStarterKitWritesSubmissionPackageTool = $externalStarterKitWritesSubmissionPackageTool
    ExternalStarterKitSubmissionPackagePreservesPreviousOutputUntilSuccess = $externalStarterKitSubmissionPackagePreservesPreviousOutputUntilSuccess
    ExternalStarterKitSubmissionPackageRefreshesTimestampAfterReplace = $externalStarterKitSubmissionPackageRefreshesTimestampAfterReplace
    ExternalStarterKitSubmissionPackageToolPasses = $externalStarterKitSubmissionPackageToolPasses
    ExternalStarterKitSubmissionPackageIncludesReviewManifest = $externalStarterKitSubmissionPackageIncludesReviewManifest
    ExternalStarterKitSubmissionPackageUsesCaseExactSourceEntries = $externalStarterKitSubmissionPackageUsesCaseExactSourceEntries
    ExternalStarterKitRootLauncherSupportsSubmissionPackage = $externalStarterKitRootLauncherSupportsSubmissionPackage
    ExternalStarterKitRootLauncherSupportsFirstMod = $externalStarterKitRootLauncherSupportsFirstMod
    ExternalStarterKitRootLauncherSupportsInstallLocal = $externalStarterKitRootLauncherSupportsInstallLocal
    ExternalStarterKitRootLauncherSupportsDiagnoseLocal = $externalStarterKitRootLauncherSupportsDiagnoseLocal
    ExternalStarterKitRootLauncherSupportsDoctor = $externalStarterKitRootLauncherSupportsDoctor
    ExternalStarterKitRootLauncherSupportsDependencies = $externalStarterKitRootLauncherSupportsDependencies
    ExternalStarterKitIdentityToolValidatesCanonicalId = $externalStarterKitIdentityToolValidatesCanonicalId
    ExternalStarterKitValidatorChecksSemver = $externalStarterKitValidatorChecksSemver
    ExternalStarterKitValidatorChecksManifestIdentityTextParity = $externalStarterKitValidatorChecksManifestIdentityTextParity
    ExternalStarterKitIdentityToolRejectsInvalidVersion = $externalStarterKitIdentityToolRejectsInvalidVersion
    ExternalStarterKitIdentityToolRollsBackBothManifestsOnValidationFailure = $externalStarterKitIdentityToolRollsBackBothManifestsOnValidationFailure
    ExternalStarterKitToolsAvoidNestedPowerShell = $externalStarterKitToolsAvoidNestedPowerShell
    ExternalStarterKitToolsUsePortableJoinPath = $externalStarterKitToolsUsePortableJoinPath
    ExternalStarterKitWritesJsonSchemas = $externalStarterKitWritesJsonSchemas
    ExternalStarterKitGeneratorUsesCheckedInTemplates = $externalStarterKitGeneratorUsesCheckedInTemplates
    ExternalStarterKitValidatorChecksJsonSchemas = $externalStarterKitValidatorChecksJsonSchemas
    ExternalStarterKitValidatorChecksEditorSchemaMappings = $externalStarterKitValidatorChecksEditorSchemaMappings
    ExternalStarterKitWritesVsCodeTasks = $externalStarterKitWritesVsCodeTasks
    ExternalStarterKitVsCodeTasksPresent = $externalStarterKitVsCodeTasksPresent
    ExternalStarterKitVsCodeTasksSupportDisabledAndReplace = $externalStarterKitVsCodeTasksSupportDisabledAndReplace
    ExternalStarterKitVsCodeTasksSupportLocalInstall = $externalStarterKitVsCodeTasksSupportLocalInstall
    ExternalStarterKitVsCodeTasksSupportLocalDiagnose = $externalStarterKitVsCodeTasksSupportLocalDiagnose
    ExternalStarterKitVsCodeTasksSupportDependencies = $externalStarterKitVsCodeTasksSupportDependencies
    ExternalStarterKitVsCodeTasksSupportDoctor = $externalStarterKitVsCodeTasksSupportDoctor
    ExternalStarterKitValidatorChecksVsCodeTasks = $externalStarterKitValidatorChecksVsCodeTasks
    ExternalStarterKitValidatorChecksSettingsAndLocaleContracts = $externalStarterKitValidatorChecksSettingsAndLocaleContracts
    ExternalStarterKitValidatorChecksAssetManifestContracts = $externalStarterKitValidatorChecksAssetManifestContracts
    ExternalStarterKitValidatorChecksManifestCapabilities = $externalStarterKitValidatorChecksManifestCapabilities
    ExternalStarterKitTemplateVersioned = $externalStarterKitTemplateVersioned
    ExternalStarterKitTemplatePassesLocalValidator = $externalStarterKitTemplatePassesLocalValidator
    ExternalStarterKitRootLauncherPasses = $externalStarterKitRootLauncherPasses
    ExternalStarterKitTemplateReferenceCsvsMatchSource = $externalStarterKitTemplateReferenceCsvsMatchSource
    ExternalStarterKitReviewManifestPasses = $externalStarterKitReviewManifestPasses
    ExternalStarterKitReviewManifestHashesFiles = $externalStarterKitReviewManifestHashesFiles
    ExternalStarterKitReviewManifestIncludesIdentity = $externalStarterKitReviewManifestIncludesIdentity
    ExternalStarterKitReviewManifestIdentityMatchesRuntimeManifest = $externalStarterKitReviewManifestIdentityMatchesRuntimeManifest
    ExternalStarterKitReviewManifestExcludesReports = $externalStarterKitReviewManifestExcludesReports
    ExternalStarterKitReviewManifestHasLimits = $externalStarterKitReviewManifestHasLimits
    ExternalStarterKitReviewManifestRejectsOversizedFile = $externalStarterKitReviewManifestRejectsOversizedFile
    ExternalStarterKitIdentityToolPasses = $externalStarterKitIdentityToolPasses
    ExternalStarterKitPrepareToolPasses = $externalStarterKitPrepareToolPasses
    ExternalStarterKitPrepareToolSupportsExistingManifest = $externalStarterKitPrepareToolSupportsExistingManifest
    ExternalStarterKitTemplateJsonSchemasVersioned = $externalStarterKitTemplateJsonSchemasVersioned
    ExternalStarterKitTemplateJsonSchemasParse = $externalStarterKitTemplateJsonSchemasParse
    ExternalStarterKitEditorSchemaMappingPresent = $externalStarterKitEditorSchemaMappingPresent
    MaxBundleBuildAssetCount = $maxBundleBuildAssetCount
    BundleBuildAssetDiscoveryUsesBoundedEnumeration = $bundleBuildAssetDiscoveryUsesBoundedEnumeration
    MaxManagedAssemblyInputCount = $maxManagedAssemblyInputCount
    MaxStaleAssemblyCleanupScanCount = $maxStaleAssemblyCleanupScanCount
    BuilderManagedAssemblyInputCapMatchesLoader = $builderManagedAssemblyInputCapMatchesLoader
    BuilderSkipsExpensiveValidationDuringOnGUI = $builderSkipsExpensiveValidationDuringOnGUI
    StaleDllCleanupUsesBoundedEnumeration = $builderStaleDllCleanupUsesBoundedEnumeration
    BuilderRejectsDuplicateManagedAssemblyFileNames = $builderRejectsDuplicateManagedAssemblyFileNames
    ResourceRegistryRejectsForgedOwner = $resourceRegistryRejectsForgedOwner
    PublicContentMethodCount = $publicContentMethodNames.Count
    RuntimePlaybook = $schema.staticValidation.runtimePlaybook
}

$result | Format-List
