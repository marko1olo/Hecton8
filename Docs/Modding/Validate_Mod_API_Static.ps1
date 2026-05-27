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
$changeControlChecklistPath = Join-Path $RepoRoot 'Docs\Modding\Change_Control_Checklist.md'
$runtimePlaybookPath = Join-Path $RepoRoot 'Docs\Modding\Runtime_Verification_Playbook.md'
$signalSourceDirectoryPath = Join-Path $RepoRoot 'Assets\_Project\Scripts\Core\Signals'
$signalSourceFilePattern = 'GlobalSignalPayloads*.cs'
$projectionPath = Join-Path $RepoRoot 'Assets\_Project\Scripts\ModdingAPI\ModEventProjectionBridge.cs'
$commandDispatcherPath = Join-Path $RepoRoot 'Assets\_Project\Scripts\ModdingAPI\ModCommandDispatcher.cs'
$futureCommandSandboxPath = Join-Path $RepoRoot 'Assets\_Project\Scripts\ModdingAPI\FutureCommandSandboxValidator.cs'
$modApiSandboxTunerWindowPath = Join-Path $RepoRoot 'Assets\_Project\Scripts\ModdingAPI\Editor\ModApiSandboxTunerWindow.cs'
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
Assert-True (Test-Path -LiteralPath $changeControlChecklistPath) "Missing change control checklist: $changeControlChecklistPath"
Assert-True (Test-Path -LiteralPath $runtimePlaybookPath) "Missing runtime verification playbook: $runtimePlaybookPath"
Assert-True (Test-Path -LiteralPath $signalSourceDirectoryPath) "Missing signal source directory: $signalSourceDirectoryPath"
Assert-True (Test-Path -LiteralPath $projectionPath) "Missing projection bridge: $projectionPath"
Assert-True (Test-Path -LiteralPath $commandDispatcherPath) "Missing command dispatcher: $commandDispatcherPath"
Assert-True (Test-Path -LiteralPath $futureCommandSandboxPath) "Missing future command sandbox validator: $futureCommandSandboxPath"
Assert-True (Test-Path -LiteralPath $modApiSandboxTunerWindowPath) "Missing mod API sandbox tuner window: $modApiSandboxTunerWindowPath"
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
$changeControlChecklistText = Get-Content -Raw -LiteralPath $changeControlChecklistPath
$runtimePlaybookText = Get-Content -Raw -LiteralPath $runtimePlaybookPath
$specText = Get-Content -Raw -LiteralPath $specPath

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
Assert-True (-not $modApiSandboxTunerWindowSource.Contains('FutureCommandOpcodes.TriggerSubtitleCue')) 'Mod API sandbox tuner still exposes TriggerSubtitleCue as a runtime-allowed opcode.'

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
Assert-True ($modBuilderWindowSource.Contains('RemoveStaleAssemblies(outputDirectory, copiedAssemblies)')) 'ModBuilderWindow does not clean stale managed DLLs from package output.'
Assert-True ($modBuilderWindowSource.Contains('Directory.GetFiles(outputDirectory, "*.dll", SearchOption.TopDirectoryOnly)')) 'ModBuilderWindow stale managed DLL cleanup does not scan output DLLs.'
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
Assert-True ([bool]$schema.resourceContentAudit.resourceRegistryRejectsForgedOwner) "Schema resourceContentAudit must record resource registry owner-id match enforcement."
Assert-True ($publicContentMethodNames.Count -eq [int]$schema.resourceContentAudit.publicContentMethodCount) "Public content method count drift. Source=$($publicContentMethodNames.Count) Schema=$($schema.resourceContentAudit.publicContentMethodCount)"

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
Assert-True (@($schema.commandApi.runtimeForbiddenFutureCommandOpcodes) -contains 'TriggerSubtitleCue') "Schema commandApi must include TriggerSubtitleCue in runtime forbidden future command opcodes."
Assert-True ($lastStaticValidation.resourceFacadeRequiresActiveScope -eq $true) "Schema lastStaticValidationSnapshot must record resource facade active-scope ownership."
Assert-True ($lastStaticValidation.resourceProxyRequiresActiveScope -eq $true) "Schema lastStaticValidationSnapshot must record resource proxy active-scope ownership."
Assert-True ($lastStaticValidation.resourceRegistryRejectsForgedOwner -eq $true) "Schema lastStaticValidationSnapshot must record resource registry owner-id match enforcement."
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
    ResourceRegistryRejectsForgedOwner = $resourceRegistryRejectsForgedOwner
    PublicContentMethodCount = $publicContentMethodNames.Count
    RuntimePlaybook = $schema.staticValidation.runtimePlaybook
}

$result | Format-List
