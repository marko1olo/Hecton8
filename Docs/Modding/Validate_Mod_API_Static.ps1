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
$signalsPath = Join-Path $RepoRoot 'Assets\_Project\Scripts\Core\GlobalSignals.cs'
$projectionPath = Join-Path $RepoRoot 'Assets\_Project\Scripts\ModdingAPI\ModEventProjectionBridge.cs'
$commandDispatcherPath = Join-Path $RepoRoot 'Assets\_Project\Scripts\ModdingAPI\ModCommandDispatcher.cs'
$futureCommandSandboxPath = Join-Path $RepoRoot 'Assets\_Project\Scripts\ModdingAPI\FutureCommandSandboxValidator.cs'
$allowedOpcodesCsvPath = Join-Path $RepoRoot 'Docs\Modding\allowed_opcodes.csv'
$kernelTuningProfilesCsvPath = Join-Path $RepoRoot 'Docs\Modding\kernel_tuning_profiles.csv'
$hectonApiPath = Join-Path $RepoRoot 'Assets\_Project\Scripts\ModdingAPI\HectonAPI.cs'
$hectonEventBusPath = Join-Path $RepoRoot 'Assets\_Project\Scripts\ModdingAPI\HectonEventBus.cs'
$eventContractsPath = Join-Path $RepoRoot 'Assets\_Project\Scripts\ModdingAPI\ModEventContracts.cs'
$resourceProxyPath = Join-Path $RepoRoot 'Assets\_Project\Scripts\ModdingAPI\IModResourceProxy.cs'
$modAssetManagerPath = Join-Path $RepoRoot 'Assets\_Project\Scripts\ModdingAPI\ModAssetManager.cs'
$spatialContractsPath = Join-Path $RepoRoot 'Assets\_Project\Scripts\ModdingAPI\ModSpatialContracts.cs'
$modLoaderPath = Join-Path $RepoRoot 'Assets\_Project\Scripts\ModdingAPI\ModLoader.cs'
$iHectonModPath = Join-Path $RepoRoot 'Assets\_Project\Scripts\ModdingAPI\IHectonMod.cs'
$modMetadataPath = Join-Path $RepoRoot 'Assets\_Project\Scripts\ModdingAPI\ModMetadata.cs'
$modRuntimeInfoPath = Join-Path $RepoRoot 'Assets\_Project\Scripts\ModdingAPI\ModRuntimeInfo.cs'
$modRuntimeStatePath = Join-Path $RepoRoot 'Assets\_Project\Scripts\ModdingAPI\ModRuntimeState.cs'
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
Assert-True (Test-Path -LiteralPath $signalsPath) "Missing signal source: $signalsPath"
Assert-True (Test-Path -LiteralPath $projectionPath) "Missing projection bridge: $projectionPath"
Assert-True (Test-Path -LiteralPath $commandDispatcherPath) "Missing command dispatcher: $commandDispatcherPath"
Assert-True (Test-Path -LiteralPath $futureCommandSandboxPath) "Missing future command sandbox validator: $futureCommandSandboxPath"
Assert-True (Test-Path -LiteralPath $allowedOpcodesCsvPath) "Missing allowed opcode CSV: $allowedOpcodesCsvPath"
Assert-True (Test-Path -LiteralPath $kernelTuningProfilesCsvPath) "Missing kernel tuning profiles CSV: $kernelTuningProfilesCsvPath"
Assert-True (Test-Path -LiteralPath $hectonApiPath) "Missing HectonAPI facade: $hectonApiPath"
Assert-True (Test-Path -LiteralPath $hectonEventBusPath) "Missing HectonEventBus source: $hectonEventBusPath"
Assert-True (Test-Path -LiteralPath $eventContractsPath) "Missing event contracts: $eventContractsPath"
Assert-True (Test-Path -LiteralPath $resourceProxyPath) "Missing resource proxy source: $resourceProxyPath"
Assert-True (Test-Path -LiteralPath $modAssetManagerPath) "Missing mod asset manager source: $modAssetManagerPath"
Assert-True (Test-Path -LiteralPath $spatialContractsPath) "Missing spatial contracts: $spatialContractsPath"
Assert-True (Test-Path -LiteralPath $modLoaderPath) "Missing mod loader: $modLoaderPath"
Assert-True (Test-Path -LiteralPath $iHectonModPath) "Missing IHectonMod contract: $iHectonModPath"
Assert-True (Test-Path -LiteralPath $modMetadataPath) "Missing mod metadata contract: $modMetadataPath"
Assert-True (Test-Path -LiteralPath $modRuntimeInfoPath) "Missing mod runtime info contract: $modRuntimeInfoPath"
Assert-True (Test-Path -LiteralPath $modRuntimeStatePath) "Missing mod runtime state source: $modRuntimeStatePath"
Assert-True (Test-Path -LiteralPath $saveBinaryStoragePath) "Missing save binary storage source: $saveBinaryStoragePath"
Assert-True (Test-Path -LiteralPath $saveBinaryPayloadCodecPath) "Missing save binary payload codec source: $saveBinaryPayloadCodecPath"

$schema = Get-Content -Raw -LiteralPath $schemaPath | ConvertFrom-Json
$contractIndexText = Get-Content -Raw -LiteralPath $contractIndexPath
$sampleModSpecText = Get-Content -Raw -LiteralPath $sampleModSpecPath
$signalSource = Get-Content -Raw -LiteralPath $signalsPath
$projectionSource = Get-Content -Raw -LiteralPath $projectionPath
$commandDispatcherSource = Get-Content -Raw -LiteralPath $commandDispatcherPath
$futureCommandSandboxSource = Get-Content -Raw -LiteralPath $futureCommandSandboxPath
$allowedOpcodesCsvText = Get-Content -Raw -LiteralPath $allowedOpcodesCsvPath
$kernelTuningProfilesCsvText = Get-Content -Raw -LiteralPath $kernelTuningProfilesCsvPath
$hectonApiSource = Get-Content -Raw -LiteralPath $hectonApiPath
$hectonEventBusSource = Get-Content -Raw -LiteralPath $hectonEventBusPath
$eventContractsSource = Get-Content -Raw -LiteralPath $eventContractsPath
$resourceProxySource = Get-Content -Raw -LiteralPath $resourceProxyPath
$modAssetManagerSource = Get-Content -Raw -LiteralPath $modAssetManagerPath
$spatialContractsSource = Get-Content -Raw -LiteralPath $spatialContractsPath
$modLoaderSource = Get-Content -Raw -LiteralPath $modLoaderPath
$iHectonModSource = Get-Content -Raw -LiteralPath $iHectonModPath
$modMetadataSource = Get-Content -Raw -LiteralPath $modMetadataPath
$modRuntimeInfoSource = Get-Content -Raw -LiteralPath $modRuntimeInfoPath
$modRuntimeStateSource = Get-Content -Raw -LiteralPath $modRuntimeStatePath
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

function Get-ClassPublicMethodNames([string]$Source, [string]$ClassName) {
    $match = [regex]::Match($Source, "public\s+static\s+class\s+$ClassName\s*\{(?<body>.*?)\n\s*\}", 'Singleline')
    if (-not $match.Success) {
        Fail "Missing public static class: $ClassName"
    }

    return @([regex]::Matches($match.Groups['body'].Value, '(?m)^\s*public\s+static\s+(?!class\b)(?:[A-Za-z0-9_<>,\[\]\.?]+\s+)+([A-Za-z0-9_]+)(?:<[^>]+>)?\s*\(') | ForEach-Object { $_.Groups[1].Value } | Sort-Object)
}

$signalMatches = [regex]::Matches($signalSource, 'public\s+struct\s+([A-Za-z0-9_]+)\s*:\s*ISignal')
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
$internalApiMethods = @([regex]::Matches($hectonApiSource, '(?m)^\s*internal\s+static\s+(?!class\b)(?:[A-Za-z0-9_<>,\[\]\.?]+\s+)+([A-Za-z0-9_]+)(?:<[^>]+>)?\s*\(') | ForEach-Object { $_.Groups[1].Value } | Sort-Object)
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
$modAupResponseSizeMatch = [regex]::Match($spatialContractsSource, 'StructLayout\(LayoutKind\.(?:Sequential|Explicit),\s*Size\s*=\s*(\d+)\)\]\s*public\s+struct\s+ModAupResponse', 'Singleline')
Assert-True $modAupResponseSizeMatch.Success 'Missing ModAupResponse size declaration.'
$modAupResponseSize = [int]$modAupResponseSizeMatch.Groups[1].Value
$currentApiVersionMatch = [regex]::Match($modLoaderSource, 'internal\s+const\s+int\s+CurrentAPIVersion\s*=\s*(\d+);')
Assert-True $currentApiVersionMatch.Success 'Missing ModLoader.CurrentAPIVersion.'
$currentApiVersion = [int]$currentApiVersionMatch.Groups[1].Value
$manifestFileNameMatch = [regex]::Match($modLoaderSource, 'private\s+const\s+string\s+ManifestFileName\s*=\s*"([^"]+)";')
Assert-True $manifestFileNameMatch.Success 'Missing ModLoader manifest file name constant.'
$manifestFileName = $manifestFileNameMatch.Groups[1].Value
$manifestStructMatch = [regex]::Match($modLoaderSource, 'private\s+struct\s+ModManifest\s*\{(?<body>.*?)public\s+ModManifest\s*\(', 'Singleline')
Assert-True $manifestStructMatch.Success 'Missing ModManifest field block.'
$manifestFields = @([regex]::Matches($manifestStructMatch.Groups['body'].Value, '(?m)^\s*public\s+[A-Za-z0-9_<>,\[\]\.?]+\s+([A-Za-z0-9_]+);') | ForEach-Object { $_.Groups[1].Value } | Sort-Object)
$modMetadataFields = Get-StructPublicFieldNames $modMetadataSource 'ModMetadata'
$modRuntimeInfoFields = Get-StructPublicFieldNames $modRuntimeInfoSource 'ModRuntimeInfo'
$lifecycleMethods = @([regex]::Matches($iHectonModSource, '(?m)^\s*void\s+(On[A-Za-z0-9_]+)\s*\(\s*\);') | ForEach-Object { $_.Groups[1].Value } | Sort-Object)
$versionedProperties = @([regex]::Matches($iHectonModSource, '(?m)^\s*int\s+RequiredAPIVersion\s*\{\s*get;\s*\}') | ForEach-Object { 'RequiredAPIVersion' })
$saveStatePublicMethods = @()
if ($hectonApiSource -match '(?m)^\s*public\s+static\s+void\s+SetModString\s*\(') { $saveStatePublicMethods += 'SetModString' }
if ($hectonApiSource -match '(?m)^\s*public\s+static\s+string\s+GetModString\s*\(') { $saveStatePublicMethods += 'GetModString' }
$saveDictionaryPrefixMatch = [regex]::Match($modRuntimeStateSource, 'private\s+const\s+string\s+SaveDictionaryPrefix\s*=\s*"([^"]+)";')
Assert-True $saveDictionaryPrefixMatch.Success 'Missing ModSaveStateStore.SaveDictionaryPrefix.'
$saveDictionaryPrefix = $saveDictionaryPrefixMatch.Groups[1].Value
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
$nativeBridgePublishLanes = @([regex]::Matches($hectonEventBusSource, 'PublishNativePayload\(HectonNativeEventKind\.([A-Za-z0-9_]+),') | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
$maxDispatchDepthMatch = [regex]::Match($hectonEventBusSource, 'private\s+const\s+int\s+MaxEventDispatchDepth\s*=\s*(\d+);')
Assert-True $maxDispatchDepthMatch.Success 'Missing HectonEventBus.MaxEventDispatchDepth.'
$maxDispatchDepth = [int]$maxDispatchDepthMatch.Groups[1].Value
$watchdogSecondsMatch = [regex]::Match($hectonEventBusSource, 'Stopwatch\.Frequency\s*\*\s*([0-9.]+)d')
Assert-True $watchdogSecondsMatch.Success 'Missing HectonEventBus callback watchdog seconds.'
$callbackWatchdogMilliseconds = [double]$watchdogSecondsMatch.Groups[1].Value * 1000.0
$subscriptionTokenHasIsActive = [regex]::IsMatch($hectonEventBusSource, 'public\s+bool\s+IsActive\s*=>')
$subscriptionTokenHasDispose = [regex]::IsMatch($hectonEventBusSource, 'public\s+void\s+Dispose\s*\(')
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
    'RegisterCustomItem' = 'public\s+static\s+bool\s+RegisterCustomItem\s*\('
    'TryFindItem' = 'public\s+static\s+bool\s+TryFindItem\s*\('
    'RegisterRecipe' = 'public\s+static\s+bool\s+RegisterRecipe\s*\('
    'RegisterRecycleYield' = 'public\s+static\s+bool\s+RegisterRecycleYield\s*\('
    'ProcessRecycle' = 'public\s+static\s+bool\s+ProcessRecycle\s*\('
    'RegisterBuildable' = 'public\s+static\s+bool\s+RegisterBuildable\s*\('
    'TryFindBuildable' = 'public\s+static\s+bool\s+TryFindBuildable\s*\('
    'RegisterBiomeMutation' = 'public\s+static\s+bool\s+RegisterBiomeMutation\s*\('
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
$missingFutureCommandOpcodesInCsv = @($futureCommandOpcodeHexes | Where-Object { $allowedOpcodeCsvHexes -notcontains $_ })
$extraAllowedOpcodeCsvHexes = @($allowedOpcodeCsvHexes | Where-Object { $futureCommandOpcodeHexes -notcontains $_ })
Assert-True ($missingFutureCommandOpcodesInCsv.Count -eq 0) "FutureCommandOpcodes missing from allowed_opcodes.csv: $($missingFutureCommandOpcodesInCsv -join ', ')"
Assert-True ($extraAllowedOpcodeCsvHexes.Count -eq 0) "allowed_opcodes.csv contains hashes missing from FutureCommandOpcodes: $($extraAllowedOpcodeCsvHexes -join ', ')"

$expectedKernelTuningProfileNames = @(
    'SurvivalOverride',
    'HapticPulse',
    'SubtitleCue'
)
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
Assert-True ($schema.globalRules.directUnityObjectReferencesForMods -eq $false) 'Schema allows direct Unity object references.'
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
Assert-True ($modAupResponseSize -eq [int]$schema.payloadLayoutAudit.modAupResponseSizeBytes) "ModAupResponse size drift. Source=$modAupResponseSize Schema=$($schema.payloadLayoutAudit.modAupResponseSizeBytes)"
Assert-True ($manifestFileName -eq $schema.loaderSaveAudit.manifestFileName) "Manifest file name drift. Source=$manifestFileName Schema=$($schema.loaderSaveAudit.manifestFileName)"
Assert-True ($currentApiVersion -eq [int]$schema.loaderSaveAudit.currentApiVersion) "Current API version drift. Source=$currentApiVersion Schema=$($schema.loaderSaveAudit.currentApiVersion)"
Assert-True ($manifestFields.Count -eq [int]$schema.loaderSaveAudit.manifestFieldCount) "Manifest field count drift. Source=$($manifestFields.Count) Schema=$($schema.loaderSaveAudit.manifestFieldCount)"
Assert-True ($modMetadataFields.Count -eq [int]$schema.loaderSaveAudit.modMetadataFieldCount) "ModMetadata field count drift. Source=$($modMetadataFields.Count) Schema=$($schema.loaderSaveAudit.modMetadataFieldCount)"
Assert-True ($modRuntimeInfoFields.Count -eq [int]$schema.loaderSaveAudit.modRuntimeInfoFieldCount) "ModRuntimeInfo field count drift. Source=$($modRuntimeInfoFields.Count) Schema=$($schema.loaderSaveAudit.modRuntimeInfoFieldCount)"
Assert-True ($lifecycleMethods.Count -eq [int]$schema.loaderSaveAudit.lifecycleMethodCount) "IHectonMod lifecycle count drift. Source=$($lifecycleMethods.Count) Schema=$($schema.loaderSaveAudit.lifecycleMethodCount)"
Assert-True ($versionedProperties.Count -eq [int]$schema.loaderSaveAudit.versionedInterfacePropertyCount) "IHectonVersionedMod property count drift. Source=$($versionedProperties.Count) Schema=$($schema.loaderSaveAudit.versionedInterfacePropertyCount)"
Assert-True ($saveStatePublicMethods.Count -eq [int]$schema.loaderSaveAudit.saveStatePublicMethodCount) "SaveState public method count drift. Source=$($saveStatePublicMethods.Count) Schema=$($schema.loaderSaveAudit.saveStatePublicMethodCount)"
Assert-True ($saveDictionaryPrefix -eq $schema.loaderSaveAudit.saveDictionaryPrefix) "Save dictionary prefix drift. Source=$saveDictionaryPrefix Schema=$($schema.loaderSaveAudit.saveDictionaryPrefix)"
Assert-True ($modPayloadBlockSize -eq [int]$schema.loaderSaveAudit.modPayloadBlockSizeBytes) "Mod payload block size drift. Source=$modPayloadBlockSize Schema=$($schema.loaderSaveAudit.modPayloadBlockSizeBytes)"
Assert-True ($modPayloadHeaderSize -eq [int]$schema.loaderSaveAudit.modPayloadHeaderSizeBytes) "Mod payload header size drift. Source=$modPayloadHeaderSize Schema=$($schema.loaderSaveAudit.modPayloadHeaderSizeBytes)"
Assert-True ($modPayloadMaxBytes -eq [int]$schema.loaderSaveAudit.modPayloadMaxBytes) "Mod payload max size drift. Source=$modPayloadMaxBytes Schema=$($schema.loaderSaveAudit.modPayloadMaxBytes)"
Assert-True ($publicEventMethodNames.Count -eq [int]$schema.eventSubscriptionAudit.publicEventMethodCount) "Public event method count drift. Source=$($publicEventMethodNames.Count) Schema=$($schema.eventSubscriptionAudit.publicEventMethodCount)"
Assert-True ($nativeEventKindNames.Count -eq [int]$schema.eventSubscriptionAudit.nativeEventKindCount) "Native event kind count drift. Source=$($nativeEventKindNames.Count) Schema=$($schema.eventSubscriptionAudit.nativeEventKindCount)"
Assert-True ($projectedEventKindNames.Count -eq [int]$schema.eventSubscriptionAudit.projectedEventKindCountIncludingNone) "Projected event kind count drift. Source=$($projectedEventKindNames.Count) Schema=$($schema.eventSubscriptionAudit.projectedEventKindCountIncludingNone)"
Assert-True ($nativeBridgePublishLanes.Count -eq [int]$schema.eventSubscriptionAudit.nativeQueueBridgePublishLaneCount) "Native queue bridge publish lane count drift. Source=$($nativeBridgePublishLanes.Count) Schema=$($schema.eventSubscriptionAudit.nativeQueueBridgePublishLaneCount)"
Assert-True ($maxDispatchDepth -eq [int]$schema.eventSubscriptionAudit.maxEventDispatchDepth) "Max event dispatch depth drift. Source=$maxDispatchDepth Schema=$($schema.eventSubscriptionAudit.maxEventDispatchDepth)"
Assert-True ([math]::Abs($callbackWatchdogMilliseconds - [double]$schema.eventSubscriptionAudit.callbackWatchdogMilliseconds) -lt 0.001) "Callback watchdog drift. Source=$callbackWatchdogMilliseconds Schema=$($schema.eventSubscriptionAudit.callbackWatchdogMilliseconds)"
Assert-True $subscriptionTokenHasIsActive 'HectonEventSubscription missing IsActive property.'
Assert-True $subscriptionTokenHasDispose 'HectonEventSubscription missing Dispose method.'
Assert-True ($publicResourceMethodNames.Count -eq [int]$schema.resourceContentAudit.publicResourceMethodCount) "Public resource method count drift. Source=$($publicResourceMethodNames.Count) Schema=$($schema.resourceContentAudit.publicResourceMethodCount)"
Assert-True ($resourceKindNames.Count -eq [int]$schema.resourceContentAudit.resourceKindCount) "Resource kind count drift. Source=$($resourceKindNames.Count) Schema=$($schema.resourceContentAudit.resourceKindCount)"
Assert-True ($resourceRegistryCapacity -eq [int]$schema.resourceContentAudit.resourceRegistryCapacity) "Resource registry capacity drift. Source=$resourceRegistryCapacity Schema=$($schema.resourceContentAudit.resourceRegistryCapacity)"
Assert-True ($internalAssetLoaderNames.Count -eq [int]$schema.resourceContentAudit.internalForbiddenAssetLoaderCount) "Internal asset loader count drift. Source=$($internalAssetLoaderNames.Count) Schema=$($schema.resourceContentAudit.internalForbiddenAssetLoaderCount)"
Assert-True ($rawTextureMaxBytes -eq [long]$schema.resourceContentAudit.rawTextureMaxBytes) "Raw texture byte cap drift. Source=$rawTextureMaxBytes Schema=$($schema.resourceContentAudit.rawTextureMaxBytes)"
Assert-True ($rawTextureMaxDimension -eq [int]$schema.resourceContentAudit.rawTextureMaxDimension) "Raw texture dimension cap drift. Source=$rawTextureMaxDimension Schema=$($schema.resourceContentAudit.rawTextureMaxDimension)"
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
Assert-True ($missingAllowedInSource.Count -eq 0) "Allowed schema lanes not found in GlobalSignals.cs: $($missingAllowedInSource -join ', ')"

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
Assert-True ($payloadLayoutAuditText.Contains('ModAupResponse')) 'Payload audit missing ModAupResponse.'

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

foreach ($method in $publicEventMethodNames) {
    Assert-True ($eventSubscriptionAuditText.Contains($method)) "Public event method missing from event subscription audit matrix: $method"
}

foreach ($kind in $nativeEventKindNames) {
    Assert-True ($eventSubscriptionAuditText.Contains($kind)) "Native event kind missing from event subscription audit matrix: $kind"
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
Assert-True ($runtimePlaybookText.Contains('Pass Criteria')) 'Runtime playbook missing pass criteria.'
Assert-True ($runtimePlaybookText.Contains('GC hot-path projection dispatch is 0 B/frame')) 'Runtime playbook missing GC pass criterion.'
Assert-True ($runtimePlaybookText.Contains('Only `CombatDamageSignal` and `WeatherChangedSignal` reach `SubscribeProjected`')) 'Runtime playbook missing projected-lane pass criterion.'
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
    ModAupResponseSizeBytes = $modAupResponseSize
    CurrentApiVersion = $currentApiVersion
    ManifestFieldCount = $manifestFields.Count
    ModMetadataFieldCount = $modMetadataFields.Count
    ModRuntimeInfoFieldCount = $modRuntimeInfoFields.Count
    LifecycleMethodCount = $lifecycleMethods.Count
    SaveStatePublicMethods = $saveStatePublicMethods.Count
    ModPayloadMaxBytes = $modPayloadMaxBytes
    PublicEventMethodCount = $publicEventMethodNames.Count
    NativeEventKindCount = $nativeEventKindNames.Count
    ProjectedEventKindCountIncludingNone = $projectedEventKindNames.Count
    NativeQueueBridgePublishLaneCount = $nativeBridgePublishLanes.Count
    MaxEventDispatchDepth = $maxDispatchDepth
    CallbackWatchdogMilliseconds = $callbackWatchdogMilliseconds
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
    PublicContentMethodCount = $publicContentMethodNames.Count
    RuntimePlaybook = $schema.staticValidation.runtimePlaybook
}

$result | Format-List
