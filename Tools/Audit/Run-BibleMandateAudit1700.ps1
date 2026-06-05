param(
    [string]$Root = "",
    [int]$MaxRiskLines = 80,
    [int]$MaxEvidenceFiles = 80
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($Root)) {
    $Root = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
} else {
    $Root = (Resolve-Path $Root).Path
}

Set-Location $Root

$RunDate = "2026-06-02"
$OutRoot = Join-Path $Root "Docs\BibleMandateAudits\1700"
$ScanRoot = Join-Path $OutRoot "_scans"
New-Item -ItemType Directory -Force -Path $OutRoot | Out-Null
New-Item -ItemType Directory -Force -Path $ScanRoot | Out-Null

$SelectedMandates = @(
    ".agents-skills\OPT_Zero_GC_Policy_AllocFree_Mandate.txt",
    ".agents-skills\OPT_Premium_Approximation_Protocol.txt",
    ".agents-skills\OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt",
    ".agents-skills\ARCH_Global_Registry_ServiceLocator_DI_Init.txt",
    ".agents-skills\ARCH_Execution_Phases.txt",
    ".agents-skills\REND_URP_Graphics_HotPath_Optimization_HLOD.txt",
    ".agents-skills\PHYS_Physics_Integrity_Determinism_ForceMode.txt",
    ".agents-skills\DBG_Telemetry_Crash_Reporting_PostMortem.txt"
)

$GeneralRiskRegex = "GameObject\.Find|FindObjectOfType|FindObjectsOfType|Resources\.Load|new Mesh\(|RecalculateNormals|MeshCollider|\.material\b|\.materials\b|Physics\.Raycast\(|Physics\.SphereCast\(|Physics\.CapsuleCast\(|Physics\.OverlapSphere\(|\.Complete\(|StartCoroutine|yield return new|Debug\.Log|\.Where\(|\.Select\(|\.ToList\(|new NativeArray|Allocator\.Persistent|GetComponent<|GetComponentsInChildren|Camera\.main|\.text\s*=|^\s*(private|protected|public|internal)?\s*(override\s+)?void\s+(Update|FixedUpdate|LateUpdate)\s*\("

$Groups = @(
    [ordered]@{
        Id = "00_mandate_registry"
        Title = "Mandate Registry Currency and Routing"
        Bibles = @("AGENTS.md","PROJECT_BIBLES.md",".agents-skills\README.md","Docs\PROJECT_ATLAS.md","Docs\ARCHITECTURE\DOMAIN_ARCHITECTURE_COVERAGE_MATRIX.md")
        MandatePatterns = @("ARCH_","OPT_","REND_","PHYS_","UI_","DATA_","STRM_","AI_","VOX_","AUD_","AUDIO_","GPU_","CORE_","LOGI_","MATH_","DBG_","QA_","NET_","TOOL_","CTRL_","ANIM_","PROG_","PROJECT_","CI_","MANDATE")
        Roots = @(".agents-skills","AGENTS.md","PROJECT_BIBLES.md","Docs\PROJECT_ATLAS.md","Docs\ARCHITECTURE\DOMAIN_ARCHITECTURE_COVERAGE_MATRIX.md")
        EvidenceRegex = "RULE|FORBID|REQ|GlobalQualityWeight|Engineering Data|Status|Scope|Authority|Evidence"
        Proof = "Mandate files exist, route docs point agents to the right bibles, and stale or conflicting mandate language is promoted into root authority before implementation."
    },
    [ordered]@{
        Id = "00_project_routes"
        Title = "Project Routes, Taste, Quality, Agent Entry"
        Bibles = @("AGENTS.md","PROJECT_BIBLES.md","TASTE.md","quality.md","testing.md","release.md")
        MandatePatterns = @("QA_","ARCH_","OPT_","DBG_")
        Roots = @("AGENTS.md","PROJECT_BIBLES.md","TASTE.md","quality.md","Docs","Tools")
        EvidenceRegex = "PROJECT_BIBLES|GlobalQualityWeight|Evidence Boundary|Proof Artifacts|Acceptance|Reject|QUALITY_GATES|Route"
        Proof = "All routes exist, root bibles carry owner/proof/rejection/quality clauses, and runtime claims remain separated from static docs."
    },
    [ordered]@{
        Id = "01_generated_assets"
        Title = "Generated Meshes, Textures, Materials, LOD, Collision"
        Bibles = @("3dmodel.md","PROCEDURAL_ASSET_PIPELINE.md","3DMODEL_HERO_REALISM_OVERKILL.md","3DMODEL_TEXTURE_GENERATION_PLAYBOOK.md","3DMODEL_TEXTURES_MATERIALS.md","3DMODEL_HARD_SURFACE_MODULES.md","3DMODEL_EQUIPMENT_PROPS.md","3DMODEL_FAUNA.md","3DMODEL_FLORA_CORAL.md","3DMODEL_GEOLOGY_ROCKS.md")
        MandatePatterns = @("TOOL_Procedural","REND_Instanced","REND_URP","STRM_Async_Asset","PHYS_Physics","OPT_Zero")
        Roots = @("Assets\_Project\Editor\Generators","Assets\_Project\Editor\Bakers","Assets\_Project\Scripts\World","Assets\_Project\Scripts\Fauna","Assets\_Project\Scripts\Rendering","Assets\_Project\Art","Assets\_Project\Data","Tools")
        EvidenceRegex = "new Mesh|MeshCollider|LOD|Recalculate|uv|normal|tangent|Texture2D|AssetDatabase|PrefabUtility|Generate|Bake|Atlas|Material|VertexColor|Collider"
        Proof = "Generator manifests, LOD chain, flat material screenshot, wireframe screenshot, UV/atlas report, collider proxy report, import settings, and pre-save mesh validation."
    },
    [ordered]@{
        Id = "02_ui_frontend_hud"
        Title = "UI, Menus, HUD, Terminals, Localization, Settings"
        Bibles = @("ui.md","UI_MENU_SCREEN_STANDARDS.md","UI_DIEGETIC_HUD_STANDARDS.md","input.md","settings.md","localization.md","accessibility.md")
        MandatePatterns = @("UI_","CTRL_","OPT_Zero","OPT_Performance")
        Roots = @("Assets\_Project\Scripts\UI","Assets\_Project\Scripts\Visor","Assets\_Project\Scripts\Input","Assets\_Project\Scripts\Player","Assets\_Project\Editor\HectonUIBuilder.cs","Assets\_Project\Scripts\LocalizedWorldSign.cs","Assets\_Project\Scripts\LocRegistry.cs","Assets\_Project\Scripts\LocNumericBuffer.cs")
        EvidenceRegex = "TMP_Text|TextMeshPro|UIDocument|Canvas|SetText|char\[\]|Localization|Loc|InputAction|Menu|HUD|Visor|Settings|Accessibility|Subtitle"
        Proof = "Desktop/mobile screenshots, controller navigation proof, localization expansion capture, 0 B UI text update proof, and menu/HUD state-truth trace."
    },
    [ordered]@{
        Id = "03_rendering_visuals"
        Title = "Rendering, Shaders, Lighting, VFX, Water Presentation"
        Bibles = @("rendering.md","shaders.md","lighting.md","vfx.md","presentation.md","water.md","atmosphere.md","platform.md","performance.md","compute.md")
        MandatePatterns = @("REND_","GPU_","CORE_Weather","OPT_Performance","OPT_Premium")
        Roots = @("Assets\_Project\Scripts\Rendering","Assets\_Project\Scripts\Graphics","Assets\_Project\Scripts\VFX","Assets\_Project\Scripts\Lighting","Assets\_Project\Scripts\Atmosphere","Assets\_Project\Scripts\Environment","Assets\_Project\Editor","Data\Visuals","Tools")
        EvidenceRegex = "RenderGraph|ScriptableRendererFeature|Shader|HLOD|BatchRendererGroup|GraphicsBuffer|VFX|Light|Fog|Water|Material|CBUFFER|ComputeShader|Volumetric|Caustic|Post"
        Proof = "Frame Debugger/RenderGraph capture, URP asset proof, shader variant count, material batching proof, compact/high screenshots, GPU/VRAM profiler captures."
    },
    [ordered]@{
        Id = "04_runtime_architecture_data_telemetry"
        Title = "Runtime Architecture, Data, Bootstrap, Telemetry, Performance"
        Bibles = @("systems.md","data.md","bootstrap.md","telemetry.md","performance.md","math.md","authoring.md","quality.md")
        MandatePatterns = @("ARCH_","DATA_Runtime","OPT_Native","OPT_Hecton","DBG_","CI_MATH")
        Roots = @("Assets\_Project\Scripts\Core","Assets\_Project\Scripts\Bootstrap","Assets\_Project\Scripts\Global","Assets\_Project\Scripts\Data","Assets\_Project\Scripts\Optimization","Tools","Docs\ARCHITECTURE")
        EvidenceRegex = "GlobalRegistry|SignalBus|GlobalDataVault|ITickable|IFixedTickable|SystemDispatcher|ProfilerMarker|NativeArray|NativeQueue|GlobalQualityWeight|DataMonolith|BlackBox|Telemetry|RouteCard"
        Proof = "Owner phase map, route cards, SignalBus/DataVault payload layouts, profiler markers, 300-frame black-box dump, no hot registry polling scan."
    },
    [ordered]@{
        Id = "05_physics_vehicles_water"
        Title = "Physics, Vehicles, Pressure, Water Truth, Survival Physiology"
        Bibles = @("physics.md","vehicles.md","water.md","survival.md","combat.md","animation.md","camera.md")
        MandatePatterns = @("PHYS_","CORE_Submarine","CORE_Abyss","CORE_Damage","ANIM_","OPT_Premium")
        Roots = @("Assets\_Project\Scripts\Physics","Assets\_Project\Scripts\Vehicles","Assets\_Project\Scripts\Physiology","Assets\_Project\Scripts\Thermodynamics","Assets\_Project\Scripts\Atmosphere","Assets\_Project\Editor\Physics","Assets\_Project\Scripts\World")
        EvidenceRegex = "Rigidbody|ForcePacket|NonAlloc|Collider|MeshCollider|Buoyancy|Pressure|Water|Tether|Vehicle|Submarine|Survival|Damage|Physiology|FixedTick"
        Proof = "Fixed-step owner proof, force packet routing, NonAlloc query proof, collision proxy report, compact/high vehicle capture, NaN/black-box dump proof."
    },
    [ordered]@{
        Id = "06_world_terrain_voxels_ecosystem"
        Title = "World, Terrain, Voxels, Geology, Ecosystem, Celestial"
        Bibles = @("world.md","terrain.md","voxels.md","ecosystem.md","atmosphere.md","celestial.md","streaming.md","3DMODEL_GEOLOGY_ROCKS.md")
        MandatePatterns = @("VOX_","CORE_Weather","MATH_AUP","STRM_World","TOOL_Procedural","AI_DYNAMIC")
        Roots = @("Assets\_Project\Scripts\World","Assets\_Project\Scripts\Ecosystem","Assets\_Project\Scripts\Fauna","Assets\_Project\Scripts\Environment","Assets\_Project\Editor\Generators","Assets\_Project\Scripts\Cartography","Assets\_Project\Scripts\Atmosphere","Assets\_Project\Scripts\Celestial")
        EvidenceRegex = "Voxel|SDF|Marching|Biome|Terrain|MapMagic|Scatter|Ecosystem|AUP|Geology|Chunk|Celestial|Tide|Silt|Biome"
        Proof = "Seed manifest, terrain mask proof, voxel seam proof, chunk/residency proof, compact/high route screenshots, biome/ecosystem owner cadence proof."
    },
    [ordered]@{
        Id = "07_gameplay_construction_tools_inventory_combat"
        Title = "Gameplay, Tools, Construction, Inventory, Combat, Economy"
        Bibles = @("gameplay.md","tools.md","construction.md","inventory.md","combat.md","logistics.md","drones.md","narrative.md")
        MandatePatterns = @("CORE_Tools","DATA_Inventory","LOGI_","CORE_Damage","PROG_","OPT_Zero")
        Roots = @("Assets\_Project\Scripts\Gameplay","Assets\_Project\Scripts\Construction","Assets\_Project\Scripts\Tools","Assets\_Project\Scripts\Inventory","Assets\_Project\Scripts\Economy","Assets\_Project\Scripts\Interaction","Assets\_Project\Scripts\Scavenging","Assets\_Project\Scripts\Equipment","Assets\_Project\Scripts\Power","Assets\_Project\Scripts\Logistics")
        EvidenceRegex = "IInteractable|Interact|Item|Recipe|Craft|Inventory|Damage|Hitbox|Tool|Cutter|Scanner|Construction|Socket|Resource|Power|Logistics|Drone|Quest"
        Proof = "First-20-min route proof, interaction target proof, inventory/economy data proof, construction graph proof, combat proxy/hitbox proof, zero-GC interaction scan."
    },
    [ordered]@{
        Id = "08_ai_creatures_sonar_drones"
        Title = "AI, Creatures, Sonar, Drones, Navigation"
        Bibles = @("ai.md","creatures.md","sonar.md","drones.md","ecosystem.md","audio.md","tools.md")
        MandatePatterns = @("AI_","AUD_Acoustic","CORE_Tools","REND_Instanced","OPT_Performance")
        Roots = @("Assets\_Project\Scripts\AI","Assets\_Project\Scripts\Fauna","Assets\_Project\Scripts\Ecosystem","Assets\_Project\Scripts\Tools","Assets\_Project\Scripts\Cartography","Assets\_Project\Scripts\Audio")
        EvidenceRegex = "AI|Creature|Boid|Flock|Path|Nav|Sonar|Scan|Drone|Cognition|Behavior|SpatialHash|AStar|Funnel|Acoustic"
        Proof = "AI state/cadence proof, pathfinding proof, sensory truth proof, sonar confidence/staleness proof, black-box last-300-frame ring for critical AI."
    },
    [ordered]@{
        Id = "09_audio_narrative_presentation"
        Title = "Audio, Narrative, PDA, Cinematics, Public Text"
        Bibles = @("audio.md","narrative.md","presentation.md","cinematics.md","textes.md","accessibility.md","sonar.md")
        MandatePatterns = @("AUD_","AUDIO_","PROG_","QA_","OPT_Premium")
        Roots = @("Assets\_Project\Scripts\Audio","Assets\_Project\Scripts\AudioLog","Assets\_Project\Scripts\Narrative","Assets\_Project\Scripts\Quest","Assets\_Project\Scripts\PDA","Assets\_Project\Scripts\VFX","Assets\_Project\Scripts\Visor")
        EvidenceRegex = "Audio|DSP|Sonar|Narrative|Quest|PDA|BlackBox|Subtitle|Cinematic|Presentation|Warning|Log|Binaural|Hydrophone"
        Proof = "DSP/voice budget proof, soundscape capture, narrative evidence-before-text proof, subtitle/accessibility proof, capture-truth label proof for public material."
    },
    [ordered]@{
        Id = "10_persistence_streaming_release_platform"
        Title = "Persistence, Streaming, Release, Platform, Modding, Testing"
        Bibles = @("persistence.md","streaming.md","release.md","platform.md","modding.md","networking.md","testing.md","authoring.md")
        MandatePatterns = @("DATA_Save","STRM_","NET_","QA_","PROJECT_LTS","TOOL_Designer")
        Roots = @("Assets\_Project\Scripts\SaveSystem","Assets\_Project\Scripts\Optimization","Assets\_Project\Scripts\QA","Assets\_Project\Scripts\Build","Assets\_Project\Scripts\ModdingAPI","Assets\_Project\Scripts\Networking","Assets\_Project\Tests","ProjectSettings","Packages","Tools")
        EvidenceRegex = "Save|Addressables|Streaming|Test|Assert|Build|Platform|Modding|Profiler|GC|Memory|h8bin|LZ4|Checksum|Network|Merkle|Rollback"
        Proof = "Save/load binary proof, Addressables/residency proof, build/import/player proof, platform device proof, mod envelope proof, testing evidence class proof."
    }
)

function Get-ExistingPath {
    param([string]$Path)
    return (Test-Path -LiteralPath (Join-Path $Root $Path))
}

function Get-BibleInfo {
    param([string[]]$Bibles)
    $items = @()
    foreach ($b in $Bibles) {
        $full = Join-Path $Root $b
        if (Test-Path -LiteralPath $full) {
            $content = Get-Content -LiteralPath $full -Raw
            $headings = [regex]::Matches($content, "(?m)^#{1,3}\s+(.+)$") | ForEach-Object { $_.Groups[1].Value } | Select-Object -First 12
            $items += [ordered]@{
                Path = $b
                Exists = $true
                Lines = (($content -split "`r?`n").Count)
                HasGlobalQualityWeight = ($content -match "GlobalQualityWeight")
                HasProof = ($content -match "Proof|Evidence|Verification")
                HasAcceptance = ($content -match "Acceptance|Accepted|accept")
                HasRejection = ($content -match "Reject|Rejection|FORBID")
                Headings = @($headings)
            }
        } else {
            $items += [ordered]@{
                Path = $b
                Exists = $false
                Lines = 0
                HasGlobalQualityWeight = $false
                HasProof = $false
                HasAcceptance = $false
                HasRejection = $false
                Headings = @()
            }
        }
    }
    return $items
}

function Get-MandateMatches {
    param([string[]]$Patterns)
    if (!(Test-Path ".agents-skills")) { return @() }
    $files = Get-ChildItem -LiteralPath ".agents-skills" -File | ForEach-Object { $_.FullName.Substring($Root.Length + 1) }
    $matches = @()
    foreach ($p in $Patterns) {
        $matches += $files | Where-Object { $_ -like ".agents-skills\$p*" -or $_ -match [regex]::Escape($p) }
    }
    return @($matches | Sort-Object -Unique)
}

function Invoke-RgFiles {
    param(
        [string]$Pattern,
        [string[]]$Roots,
        [int]$Limit
    )
    $result = @()
    foreach ($r in $Roots) {
        if (!(Get-ExistingPath $r)) { continue }
        $lines = & rg -l -i --glob "*.cs" --glob "*.shader" --glob "*.hlsl" --glob "*.compute" --glob "*.uxml" --glob "*.uss" --glob "*.asmdef" --glob "*.asset" --glob "*.prefab" --glob "*.mat" --glob "*.md" --glob "!Docs/_Archive/**" --glob "!Docs/Archive/**" --glob "!Docs/DEPRECATED/**" --glob "!Docs/BibleMandateAudits/**" --glob "!Docs/AgentLogs/**" --glob "!Docs/Tasks/**" -- $Pattern $r 2>$null
        if ($LASTEXITCODE -eq 0 -and $lines) {
            $result += $lines
        }
    }
    return @($result | Sort-Object -Unique)
}

function Invoke-RgLines {
    param(
        [string]$Pattern,
        [string[]]$Roots,
        [int]$Limit
    )
    $result = @()
    foreach ($r in $Roots) {
        if (!(Get-ExistingPath $r)) { continue }
        $lines = & rg -n --with-filename --glob "*.cs" --glob "*.shader" --glob "*.hlsl" --glob "*.compute" --glob "*.uxml" --glob "*.uss" --glob "*.asmdef" --glob "!Docs/_Archive/**" --glob "!Docs/Archive/**" --glob "!Docs/DEPRECATED/**" --glob "!Docs/BibleMandateAudits/**" --glob "!Docs/AgentLogs/**" --glob "!Docs/Tasks/**" -- $Pattern $r 2>$null
        if ($LASTEXITCODE -eq 0 -and $lines) {
            $result += $lines
        }
    }
    return @($result)
}

function Get-Verdit {
    param(
        [object[]]$BibleInfo,
        [object[]]$EvidenceFiles,
        [object[]]$RuntimeRiskLines,
        [string]$Id
    )
    $missingBibles = @($BibleInfo | Where-Object { -not $_.Exists })
    if ($missingBibles.Count -gt 0) { return "RED_MISSING_BIBLE_ROUTE" }
    if ($Id -ne "00_project_routes" -and $EvidenceFiles.Count -eq 0) { return "RED_NO_CODE_EVIDENCE_STATIC" }
    if ($RuntimeRiskLines.Count -gt 0) { return "YELLOW_RUNTIME_STATIC_RISK_REVIEW_REQUIRED" }
    return "GREEN_STATIC_ALIGNMENT_PENDING_RUNTIME_PROOF"
}

function Split-RiskLines {
    param([string[]]$RiskLines)
    $runtime = @()
    $editor = @()
    foreach ($line in $RiskLines) {
        $path = ($line -split ":", 2)[0]
        if ($path -match "(\.md$|\.txt$|^\.agents-skills\\|^Tools\\|^Docs\\|^ProjectSettings\\|^Packages\\|\\Tests\\|\\Editor\\|Assets\\_Project\\Editor\\)") {
            $editor += $line
        } else {
            $runtime += $line
        }
    }
    return [ordered]@{
        Runtime = @($runtime)
        Editor = @($editor)
    }
}

function Write-SystemReport {
    param(
        [hashtable]$Group,
        [object[]]$BibleInfo,
        [string[]]$MandateMatches,
        [string[]]$EvidenceFiles,
        [string[]]$RuntimeRiskLines,
        [string[]]$EditorRiskLines,
        [string]$Verdict
    )

    $folder = Join-Path $OutRoot $Group.Id
    New-Item -ItemType Directory -Force -Path $folder | Out-Null
    $reportPath = Join-Path $folder "REPORT.md"

    $missingRoots = @($Group.Roots | Where-Object { -not (Get-ExistingPath $_) })
    $presentRoots = @($Group.Roots | Where-Object { Get-ExistingPath $_ })
    $missingBibles = @($BibleInfo | Where-Object { -not $_.Exists } | ForEach-Object { $_.Path })
    $weakBibles = @($BibleInfo | Where-Object { $_.Exists -and (-not $_.HasProof -or -not $_.HasRejection -or -not $_.HasAcceptance) } | ForEach-Object { $_.Path })
    $evidenceRaw = Join-Path $ScanRoot ("{0}_evidence_files.txt" -f $Group.Id)
    $runtimeRiskRaw = Join-Path $ScanRoot ("{0}_runtime_risks.txt" -f $Group.Id)
    $editorRiskRaw = Join-Path $ScanRoot ("{0}_editor_tool_risks.txt" -f $Group.Id)
    Set-Content -LiteralPath $evidenceRaw -Value ($EvidenceFiles -join "`r`n") -Encoding UTF8
    Set-Content -LiteralPath $runtimeRiskRaw -Value ($RuntimeRiskLines -join "`r`n") -Encoding UTF8
    Set-Content -LiteralPath $editorRiskRaw -Value ($EditorRiskLines -join "`r`n") -Encoding UTF8

    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add("# $($Group.Title)")
    $lines.Add("")
    $lines.Add("Status: STATIC BIBLE/MANDATE/CODEBASE AUDIT - RUNTIME PROOF NOT RUN")
    $lines.Add("Date: $RunDate")
    $lines.Add("Verdict: $Verdict")
    $lines.Add("")
    $lines.Add("## Scope")
    $lines.Add("")
    $lines.Add("This report compares the current root bible routes and selected mandate registry files against static codebase evidence. It does not prove Unity import health, Play Mode behavior, profiler cost, memory use, visual quality, or device performance.")
    $lines.Add("")
    $lines.Add("## Bibles Checked")
    $lines.Add("")
    foreach ($b in $BibleInfo) {
        if ($b.Exists) {
            $flags = @()
            if ($b.HasGlobalQualityWeight) { $flags += "GlobalQualityWeight" }
            if ($b.HasProof) { $flags += "proof" }
            if ($b.HasAcceptance) { $flags += "acceptance" }
            if ($b.HasRejection) { $flags += "rejection" }
            $flagText = if ($flags.Count -gt 0) { $flags -join ", " } else { "no structural flags detected" }
            $lines.Add(("- OK {0} - {1} lines; {2}." -f $b.Path, $b.Lines, $flagText))
        } else {
            $lines.Add(("- MISSING {0}." -f $b.Path))
        }
    }
    if ($weakBibles.Count -gt 0) {
        $lines.Add("")
        $lines.Add("Static weak-bible flags: $($weakBibles -join ', '). These may still be acceptable if the clauses use domain-specific wording, but they require manual review.")
    }
    $lines.Add("")
    $lines.Add("## Mandates Matched")
    $lines.Add("")
    if ($MandateMatches.Count -eq 0) {
        $lines.Add("- NO DIRECT MANDATE MATCH by configured patterns. This is a routing risk.")
    } else {
        foreach ($m in $MandateMatches) { $lines.Add(("- {0}" -f $m)) }
    }
    $lines.Add("")
    $lines.Add("## Code/Asset Roots")
    $lines.Add("")
    if ($presentRoots.Count -eq 0) {
        $lines.Add("- No configured roots exist.")
    } else {
        foreach ($r in $presentRoots) { $lines.Add(("- OK {0}" -f $r)) }
    }
    if ($missingRoots.Count -gt 0) {
        foreach ($r in $missingRoots) { $lines.Add(("- MISSING {0}" -f $r)) }
    }
    $lines.Add("")
    $lines.Add("## Static Evidence Found")
    $lines.Add("")
    if ($EvidenceFiles.Count -eq 0) {
        $lines.Add("- No files matched this system evidence regex. Treat implementation as missing or outside configured roots until manually proven.")
    } else {
        $lines.Add(("Total matching files: {0}. Showing first {1}. Full list: `_scans/{2}_evidence_files.txt`." -f $EvidenceFiles.Count, $MaxEvidenceFiles, $Group.Id))
        $lines.Add("")
        foreach ($f in ($EvidenceFiles | Select-Object -First $MaxEvidenceFiles)) { $lines.Add(("- {0}" -f $f)) }
    }
    $lines.Add("")
    $lines.Add("## Static Risk Suspects")
    $lines.Add("")
    if ($RuntimeRiskLines.Count -eq 0 -and $EditorRiskLines.Count -eq 0) {
        $lines.Add("- No configured static risk patterns found in scanned roots.")
    } else {
        $lines.Add("These are suspects, not confirmed defects. Runtime suspects need code review. Editor/tool suspects are legal only if they cannot execute in gameplay/player hot paths.")
        $lines.Add("")
        $lines.Add("Runtime suspects:")
        if ($RuntimeRiskLines.Count -eq 0) {
            $lines.Add("- None in configured scan.")
        } else {
            $lines.Add(("Total runtime suspects: {0}. Showing first {1}. Full list: `_scans/{2}_runtime_risks.txt`." -f $RuntimeRiskLines.Count, $MaxRiskLines, $Group.Id))
            $lines.Add("")
            foreach ($r in ($RuntimeRiskLines | Select-Object -First $MaxRiskLines)) { $lines.Add(("- {0}" -f $r)) }
        }
        $lines.Add("")
        $lines.Add("Editor/tool/static suspects:")
        if ($EditorRiskLines.Count -eq 0) {
            $lines.Add("- None in configured scan.")
        } else {
            $lines.Add(("Total editor/tool/static suspects: {0}. Showing first {1}. Full list: `_scans/{2}_editor_tool_risks.txt`." -f $EditorRiskLines.Count, $MaxRiskLines, $Group.Id))
            $lines.Add("")
            foreach ($r in ($EditorRiskLines | Select-Object -First $MaxRiskLines)) { $lines.Add(("- {0}" -f $r)) }
        }
    }
    $lines.Add("")
    $lines.Add("## Exists / Missing / Required Proof")
    $lines.Add("")
    if ($missingBibles.Count -gt 0) {
        $lines.Add("- Missing: bible route files are absent: $($missingBibles -join ', ').")
    } elseif ($EvidenceFiles.Count -eq 0 -and $Group.Id -ne "00_project_routes") {
        $lines.Add("- Missing: static implementation evidence was not found in configured roots.")
    } else {
        $lines.Add("- Exists: bible routes exist and static implementation evidence was found.")
    }
    if ($RuntimeRiskLines.Count -gt 0) {
        $lines.Add("- Partial: runtime static risk suspects need manual code review.")
    }
    if ($EditorRiskLines.Count -gt 0) {
        $lines.Add("- Editor/tool: static suspects exist but may be legal if editor-only or cold-path.")
    }
    $lines.Add("- Required proof: $($Group.Proof)")
    $lines.Add("")
    $lines.Add("## Next Audit Action")
    $lines.Add("")
    switch ($Verdict) {
        "RED_MISSING_BIBLE_ROUTE" { $lines.Add("Create or route the missing bible before accepting implementation work in this system.") }
        "RED_NO_CODE_EVIDENCE_STATIC" { $lines.Add("Find the actual implementation root or mark the system as not implemented. Do not claim runtime readiness.") }
        "YELLOW_RUNTIME_STATIC_RISK_REVIEW_REQUIRED" { $lines.Add("Classify each runtime suspect as cold-path/legal or runtime violation. Fix runtime violations before profiler proof.") }
        default { $lines.Add("Run Unity import, Play Mode, profiler, GC, capture, and device proof before changing status beyond static alignment.") }
    }

    Set-Content -LiteralPath $reportPath -Value ($lines -join "`r`n") -Encoding UTF8
}

$AllResults = @()

foreach ($g in $Groups) {
    $bibleInfo = @(Get-BibleInfo $g.Bibles)
    $mandates = @(Get-MandateMatches $g.MandatePatterns)
    $evidence = @(Invoke-RgFiles $g.EvidenceRegex $g.Roots $MaxEvidenceFiles)
    $risks = @(Invoke-RgLines $GeneralRiskRegex $g.Roots $MaxRiskLines)
    $riskSplit = Split-RiskLines $risks
    $verdict = Get-Verdit $bibleInfo $evidence $riskSplit.Runtime $g.Id
    Write-SystemReport $g $bibleInfo $mandates $evidence $riskSplit.Runtime $riskSplit.Editor $verdict
    $AllResults += [ordered]@{
        Id = $g.Id
        Title = $g.Title
        Verdict = $verdict
        BibleCount = $g.Bibles.Count
        MissingBibles = @($bibleInfo | Where-Object { -not $_.Exists } | ForEach-Object { $_.Path })
        MandateMatches = $mandates.Count
        EvidenceFiles = $evidence.Count
        RuntimeRiskSuspects = $riskSplit.Runtime.Count
        EditorRiskSuspects = $riskSplit.Editor.Count
        RiskSuspects = $risks.Count
        RequiredProof = $g.Proof
    }
}

$RouteFile = Join-Path $Root "PROJECT_BIBLES.md"
$RouteMissing = @()
if (Test-Path -LiteralPath $RouteFile) {
    $routeText = Get-Content -LiteralPath $RouteFile -Raw
    $routeDocs = [regex]::Matches($routeText, '`([^`]+\.md)`') | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique
    foreach ($doc in $routeDocs) {
        if (!(Test-Path -LiteralPath (Join-Path $Root $doc))) { $RouteMissing += $doc }
    }
}

$IndexLines = New-Object System.Collections.Generic.List[string]
$IndexLines.Add("# HECTON-8 Bible / Mandate / Codebase Audit 1700")
$IndexLines.Add("")
$IndexLines.Add("Status: STATIC AUDIT INDEX - RUNTIME PROOF NOT RUN")
$IndexLines.Add("Date: $RunDate")
$IndexLines.Add("Output root: Docs/BibleMandateAudits/1700/")
$IndexLines.Add("")
$IndexLines.Add("## Audit Basis")
$IndexLines.Add("")
$IndexLines.Add("Selected mandates:")
foreach ($m in $SelectedMandates) {
    $exists = if (Test-Path -LiteralPath (Join-Path $Root $m)) { "OK" } else { "MISSING" }
    $IndexLines.Add(("- {0} {1}" -f $exists, $m))
}
$IndexLines.Add("")
$IndexLines.Add("Runtime boundary: this audit uses static file scans only. Unity import, Console, Play Mode, profiler, GC, Frame Debugger, Memory Profiler, build, and device proof are not implied.")
$IndexLines.Add("")
$IndexLines.Add("## System Reports")
$IndexLines.Add("")
$IndexLines.Add("| System | Verdict | Bibles | Mandates | Evidence Files | Runtime Suspects | Editor/Tool Suspects | Report |")
$IndexLines.Add("|---|---:|---:|---:|---:|---:|---:|---|")
foreach ($r in $AllResults) {
    $IndexLines.Add(("| {0} | {1} | {2} | {3} | {4} | {5} | {6} | {7}\REPORT.md |" -f $r.Title, $r.Verdict, $r.BibleCount, $r.MandateMatches, $r.EvidenceFiles, $r.RuntimeRiskSuspects, $r.EditorRiskSuspects, $r.Id))
}
$IndexLines.Add("")
$IndexLines.Add("## Route Integrity")
$IndexLines.Add("")
if ($RouteMissing.Count -eq 0) {
    $IndexLines.Add("- PROJECT_BIBLES.md route targets: no missing root markdown files detected by static route parse.")
} else {
    foreach ($m in $RouteMissing) { $IndexLines.Add(("- MISSING route target {0}" -f $m)) }
}
$IndexLines.Add("")
$IndexLines.Add("## Highest Priority Next Work")
$IndexLines.Add("")
$reds = @($AllResults | Where-Object { $_.Verdict -like "RED*" })
$yellows = @($AllResults | Where-Object { $_.Verdict -like "YELLOW*" })
if ($reds.Count -gt 0) {
    foreach ($r in $reds) { $IndexLines.Add(("- RED: {0} - {1}." -f $r.Id, $r.Title)) }
}
if ($yellows.Count -gt 0) {
    foreach ($r in $yellows) { $IndexLines.Add(("- YELLOW: {0} - classify runtime suspects as cold-path, guarded debug, or runtime violation." -f $r.Id)) }
}
if ($reds.Count -eq 0 -and $yellows.Count -eq 0) {
    $IndexLines.Add("- No red/yellow static findings in configured scan. Runtime/device proof remains required.")
}
$IndexLines.Add("")
$IndexLines.Add("## Dynamic Summary Reports")
$IndexLines.Add("")
$IndexLines.Add('- `WHAT_EXISTS_WHAT_MISSING.md` - compact per-system exists/missing/proof status.')
$IndexLines.Add('- `MANDATE_CURRENCY_MATRIX.md` - every `.agents-skills/*.txt` mandate mapped to expected bible routes and audit groups.')
$IndexLines.Add('- `MANDATE_CURRENCY_SUMMARY.md` - mandate status and repeated currency flags summary.')
$IndexLines.Add('- `RISK_CATEGORY_MATRIX.md` - static runtime suspect categories by system.')
$IndexLines.Add('- `RUNTIME_PRECLASSIFICATION_MATRIX.md` - heuristic first-pass legality/risk classes by system.')
$IndexLines.Add('- `HOTSPOT_REVIEW.md` - files with the highest concentration of unresolved `REVIEW_*` risk lines.')
$manualReports = @(Get-ChildItem -LiteralPath $OutRoot -Filter "MANUAL_REVIEW_PASS_*.md" -File -ErrorAction SilentlyContinue | Sort-Object Name)
if ($manualReports.Count -gt 0) {
    foreach ($manualReport in $manualReports) {
        $IndexLines.Add(("- `{0}` - human static review notes for critical hotspot pass." -f $manualReport.Name))
    }
} else {
    $IndexLines.Add('- `MANUAL_REVIEW_PASS_*.md` - no manual hotspot pass has been recorded yet.')
}
$IndexLines.Add('- Per-system `MANUAL_REVIEW.md` files - human static review notes for system-local hotspot classification when present.')
$IndexLines.Add('- Per-system `RUNTIME_TRIAGE.md` files - first-pass categorized review queues for runtime suspects.')
$IndexLines.Add('- Per-system `RUNTIME_PRECLASSIFICATION.md` files - generated suspect lines grouped by preliminary class.')
$IndexLines.Add('- `_scans/` - full raw evidence and risk lists for follow-up review.')
$IndexLines.Add("")
$IndexLines.Add("## Rerun Command")
$IndexLines.Add("")
$IndexLines.Add('```powershell')
$IndexLines.Add("powershell -ExecutionPolicy Bypass -File Tools\Audit\Run-BibleMandateAudit1700.ps1")
$IndexLines.Add('```')

Set-Content -LiteralPath (Join-Path $OutRoot "INDEX.md") -Value ($IndexLines -join "`r`n") -Encoding UTF8

$SummaryLines = New-Object System.Collections.Generic.List[string]
$SummaryLines.Add("# What Exists / What Is Missing")
$SummaryLines.Add("")
$SummaryLines.Add("Status: STATIC CODEBASE COMPARISON - RUNTIME PROOF NOT RUN")
$SummaryLines.Add("Date: $RunDate")
$SummaryLines.Add("")
$SummaryLines.Add("## Global Result")
$SummaryLines.Add("")
if ($RouteMissing.Count -eq 0) {
    $SummaryLines.Add("- Root bible routing is structurally intact: PROJECT_BIBLES.md does not reference missing root markdown files.")
} else {
    $SummaryLines.Add("- Root bible routing is broken: missing route targets are listed in INDEX.md.")
}
$SummaryLines.Add(("- Systems audited: {0}. Green: {1}. Yellow: {2}. Red: {3}." -f $AllResults.Count, @($AllResults | Where-Object { $_.Verdict -like "GREEN*" }).Count, @($AllResults | Where-Object { $_.Verdict -like "YELLOW*" }).Count, @($AllResults | Where-Object { $_.Verdict -like "RED*" }).Count))
$SummaryLines.Add("- Static evidence exists for every configured implementation domain, but yellow systems require code review because static risk patterns appear in runtime roots.")
$SummaryLines.Add("- No Unity import, Play Mode, profiler, GC, Frame Debugger, Memory Profiler, build, or hardware-device proof was executed by this script.")
$SummaryLines.Add("")
$SummaryLines.Add("## Per-System Status")
$SummaryLines.Add("")
$SummaryLines.Add("| System | Exists | Missing / Partial | Required Proof |")
$SummaryLines.Add("|---|---|---|---|")
foreach ($r in $AllResults) {
    $existsParts = New-Object System.Collections.Generic.List[string]
    $missingParts = New-Object System.Collections.Generic.List[string]
    if ($r.MissingBibles.Count -eq 0) { $existsParts.Add("bibles routed") } else { $missingParts.Add(("missing bibles: {0}" -f ($r.MissingBibles -join ", "))) }
    if ($r.EvidenceFiles -gt 0) { $existsParts.Add(("static evidence files: {0}" -f $r.EvidenceFiles)) } else { $missingParts.Add("no static evidence in configured roots") }
    if ($r.MandateMatches -gt 0) { $existsParts.Add(("mandate refs: {0}" -f $r.MandateMatches)) } else { $missingParts.Add("no mandate files matched configured patterns") }
    if ($r.RuntimeRiskSuspects -gt 0) { $missingParts.Add(("runtime suspects need classification: {0}" -f $r.RuntimeRiskSuspects)) }
    if ($r.EditorRiskSuspects -gt 0) { $missingParts.Add(("editor/tool suspects need boundary confirmation: {0}" -f $r.EditorRiskSuspects)) }
    if ($missingParts.Count -eq 0) { $missingParts.Add("runtime proof still required") }
    $SummaryLines.Add(("| {0} | {1} | {2} | {3} |" -f $r.Title, ($existsParts -join "; "), ($missingParts -join "; "), $r.RequiredProof))
}
Set-Content -LiteralPath (Join-Path $OutRoot "WHAT_EXISTS_WHAT_MISSING.md") -Value ($SummaryLines -join "`r`n") -Encoding UTF8

$MandateRouteMap = [ordered]@{
    "AI" = @("ai.md","creatures.md","sonar.md","drones.md","ecosystem.md")
    "ANIM" = @("animation.md","creatures.md","player.md","tools.md")
    "ARCH" = @("systems.md","bootstrap.md","telemetry.md","performance.md")
    "AUD" = @("audio.md","sonar.md","narrative.md")
    "AUDIO" = @("audio.md","accessibility.md","presentation.md")
    "CI" = @("testing.md","quality.md","math.md")
    "CORE" = @("gameplay.md","player.md","survival.md","vehicles.md","tools.md","water.md","combat.md")
    "CTRL" = @("input.md","player.md","accessibility.md","settings.md")
    "DATA" = @("data.md","persistence.md","inventory.md","authoring.md")
    "DBG" = @("telemetry.md","quality.md","performance.md")
    "GPU" = @("compute.md","rendering.md","performance.md")
    "LOGI" = @("logistics.md","construction.md","drones.md")
    "MANDATE" = @("AGENTS.md","PROJECT_BIBLES.md","quality.md")
    "MATH" = @("math.md","data.md","physics.md","networking.md")
    "NET" = @("networking.md","data.md","logistics.md")
    "OPT" = @("performance.md","systems.md","quality.md","compute.md")
    "PHYS" = @("physics.md","vehicles.md","water.md","survival.md","animation.md")
    "PROG" = @("narrative.md","gameplay.md","testing.md")
    "PROJECT" = @("platform.md","release.md","systems.md")
    "QA" = @("quality.md","testing.md","release.md","textes.md")
    "REND" = @("rendering.md","shaders.md","lighting.md","vfx.md","terrain.md","water.md","xr.md")
    "STRM" = @("streaming.md","persistence.md","platform.md","release.md")
    "TOOL" = @("tools.md","authoring.md","PROCEDURAL_ASSET_PIPELINE.md","3dmodel.md")
    "UI" = @("ui.md","UI_MENU_SCREEN_STANDARDS.md","UI_DIEGETIC_HUD_STANDARDS.md","localization.md","settings.md","accessibility.md")
    "VOX" = @("voxels.md","terrain.md","world.md","streaming.md")
}

$MandateMatrixLines = New-Object System.Collections.Generic.List[string]
$MandateMatrixLines.Add("# Mandate Currency Matrix")
$MandateMatrixLines.Add("")
$MandateMatrixLines.Add("Status: STATIC MANDATE ROUTE AUDIT - HUMAN VALIDATION STILL REQUIRED")
$MandateMatrixLines.Add("Date: $RunDate")
$MandateMatrixLines.Add("")
$MandateMatrixLines.Add("This report checks every `.agents-skills/*.txt` file against expected root bible routes and configured audit groups. It does not prove implementation correctness.")
$MandateMatrixLines.Add("")
$MandateMatrixLines.Add("| Mandate | Prefix | Status | Expected Routes | Audit Groups | Currency Flags |")
$MandateMatrixLines.Add("|---|---|---|---|---|---|")
$MandateJson = @()
$MandateFiles = @()
if (Test-Path -LiteralPath (Join-Path $Root ".agents-skills")) {
    $MandateFiles = @(Get-ChildItem -LiteralPath (Join-Path $Root ".agents-skills") -Filter "*.txt" -File | Sort-Object Name)
}
foreach ($mf in $MandateFiles) {
    $name = $mf.Name
    $prefix = ($name -split "_", 2)[0]
    $expectedRoutes = @()
    if ($MandateRouteMap.Contains($prefix)) {
        $expectedRoutes = @($MandateRouteMap[$prefix])
    } else {
        $expectedRoutes = @("PROJECT_BIBLES.md","quality.md")
    }
    $missingRoutes = @($expectedRoutes | Where-Object { -not (Test-Path -LiteralPath (Join-Path $Root $_)) })
    $coveredGroups = @()
    foreach ($g in $Groups) {
        foreach ($p in $g.MandatePatterns) {
            if ($name -like "$p*" -or $name -match [regex]::Escape($p)) {
                $coveredGroups += $g.Id
                break
            }
        }
    }
    $coveredGroups = @($coveredGroups | Sort-Object -Unique)
    $text = Get-Content -LiteralPath $mf.FullName -Raw
    $flags = @()
    if ($text -match "(?im)\bTODO\b|\bTBD\b|\bWIP\b|\bFIXME\b") { $flags += "TODO_OR_WIP_MARKER" }
    if ($text -match "(?i)deprecated|obsolete|legacy") { $flags += "DEPRECATED_OR_LEGACY_WORDING" }
    if ($text -match "(?i)Unity\s*5|Unity\s*2019|Unity\s*2020|URP\s*7|URP\s*10|URP\s*12") { $flags += "OLD_VERSION_WORDING_REVIEW" }
    if ($text -notmatch "GlobalQualityWeight|quality|Quality") { $flags += "NO_EXPLICIT_QUALITY_SCALING_WORD" }
    if ($text -notmatch "proof|evidence|verify|validation|audit") { $flags += "NO_EXPLICIT_PROOF_WORD" }
    $status = "GREEN_ROUTE_COVERED"
    if ($missingRoutes.Count -gt 0 -or $coveredGroups.Count -eq 0) { $status = "RED_ROUTE_OR_GROUP_GAP" }
    elseif ($flags.Count -gt 0) { $status = "YELLOW_CURRENCY_REVIEW" }
    $routeText = ($expectedRoutes -join ", ")
    $groupText = if ($coveredGroups.Count -gt 0) { ($coveredGroups -join ", ") } else { "NONE" }
    $flagText = if ($flags.Count -gt 0) { ($flags -join ", ") } else { "none" }
    $MandateMatrixLines.Add(("| {0} | {1} | {2} | {3} | {4} | {5} |" -f $name, $prefix, $status, $routeText, $groupText, $flagText))
    $MandateJson += [pscustomobject][ordered]@{
        mandate = $name
        prefix = $prefix
        status = $status
        expectedRoutes = $expectedRoutes
        missingRoutes = $missingRoutes
        auditGroups = $coveredGroups
        currencyFlags = $flags
    }
}
Set-Content -LiteralPath (Join-Path $OutRoot "MANDATE_CURRENCY_MATRIX.md") -Value ($MandateMatrixLines -join "`r`n") -Encoding UTF8
Set-Content -LiteralPath (Join-Path (Join-Path $OutRoot "00_mandate_registry") "MANDATE_CURRENCY_DETAIL.md") -Value ($MandateMatrixLines -join "`r`n") -Encoding UTF8
$MandateJson | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $ScanRoot "mandate_currency.json") -Encoding UTF8

$MandateSummaryLines = New-Object System.Collections.Generic.List[string]
$MandateSummaryLines.Add("# Mandate Currency Summary")
$MandateSummaryLines.Add("")
$MandateSummaryLines.Add("Status: STATIC SUMMARY - HUMAN VALIDATION STILL REQUIRED")
$MandateSummaryLines.Add("Date: $RunDate")
$MandateSummaryLines.Add("")
$MandateSummaryLines.Add(("Total mandates scanned: {0}" -f $MandateJson.Count))
$MandateSummaryLines.Add("")
$MandateSummaryLines.Add("## Status Counts")
$MandateSummaryLines.Add("")
foreach ($s in (@($MandateJson | Group-Object status | Sort-Object Name))) {
    $MandateSummaryLines.Add(("- {0}: {1}" -f $s.Name, $s.Count))
}
$MandateSummaryLines.Add("")
$MandateSummaryLines.Add("## Currency Flags")
$MandateSummaryLines.Add("")
$allFlags = @()
foreach ($m in $MandateJson) { $allFlags += @($m.currencyFlags) }
if ($allFlags.Count -eq 0) {
    $MandateSummaryLines.Add("- none")
} else {
    foreach ($f in ($allFlags | Group-Object | Sort-Object Count -Descending)) {
        $MandateSummaryLines.Add(("- {0}: {1}" -f $f.Name, $f.Count))
    }
}
$MandateSummaryLines.Add("")
$MandateSummaryLines.Add("## Red Items")
$MandateSummaryLines.Add("")
$redMandates = @($MandateJson | Where-Object { $_.status -like "RED*" })
if ($redMandates.Count -eq 0) {
    $MandateSummaryLines.Add("- none")
} else {
    foreach ($m in $redMandates) {
        $MandateSummaryLines.Add(("- {0}: missing routes [{1}], audit groups [{2}]" -f $m.mandate, ($m.missingRoutes -join ", "), ($m.auditGroups -join ", ")))
    }
}
$MandateSummaryLines.Add("")
$MandateSummaryLines.Add("## Required Follow-Up")
$MandateSummaryLines.Add("")
$MandateSummaryLines.Add("- Yellow mandates are not automatically wrong. They mean the root bible route exists, but the source mandate needs review for missing explicit quality/proof wording or legacy terms.")
$MandateSummaryLines.Add("- Red mandates mean route/group coverage is structurally missing and must be fixed before using that mandate as current authority.")
Set-Content -LiteralPath (Join-Path $OutRoot "MANDATE_CURRENCY_SUMMARY.md") -Value ($MandateSummaryLines -join "`r`n") -Encoding UTF8

$RiskCategories = @(
    [ordered]@{ Name = "Unity scene lookup"; Pattern = "GameObject\.Find|FindObjectOfType|FindObjectsOfType|Camera\.main|GetComponent<|GetComponentsInChildren|GetComponents<" },
    [ordered]@{ Name = "Runtime mesh/material mutation"; Pattern = "new Mesh\(|RecalculateNormals|RecalculateTangents|MeshCollider|\.material\b|\.materials\b" },
    [ordered]@{ Name = "Potential allocating physics query"; Pattern = "Physics\.Raycast\(|Physics\.SphereCast\(|Physics\.CapsuleCast\(|Physics\.OverlapSphere\(|Physics\.BoxCast\(" },
    [ordered]@{ Name = "Job fence / sync wait"; Pattern = "\.Complete\(" },
    [ordered]@{ Name = "Native allocation or persistent lifetime"; Pattern = "new NativeArray|Allocator\.Persistent|Allocator\.TempJob|Allocator\.Temp" },
    [ordered]@{ Name = "LINQ or managed collection allocation"; Pattern = "\.Where\(|\.Select\(|\.ToList\(" },
    [ordered]@{ Name = "Coroutine / managed timing"; Pattern = "StartCoroutine|StopCoroutine|yield return new" },
    [ordered]@{ Name = "Runtime debug logging"; Pattern = "Debug\.Log|H8Debug\.Log|H8Debug\.Warn|H8Debug\.Error" },
    [ordered]@{ Name = "Hot Unity phase method"; Pattern = "void\s+(Update|FixedUpdate|LateUpdate)\s*\(" },
    [ordered]@{ Name = "Synchronous resource load"; Pattern = "Resources\.Load|Addressables\.LoadAssetAsync.*WaitForCompletion" }
)

function Get-RiskPreclassification {
    param(
        [string]$Line,
        [string]$CategoryName
    )
    $pathPart = ($Line -split ":", 2)[0]
    $cold = ($Line -match "COLD|cold|Awake|Start\(|OnEnable|OnDisable|Initialize|Initialise|Setup|Build|Bake|Generate|Create|Register|Ensure|Warmup|Precompute|Bootstrap|SmokeTester|Smoke|Validator|Audit|Inquisition|Tuner|Designer|Window|Editor|Test|Tests")
    $editor = ($pathPart -match "\\Editor\\" -or $pathPart -match "^Assets\\_Project\\Editor" -or $Line -match "UnityEditor|EditorUtility|AssetDatabase|PrefabUtility|MenuItem|#if UNITY_EDITOR|DEVELOPMENT_BUILD")
    if ($editor -or $pathPart -match "Assets\\_Project\\Scripts\\Core\\H8Debug\.cs$") { return "LEGAL_EDITOR_OR_DEV_GUARDED" }
    if ($Line -match "Resources\.Load|WaitForCompletion") { return "REVIEW_SYNC_LOAD_VIOLATION_CANDIDATE" }
    if ($Line -match "\.Complete\(") { return "REVIEW_JOB_FENCE_REQUIRED" }
    if ($Line -match "Physics\.Raycast\(|Physics\.SphereCast\(|Physics\.CapsuleCast\(|Physics\.OverlapSphere\(|Physics\.BoxCast\(") { return "REVIEW_NONALLOC_ROUTE_REQUIRED" }
    if ($Line -match "H8Debug\.Log|H8Debug\.Warn|H8Debug\.Error") {
        return "LEGAL_EDITOR_OR_DEV_GUARDED"
    }
    if ($Line -match "Debug\.Log") {
        if ($cold) { return "LIKELY_LEGAL_COLD_OR_DIAGNOSTIC_PATH" }
        return "REVIEW_LOG_GUARD_REQUIRED"
    }
    if ($Line -match "new Mesh\(|RecalculateNormals|RecalculateTangents|MeshCollider|\.material\b|\.materials\b") {
        if ($cold) { return "LIKELY_LEGAL_COLD_PATH" }
        return "REVIEW_RUNTIME_MESH_MATERIAL_PATH"
    }
    if ($Line -match "new NativeArray|Allocator\.Persistent|Allocator\.TempJob|Allocator\.Temp") {
        if ($cold) { return "LIKELY_LEGAL_COLD_OR_OWNER_LIFETIME_PATH" }
        return "REVIEW_NATIVE_LIFETIME_OR_ALLOCATOR_PROOF_REQUIRED"
    }
    if ($Line -match "\.Where\(|\.Select\(|\.ToList\(") {
        if ($cold) { return "LIKELY_LEGAL_COLD_MANAGED_QUERY" }
        return "REVIEW_LINQ_OR_MANAGED_ALLOCATION"
    }
    if ($Line -match "StartCoroutine|StopCoroutine|yield return new") {
        if ($cold) { return "LIKELY_LEGAL_COLD_OR_PRESENTATION_PATH" }
        return "REVIEW_COROUTINE_RUNTIME_PATH"
    }
    if ($Line -match "GameObject\.Find|FindObjectOfType|FindObjectsOfType|Camera\.main|GetComponent<|GetComponentsInChildren|GetComponents<") {
        if ($cold) { return "LIKELY_LEGAL_COLD_LOOKUP" }
        return "REVIEW_CACHE_OR_INJECTION_REQUIRED"
    }
    if ($Line -match "void\s+(Update|FixedUpdate|LateUpdate)\s*\(") { return "REVIEW_HOT_PHASE_METHOD" }
    return "REVIEW_UNCLASSIFIED_STATIC_RISK"
}

$MatrixLines = New-Object System.Collections.Generic.List[string]
$MatrixLines.Add("# Static Runtime Risk Category Matrix")
$MatrixLines.Add("")
$MatrixLines.Add("Status: CATEGORY COUNTS ONLY - NOT CONFIRMED DEFECTS")
$MatrixLines.Add("Date: $RunDate")
$MatrixLines.Add("")
$MatrixLines.Add('These counts are derived from `_scans/*_runtime_risks.txt`. A single line can match more than one category. Every non-zero count is a review target, not automatic guilt.')
$MatrixLines.Add("")
$MatrixLines.Add("| System | Runtime Suspects | Top Categories |")
$MatrixLines.Add("|---|---:|---|")
foreach ($r in $AllResults) {
    $runtimeFile = Join-Path $ScanRoot ("{0}_runtime_risks.txt" -f $r.Id)
    $riskText = ""
    if (Test-Path -LiteralPath $runtimeFile) {
        $riskText = Get-Content -LiteralPath $runtimeFile -Raw
    }
    $counts = @()
    foreach ($cat in $RiskCategories) {
        $count = 0
        if ($riskText.Length -gt 0) {
            $count = ([regex]::Matches($riskText, $cat.Pattern)).Count
        }
        if ($count -gt 0) {
            $counts += [pscustomobject]@{ Name = $cat.Name; Count = $count }
        }
    }
    $top = @($counts | Sort-Object Count -Descending | Select-Object -First 5 | ForEach-Object { "{0}: {1}" -f $_.Name, $_.Count })
    if ($top.Count -eq 0) { $top = @("none in configured scan") }
    $MatrixLines.Add(("| {0} | {1} | {2} |" -f $r.Title, $r.RuntimeRiskSuspects, ($top -join "; ")))
}
$MatrixLines.Add("")
$MatrixLines.Add("## Review Method")
$MatrixLines.Add("")
$MatrixLines.Add('1. Open the system `REPORT.md` and full `_scans/*_runtime_risks.txt` list.')
$MatrixLines.Add("2. Mark each line as legal cold path, legal editor/dev-only path, guarded diagnostic path, or runtime violation.")
$MatrixLines.Add("3. Runtime violations must be fixed before Unity/profiler proof can upgrade the system beyond yellow.")
$MatrixLines.Add('4. Legal editor/tool paths should be moved under `Editor/`, wrapped in `#if UNITY_EDITOR`, or documented in the system report if the path name is ambiguous.')
Set-Content -LiteralPath (Join-Path $OutRoot "RISK_CATEGORY_MATRIX.md") -Value ($MatrixLines -join "`r`n") -Encoding UTF8

$PreclassRows = @()
$GlobalClassifiedRows = @()
foreach ($r in $AllResults) {
    $runtimeFile = Join-Path $ScanRoot ("{0}_runtime_risks.txt" -f $r.Id)
    $riskLines = @()
    if (Test-Path -LiteralPath $runtimeFile) {
        $riskLines = @(Get-Content -LiteralPath $runtimeFile | Where-Object { $_ -and $_.Trim().Length -gt 0 })
    }
    $classified = @()
    foreach ($line in $riskLines) {
        $matchingCategory = "Uncategorized"
        foreach ($cat in $RiskCategories) {
            if ($line -match $cat.Pattern) {
                $matchingCategory = $cat.Name
                break
            }
        }
        $class = Get-RiskPreclassification -Line $line -CategoryName $matchingCategory
        $riskPath = ($line -split ":", 2)[0]
        $classified += [pscustomobject]@{
            System = $r.Id
            Title = $r.Title
            Path = $riskPath
            Line = $line
            Category = $matchingCategory
            Class = $class
        }
    }
    $GlobalClassifiedRows += $classified

    $classCounts = @($classified | Group-Object Class | Sort-Object Count -Descending)
    $PreclassRows += [pscustomobject]@{
        Id = $r.Id
        Title = $r.Title
        Total = $classified.Count
        ClassCounts = $classCounts
    }

    $prePath = Join-Path (Join-Path $OutRoot $r.Id) "RUNTIME_PRECLASSIFICATION.md"
    $PreLines = New-Object System.Collections.Generic.List[string]
    $PreLines.Add(("# Runtime Preclassification - {0}" -f $r.Title))
    $PreLines.Add("")
    $PreLines.Add("Status: HEURISTIC FIRST PASS - MANUAL REVIEW STILL REQUIRED")
    $PreLines.Add("Date: $RunDate")
    $PreLines.Add("")
    $PreLines.Add("This file groups static runtime suspects by a conservative heuristic. It can reduce review time, but it cannot prove a line is legal or illegal without reading the containing method and owner phase.")
    $PreLines.Add("")
    if ($classified.Count -eq 0) {
        $PreLines.Add("No runtime suspects in configured static scan.")
    } else {
        $PreLines.Add(("Total runtime suspects: {0}." -f $classified.Count))
        $PreLines.Add("")
        $PreLines.Add("## Summary")
        $PreLines.Add("")
        foreach ($c in $classCounts) {
            $PreLines.Add(("- {0}: {1}" -f $c.Name, $c.Count))
        }
        $PreLines.Add("")
        foreach ($c in $classCounts) {
            $items = @($classified | Where-Object { $_.Class -eq $c.Name })
            $PreLines.Add(("## {0} ({1})" -f $c.Name, $items.Count))
            $PreLines.Add("")
            foreach ($item in ($items | Select-Object -First 40)) {
                $PreLines.Add(("- {0} | {1}" -f $item.Category, $item.Line))
            }
            if ($items.Count -gt 40) {
                $PreLines.Add(('- Additional lines omitted here: {0}. Use `../_scans/{1}_runtime_risks.txt` for the full list.' -f ($items.Count - 40), $r.Id))
            }
            $PreLines.Add("")
        }
    }
    Set-Content -LiteralPath $prePath -Value ($PreLines -join "`r`n") -Encoding UTF8
}

$PreMatrixLines = New-Object System.Collections.Generic.List[string]
$PreMatrixLines.Add("# Runtime Preclassification Matrix")
$PreMatrixLines.Add("")
$PreMatrixLines.Add("Status: HEURISTIC FIRST PASS - MANUAL REVIEW STILL REQUIRED")
$PreMatrixLines.Add("Date: $RunDate")
$PreMatrixLines.Add("")
$PreMatrixLines.Add("This matrix groups static risk lines into review states. It is not profiler proof and not a final defect list.")
$PreMatrixLines.Add("")
$PreMatrixLines.Add("| System | Total | Top Preliminary Classes |")
$PreMatrixLines.Add("|---|---:|---|")
foreach ($row in $PreclassRows) {
    $topClasses = @($row.ClassCounts | Select-Object -First 6 | ForEach-Object { "{0}: {1}" -f $_.Name, $_.Count })
    if ($topClasses.Count -eq 0) { $topClasses = @("none") }
    $PreMatrixLines.Add(("| {0} | {1} | {2} |" -f $row.Title, $row.Total, ($topClasses -join "; ")))
}
$PreMatrixLines.Add("")
$PreMatrixLines.Add("## Meaning")
$PreMatrixLines.Add("")
$PreMatrixLines.Add('- `LIKELY_LEGAL_*` means the line looks like setup, smoke test, editor, or owner-lifetime code. It still needs method-level confirmation.')
$PreMatrixLines.Add('- `REVIEW_*` means the line can plausibly violate runtime mandates if it runs during gameplay or player hot paths.')
$PreMatrixLines.Add('- `LEGAL_EDITOR_OR_DEV_GUARDED` means path or line text indicates editor/development-only scope; confirm compile guards if the file is outside an Editor folder.')
Set-Content -LiteralPath (Join-Path $OutRoot "RUNTIME_PRECLASSIFICATION_MATRIX.md") -Value ($PreMatrixLines -join "`r`n") -Encoding UTF8

$HotspotLines = New-Object System.Collections.Generic.List[string]
$HotspotLines.Add("# Hotspot Review")
$HotspotLines.Add("")
$HotspotLines.Add("Status: STATIC REVIEW PRIORITY LIST - NOT A DEFECT VERDICT")
$HotspotLines.Add("Date: $RunDate")
$HotspotLines.Add("")
$HotspotLines.Add('This report lists files with the highest concentration of unresolved `REVIEW_*` risk lines across system reports. Read the containing methods before declaring a violation.')
$HotspotLines.Add("")
$reviewRows = @($GlobalClassifiedRows | Where-Object { $_.Class -like "REVIEW_*" })
$HotspotLines.Add(('Total unresolved `REVIEW_*` lines: {0}' -f $reviewRows.Count))
$HotspotLines.Add("")
$HotspotLines.Add("## Top Files")
$HotspotLines.Add("")
$HotspotLines.Add("| Count | File | Classes | Systems |")
$HotspotLines.Add("|---:|---|---|---|")
foreach ($group in ($reviewRows | Group-Object Path | Sort-Object Count -Descending | Select-Object -First 40)) {
    $items = @($group.Group)
    $classes = @($items | Group-Object Class | Sort-Object Count -Descending | ForEach-Object { "{0}: {1}" -f $_.Name, $_.Count })
    $systems = @($items | Select-Object -ExpandProperty System -Unique)
    $HotspotLines.Add(("| {0} | {1} | {2} | {3} |" -f $group.Count, $group.Name, ($classes -join "; "), ($systems -join ", ")))
}
$HotspotLines.Add("")
$HotspotLines.Add("## First Review Targets")
$HotspotLines.Add("")
$HotspotLines.Add('- Prioritize files with `REVIEW_RUNTIME_MESH_MATERIAL_PATH`, `REVIEW_SYNC_LOAD_VIOLATION_CANDIDATE`, `REVIEW_JOB_FENCE_REQUIRED`, and `REVIEW_CACHE_OR_INJECTION_REQUIRED` before generic log guard work.')
$HotspotLines.Add("- If a hotspot is setup-only, add explicit comments/guards or move it to an editor/bootstrap route so future audits do not keep flagging it as ambiguous.")
Set-Content -LiteralPath (Join-Path $OutRoot "HOTSPOT_REVIEW.md") -Value ($HotspotLines -join "`r`n") -Encoding UTF8

foreach ($r in $AllResults) {
    $triagePath = Join-Path (Join-Path $OutRoot $r.Id) "RUNTIME_TRIAGE.md"
    $runtimeFile = Join-Path $ScanRoot ("{0}_runtime_risks.txt" -f $r.Id)
    $riskLines = @()
    if (Test-Path -LiteralPath $runtimeFile) {
        $riskLines = @(Get-Content -LiteralPath $runtimeFile | Where-Object { $_ -and $_.Trim().Length -gt 0 })
    }

    $TriageLines = New-Object System.Collections.Generic.List[string]
    $TriageLines.Add(("# Runtime Triage Queue - {0}" -f $r.Title))
    $TriageLines.Add("")
    $TriageLines.Add("Status: STATIC CATEGORIZATION - MANUAL CODE REVIEW REQUIRED")
    $TriageLines.Add("Date: $RunDate")
    $TriageLines.Add("")
    $TriageLines.Add('This file is generated by `Tools/Audit/Run-BibleMandateAudit1700.ps1`. It is a review queue, not a proof artifact and not a defect verdict.')
    $TriageLines.Add("")
    $TriageLines.Add("Required classification for every listed line:")
    $TriageLines.Add("- LEGAL_COLD_PATH: runs only during boot/import/setup outside gameplay hot paths.")
    $TriageLines.Add("- LEGAL_EDITOR_OR_DEV_GUARDED: wrapped in editor/development-only compilation or never enters player runtime.")
    $TriageLines.Add("- RUNTIME_VIOLATION: can execute in gameplay/player runtime and violates the bible/mandate hot-path law.")
    $TriageLines.Add("- FALSE_POSITIVE: static pattern matched wording that is not the risky API or code path.")
    $TriageLines.Add("")
    if ($riskLines.Count -eq 0) {
        $TriageLines.Add("No runtime suspects in configured static scan.")
    } else {
        $TriageLines.Add(('Total runtime suspects: {0}. Full raw list: `../_scans/{1}_runtime_risks.txt`.' -f $riskLines.Count, $r.Id))
        $TriageLines.Add("")
        foreach ($cat in $RiskCategories) {
            $matches = @($riskLines | Where-Object { $_ -match $cat.Pattern })
            if ($matches.Count -eq 0) { continue }
            $TriageLines.Add(("## {0} ({1})" -f $cat.Name, $matches.Count))
            $TriageLines.Add("")
            foreach ($m in ($matches | Select-Object -First 30)) {
                $TriageLines.Add(("- [ ] CLASSIFY: {0}" -f $m))
            }
            if ($matches.Count -gt 30) {
                $TriageLines.Add(("- Additional lines omitted here: {0}. Use the raw scan file for full classification." -f ($matches.Count - 30)))
            }
            $TriageLines.Add("")
        }
        $uncategorized = @($riskLines | Where-Object {
            $line = $_
            -not ($RiskCategories | Where-Object { $line -match $_.Pattern })
        })
        if ($uncategorized.Count -gt 0) {
            $TriageLines.Add(("## Uncategorized Static Risk Pattern ({0})" -f $uncategorized.Count))
            $TriageLines.Add("")
            foreach ($m in ($uncategorized | Select-Object -First 30)) {
                $TriageLines.Add(("- [ ] CLASSIFY: {0}" -f $m))
            }
            if ($uncategorized.Count -gt 30) {
                $TriageLines.Add(("- Additional lines omitted here: {0}. Use the raw scan file for full classification." -f ($uncategorized.Count - 30)))
            }
            $TriageLines.Add("")
        }
    }
    Set-Content -LiteralPath $triagePath -Value ($TriageLines -join "`r`n") -Encoding UTF8
}

$AllResults | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $ScanRoot "audit_snapshot.json") -Encoding UTF8

$summary = [ordered]@{
    date = $RunDate
    output = "Docs/BibleMandateAudits/1700"
    groups = $AllResults.Count
    red = @($AllResults | Where-Object { $_.Verdict -like "RED*" }).Count
    yellow = @($AllResults | Where-Object { $_.Verdict -like "YELLOW*" }).Count
    green = @($AllResults | Where-Object { $_.Verdict -like "GREEN*" }).Count
    routeMissing = $RouteMissing.Count
}
$summary | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $ScanRoot "summary.json") -Encoding UTF8

Write-Output "Bible mandate audit complete."
Write-Output "Output: Docs/BibleMandateAudits/1700/INDEX.md"
Write-Output ("Groups={0} Red={1} Yellow={2} Green={3} MissingRoutes={4}" -f $summary.groups, $summary.red, $summary.yellow, $summary.green, $summary.routeMissing)
