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

$schemaPath = Join-Path $RepoRoot 'Docs\Modding\Signal_Schema.json'
$specPath = Join-Path $RepoRoot 'Docs\Modding\Mod_API_Specification.md'
$auditPath = Join-Path $RepoRoot 'Docs\Modding\Signal_Audit_Matrix.md'
$commandAuditPath = Join-Path $RepoRoot 'Docs\Modding\Command_Audit_Matrix.md'
$apiSurfaceAuditPath = Join-Path $RepoRoot 'Docs\Modding\API_Surface_Audit_Matrix.md'
$payloadLayoutAuditPath = Join-Path $RepoRoot 'Docs\Modding\Payload_Layout_Audit_Matrix.md'
$loaderSaveAuditPath = Join-Path $RepoRoot 'Docs\Modding\Loader_Save_Audit_Matrix.md'
$runtimePlaybookPath = Join-Path $RepoRoot 'Docs\Modding\Runtime_Verification_Playbook.md'
$signalsPath = Join-Path $RepoRoot 'Assets\_Project\Scripts\Core\GlobalSignals.cs'
$projectionPath = Join-Path $RepoRoot 'Assets\_Project\Scripts\ModdingAPI\ModEventProjectionBridge.cs'
$commandDispatcherPath = Join-Path $RepoRoot 'Assets\_Project\Scripts\ModdingAPI\ModCommandDispatcher.cs'
$hectonApiPath = Join-Path $RepoRoot 'Assets\_Project\Scripts\ModdingAPI\HectonAPI.cs'
$eventContractsPath = Join-Path $RepoRoot 'Assets\_Project\Scripts\ModdingAPI\ModEventContracts.cs'
$spatialContractsPath = Join-Path $RepoRoot 'Assets\_Project\Scripts\ModdingAPI\ModSpatialContracts.cs'
$modLoaderPath = Join-Path $RepoRoot 'Assets\_Project\Scripts\ModdingAPI\ModLoader.cs'
$iHectonModPath = Join-Path $RepoRoot 'Assets\_Project\Scripts\ModdingAPI\IHectonMod.cs'
$modMetadataPath = Join-Path $RepoRoot 'Assets\_Project\Scripts\ModdingAPI\ModMetadata.cs'
$modRuntimeInfoPath = Join-Path $RepoRoot 'Assets\_Project\Scripts\ModdingAPI\ModRuntimeInfo.cs'
$modRuntimeStatePath = Join-Path $RepoRoot 'Assets\_Project\Scripts\ModdingAPI\ModRuntimeState.cs'
$saveBinaryStoragePath = Join-Path $RepoRoot 'Assets\_Project\Scripts\SaveBinaryStorage.cs'
$saveBinaryPayloadCodecPath = Join-Path $RepoRoot 'Assets\_Project\Scripts\SaveBinaryPayloadCodec.cs'

Assert-True (Test-Path -LiteralPath $schemaPath) "Missing schema: $schemaPath"
Assert-True (Test-Path -LiteralPath $specPath) "Missing spec: $specPath"
Assert-True (Test-Path -LiteralPath $auditPath) "Missing audit matrix: $auditPath"
Assert-True (Test-Path -LiteralPath $commandAuditPath) "Missing command audit matrix: $commandAuditPath"
Assert-True (Test-Path -LiteralPath $apiSurfaceAuditPath) "Missing API surface audit matrix: $apiSurfaceAuditPath"
Assert-True (Test-Path -LiteralPath $payloadLayoutAuditPath) "Missing payload layout audit matrix: $payloadLayoutAuditPath"
Assert-True (Test-Path -LiteralPath $loaderSaveAuditPath) "Missing loader/save audit matrix: $loaderSaveAuditPath"
Assert-True (Test-Path -LiteralPath $runtimePlaybookPath) "Missing runtime verification playbook: $runtimePlaybookPath"
Assert-True (Test-Path -LiteralPath $signalsPath) "Missing signal source: $signalsPath"
Assert-True (Test-Path -LiteralPath $projectionPath) "Missing projection bridge: $projectionPath"
Assert-True (Test-Path -LiteralPath $commandDispatcherPath) "Missing command dispatcher: $commandDispatcherPath"
Assert-True (Test-Path -LiteralPath $hectonApiPath) "Missing HectonAPI facade: $hectonApiPath"
Assert-True (Test-Path -LiteralPath $eventContractsPath) "Missing event contracts: $eventContractsPath"
Assert-True (Test-Path -LiteralPath $spatialContractsPath) "Missing spatial contracts: $spatialContractsPath"
Assert-True (Test-Path -LiteralPath $modLoaderPath) "Missing mod loader: $modLoaderPath"
Assert-True (Test-Path -LiteralPath $iHectonModPath) "Missing IHectonMod contract: $iHectonModPath"
Assert-True (Test-Path -LiteralPath $modMetadataPath) "Missing mod metadata contract: $modMetadataPath"
Assert-True (Test-Path -LiteralPath $modRuntimeInfoPath) "Missing mod runtime info contract: $modRuntimeInfoPath"
Assert-True (Test-Path -LiteralPath $modRuntimeStatePath) "Missing mod runtime state source: $modRuntimeStatePath"
Assert-True (Test-Path -LiteralPath $saveBinaryStoragePath) "Missing save binary storage source: $saveBinaryStoragePath"
Assert-True (Test-Path -LiteralPath $saveBinaryPayloadCodecPath) "Missing save binary payload codec source: $saveBinaryPayloadCodecPath"

$schema = Get-Content -Raw -LiteralPath $schemaPath | ConvertFrom-Json
$signalSource = Get-Content -Raw -LiteralPath $signalsPath
$projectionSource = Get-Content -Raw -LiteralPath $projectionPath
$commandDispatcherSource = Get-Content -Raw -LiteralPath $commandDispatcherPath
$hectonApiSource = Get-Content -Raw -LiteralPath $hectonApiPath
$eventContractsSource = Get-Content -Raw -LiteralPath $eventContractsPath
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
$modCommandSizeMatch = [regex]::Match($commandDispatcherSource, 'StructLayout\(LayoutKind\.Sequential,\s*Size\s*=\s*(\d+)\)\]\s*public\s+struct\s+ModCommand', 'Singleline')
Assert-True $modCommandSizeMatch.Success 'Missing ModCommand sequential size declaration.'
$modCommandSize = [int]$modCommandSizeMatch.Groups[1].Value
$modAupResponseSizeMatch = [regex]::Match($spatialContractsSource, 'StructLayout\(LayoutKind\.Sequential,\s*Size\s*=\s*(\d+)\)\]\s*public\s+struct\s+ModAupResponse', 'Singleline')
Assert-True $modAupResponseSizeMatch.Success 'Missing ModAupResponse sequential size declaration.'
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

Assert-True ($schema.status -eq 'MOD_API_DEFINED_STATIC_SOURCE_RUNTIME_PENDING') "Unexpected schema status: $($schema.status)"
Assert-True ($schema.globalRules.directSignalBusAccessForMods -eq $false) 'Schema allows direct SignalBus access.'
Assert-True ($schema.globalRules.directNativeContainerAccessForMods -eq $false) 'Schema allows direct native container access.'
Assert-True ($schema.globalRules.directDataVaultAccessForMods -eq $false) 'Schema allows direct DataVault access.'
Assert-True ($schema.globalRules.directUnityObjectReferencesForMods -eq $false) 'Schema allows direct Unity object references.'
Assert-True ($schema.globalRules.stringEventNames -eq $false) 'Schema allows string event names.'
Assert-True ($schema.globalRules.jsonHotPathEvents -eq $false) 'Schema allows JSON hot-path events.'

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

Assert-True ($specText.Contains('Signal_Audit_Matrix.md')) 'Spec does not link Signal_Audit_Matrix.md.'
Assert-True ($specText.Contains('Command_Audit_Matrix.md')) 'Spec does not link Command_Audit_Matrix.md.'
Assert-True ($specText.Contains('API_Surface_Audit_Matrix.md')) 'Spec does not link API_Surface_Audit_Matrix.md.'
Assert-True ($specText.Contains('Payload_Layout_Audit_Matrix.md')) 'Spec does not link Payload_Layout_Audit_Matrix.md.'
Assert-True ($specText.Contains('Loader_Save_Audit_Matrix.md')) 'Spec does not link Loader_Save_Audit_Matrix.md.'
Assert-True ($specText.Contains('Runtime_Verification_Playbook.md')) 'Spec does not link Runtime_Verification_Playbook.md.'
Assert-True ($runtimePlaybookText.Contains('Pass Criteria')) 'Runtime playbook missing pass criteria.'
Assert-True ($runtimePlaybookText.Contains('GC hot-path projection dispatch is 0 B/frame')) 'Runtime playbook missing GC pass criterion.'
Assert-True ($runtimePlaybookText.Contains('Only `CombatDamageSignal` and `WeatherChangedSignal` reach `SubscribeProjected`')) 'Runtime playbook missing projected-lane pass criterion.'
Assert-True ($runtimePlaybookText.Contains('Command_Audit_Matrix.md')) 'Runtime playbook does not link Command_Audit_Matrix.md.'
Assert-True ($runtimePlaybookText.Contains('API_Surface_Audit_Matrix.md')) 'Runtime playbook does not link API_Surface_Audit_Matrix.md.'
Assert-True ($runtimePlaybookText.Contains('Payload_Layout_Audit_Matrix.md')) 'Runtime playbook does not link Payload_Layout_Audit_Matrix.md.'
Assert-True ($runtimePlaybookText.Contains('Loader_Save_Audit_Matrix.md')) 'Runtime playbook does not link Loader_Save_Audit_Matrix.md.'

$result = [pscustomobject]@{
    Status = 'PASS'
    SchemaRevision = $schema.schemaRevision
    SourceSignals = $uniqueSignals.Count
    AllowedProjectedSignals = $allowedSignals.Count
    DeniedByDefaultSignals = $uniqueSignals.Count - $allowedSignals.Count
    AcceptedCommandOpcodes = $acceptedOpcodes.Count
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
    ProjectionBridgeSignals = ($uniqueBridgeSignals -join ',')
    AuditPath = $schema.sourceSignalInventory.auditPath
    CommandAuditPath = $schema.commandApi.auditPath
    ApiSurfaceAuditPath = $schema.apiSurfaceAudit.auditPath
    PayloadLayoutAuditPath = $schema.payloadLayoutAudit.auditPath
    LoaderSaveAuditPath = $schema.loaderSaveAudit.auditPath
    RuntimePlaybook = $schema.staticValidation.runtimePlaybook
}

$result | Format-List
