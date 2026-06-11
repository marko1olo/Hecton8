// H8BlackboxWriters.cs — Output generation for Hecton8 Blackbox Diagnostics
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Hecton8.BlackboxDiagnostics
{
    public static class H8Writers
    {
        public static void WriteRunSummary(string outputPath, H8RunSummary summary)
        {
            try
            {
                string path = Path.Combine(outputPath, "run_summary.json");
                string json = JsonUtility.ToJson(summary, true);
                H8Utils.WriteFile(path, json);
            }
            catch (Exception e) { Debug.LogWarning($"[H8Blackbox] Failed to write run summary: {e.Message}"); }
        }

        public static void WriteSnapshot(string outputPath, H8DiagnosticSnapshot snapshot, string filename)
        {
            try
            {
                string path = Path.Combine(outputPath, filename);
                string json = JsonUtility.ToJson(snapshot, true);
                H8Utils.WriteFile(path, json);
            }
            catch (Exception e) { Debug.LogWarning($"[H8Blackbox] Failed to write snapshot: {e.Message}"); }
        }

        public static void WriteFindings(string outputPath, List<H8Finding> findings)
        {
            WriteFindings(outputPath, findings, "findings.md");
        }

        public static void WriteFindings(string outputPath, List<H8Finding> findings, string filename)
        {
            try
            {
                string path = Path.Combine(outputPath, filename);
                var sb = new StringBuilder();
                sb.AppendLine($"# Hecton8 Blackbox Findings ({filename})");
                sb.AppendLine();

                var severities = new[] { "Critical", "Error", "Warning", "Info" };
                foreach (var sev in severities)
                {
                    sb.AppendLine($"## {sev} Findings");
                    int count = 0;
                    foreach (var f in findings)
                    {
                        if (f.severity != sev) continue;
                        count++;
                        sb.AppendLine($"### [{f.id}] {f.title}");
                        sb.AppendLine($"- **Category:** {f.category}");
                        sb.AppendLine($"- **Evidence:** {f.evidence}");
                        sb.AppendLine($"- **Measured Value:** `{f.measuredValue}`");
                        sb.AppendLine($"- **Why It Matters:** {f.whyItMatters}");
                        sb.AppendLine($"- **Confidence:** {f.confidence}%");
                        sb.AppendLine($"- **Next Check:** {f.nextCheck}");
                        sb.AppendLine($"- **Likely Fix:** {f.likelyFix}");
                        sb.AppendLine();
                    }
                    if (count == 0) sb.AppendLine("_None._\n");
                }

                H8Utils.WriteFile(path, sb.ToString());
            }
            catch (Exception e) { Debug.LogWarning($"[H8Blackbox] Failed to write findings: {e.Message}"); }
        }

        public static void WriteReport(string outputPath, H8DiagnosticSnapshot snapshot, List<H8Finding> findings)
        {
            try
            {
                string path = Path.Combine(outputPath, "report.md");
                var sb = new StringBuilder();
                sb.AppendLine("# Hecton8 Full Diagnostic Report");
                sb.AppendLine($"**Generated:** {snapshot.timestamp} | **Mode:** {snapshot.mode}");
                sb.AppendLine($"**Active Scene:** `{snapshot.project.activeScene}`");
                sb.AppendLine();

                sb.AppendLine("## Top Findings");
                foreach (var f in findings)
                {
                    if (f.severity == "Critical" || f.severity == "Error")
                    {
                        sb.AppendLine($"- **[{f.severity}]** {f.title}: `{f.measuredValue}`");
                    }
                }
                sb.AppendLine();

                sb.AppendLine("## 23 Diagnostics Questions");
                sb.AppendLine($"1. **Did bootstrap run?** -> `{snapshot.registry.registryPhaseName}` (Phase {snapshot.registry.registryPhase})");
                sb.AppendLine($"2. **Is GlobalRegistry ready?** -> `{snapshot.registry.inferredState}`");
                
                var nullSlots = new List<string>();
                int missingCount = 0;
                int filledCount = 0;
                foreach (var s in snapshot.registry.slots)
                {
                    if (!s.memberFound) missingCount++;
                    else if (s.isNull) nullSlots.Add(s.slotName);
                    else filledCount++;
                }
                sb.AppendLine($"3. **Registry Slots:** `Null={nullSlots.Count}, Missing={missingCount}, Filled={filledCount}`");
                sb.AppendLine($"   - Null slots: `{(nullSlots.Count > 0 ? string.Join(", ", nullSlots) : "None")}`");
                
                sb.AppendLine($"4. **Is this direct 02_HECTON_WORLD start?** -> `{snapshot.bootstrap.inferredState}`");
                sb.AppendLine($"5. **Is Ocean_Crest active?** -> `{snapshot.crest.oceanCrestActive}` (hierarchy: {snapshot.crest.oceanCrestActiveInHierarchy})");
                sb.AppendLine($"6. **Is OceanRenderer active and enabled?** -> Active: `{snapshot.crest.oceanRendererActive}`, Enabled: `{snapshot.crest.oceanRendererEnabled}`");
                sb.AppendLine($"7. **Is Crest4KinematicsAdapter active?** -> `{snapshot.crest.adapterActive}` (enabled: {snapshot.crest.adapterEnabled})");
                sb.AppendLine($"8. **Is OceanKinematics registered?** -> `{snapshot.crest.kinematicsRegistered}`");
                sb.AppendLine($"9. **Is MapMagicObject active?** -> `{snapshot.mapMagic.mapMagicObjectActive}`");
                sb.AppendLine($"10. **Is MapMagicRuntimeBridge active?** -> `{snapshot.mapMagic.runtimeBridgeActive}`");
                sb.AppendLine($"11. **Is MapMagic graph assigned?** -> `{snapshot.mapMagic.graphAssigned}` ({snapshot.mapMagic.graphAssetName})");
                sb.AppendLine($"12. **Are there any terrain generated?** -> `{snapshot.mapMagic.activeTerrainCount}` active");
                sb.AppendLine($"13. **Is MapMagic registered?** -> `{snapshot.mapMagic.registeredInGlobalRegistry}`");
                sb.AppendLine($"14. **Is HectonAtmosphereManager active?** -> `{snapshot.atmosphere.atmosphereManagerActive}`");
                sb.AppendLine($"15. **Is HectonCelestialEngine active?** -> `{snapshot.atmosphere.celestialEngineActive}`");
                sb.AppendLine($"16. **Are atmosphere and celestial registered?** -> Atmo: `{snapshot.atmosphere.atmosphereRegistered}`, Celestial: `{snapshot.atmosphere.celestialRegistered}`");
                sb.AppendLine($"17. **Which URP pipeline asset is active?** -> `{snapshot.urp.activeUrpAssetName}`");
                sb.AppendLine($"18. **Which URP Renderer is active?** -> `{snapshot.urp.activeRendererDataName}`");
                sb.AppendLine($"19. **Are Hecton features enabled?** -> (See feature list below)");
                
                bool mainCam = false;
                foreach (var c in snapshot.cameras) if (c.isMainCamera) mainCam = true;
                sb.AppendLine($"20. **Is there an active MainCamera?** -> `{mainCam}`");
                
                int totalErrors = snapshot.console.totalErrors;
                sb.AppendLine($"21. **Are there Console errors?** -> `{totalErrors}` errors detected");
                
                sb.AppendLine($"22. **What Unity objects are destroyed but accessed?** -> (Check findings for MissingReference exceptions)");
                sb.AppendLine($"23. **Is Git tree dirty?** -> `{(snapshot.git.modifiedFiles.Count > 0)}`");
                sb.AppendLine();

                sb.AppendLine("## URP Features");
                sb.AppendLine("| Feature | Type | Active |");
                sb.AppendLine("|---|---|---|");
                foreach (var feat in snapshot.urp.rendererFeatures)
                {
                    sb.AppendLine($"| {feat.name} | {feat.typeName} | `{feat.isActive}` |");
                }

                H8Utils.WriteFile(path, sb.ToString());
            }
            catch (Exception e) { Debug.LogWarning($"[H8Blackbox] Failed to write report: {e.Message}"); }
        }

        public static void WriteCompactHandoff(string outputPath, H8DiagnosticSnapshot snapshot, List<H8Finding> findings)
        {
            try
            {
                string path = Path.Combine(outputPath, "compact_handoff_for_claude.md");
                var sb = new StringBuilder();
                sb.AppendLine("# H8 Blackbox: AI Handoff");
                sb.AppendLine("## Measured Facts");
                sb.AppendLine($"- Bootstrap State: `{snapshot.bootstrap.inferredState}`");
                sb.AppendLine($"- Registry Phase: `{snapshot.registry.registryPhaseName}`");
                sb.AppendLine($"- MapMagic Graph: `{snapshot.mapMagic.graphAssigned}`");
                sb.AppendLine($"- MapMagic Active Terrains: `{snapshot.mapMagic.activeTerrainCount}`");
                sb.AppendLine($"- Crest OceanRenderer Enabled: `{snapshot.crest.oceanRendererEnabled}`");
                sb.AppendLine($"- URP Asset: `{snapshot.urp.activeUrpAssetName}`");
                sb.AppendLine($"- Main Camera Found: `{snapshot.cameras.Exists(c => c.isMainCamera)}`");
                sb.AppendLine($"- Console Errors: `{snapshot.console.totalErrors}`");
                sb.AppendLine();

                sb.AppendLine("## Critical Findings");
                foreach (var f in findings)
                {
                    if (f.severity == "Critical") sb.AppendLine($"- [{f.id}] {f.title}: {f.measuredValue}");
                }
                sb.AppendLine();
                sb.AppendLine("Given these measured facts, identify likely root cause and minimal next fix.");

                H8Utils.WriteFile(path, sb.ToString());
            }
            catch (Exception e) { Debug.LogWarning($"[H8Blackbox] Failed to write compact handoff: {e.Message}"); }
        }

        public static void WriteNextSteps(string outputPath, List<H8Finding> findings)
        {
            try
            {
                string path = Path.Combine(outputPath, "next_steps_for_agent.md");
                var sb = new StringBuilder();
                sb.AppendLine("# Recommended Next Actions");
                sb.AppendLine();
                
                int c = 1;
                foreach (var f in findings)
                {
                    if (f.severity == "Critical" || f.severity == "Error")
                    {
                        sb.AppendLine($"{c}. **Fix {f.category} Issue:** {f.likelyFix}");
                        c++;
                    }
                }
                
                sb.AppendLine();
                sb.AppendLine("## If you only ran EditMode:");
                sb.AppendLine("Consider running `H8Runner.RunPlayMode()` (once implemented) or pressing Play in the Editor to gather runtime metrics.");

                H8Utils.WriteFile(path, sb.ToString());
            }
            catch (Exception e) { Debug.LogWarning($"[H8Blackbox] Failed to write next steps: {e.Message}"); }
        }

        public static void WriteHierarchy(string outputPath, List<H8KeyObjectInfo> keyObjects)
        {
            try
            {
                string path = Path.Combine(outputPath, "hierarchy_editmode.txt");
                var sb = new StringBuilder();
                foreach (var ko in keyObjects)
                {
                    sb.AppendLine($"[Key: {ko.searchKey}] Exists={ko.exists}");
                    if (ko.exists)
                    {
                        sb.AppendLine($"Path: {ko.hierarchyPath}");
                        sb.AppendLine($"ActiveInHierarchy: {ko.activeInHierarchy}");
                        sb.AppendLine($"Layer: {ko.layerName}");
                        sb.AppendLine("Components:");
                        foreach (var comp in ko.components)
                        {
                            sb.AppendLine($"  - {comp.typeName} (Enabled={comp.enabled})");
                        }
                    }
                    sb.AppendLine("-----------------------------------------");
                }
                H8Utils.WriteFile(path, sb.ToString());
            }
            catch (Exception e) { Debug.LogWarning($"[H8Blackbox] Failed to write hierarchy: {e.Message}"); }
        }

        public static void WriteConsoleLogs(string outputPath, H8ConsoleInfo console)
        {
            try
            {
                string path = Path.Combine(outputPath, "raw_console_log.txt");
                var sb = new StringBuilder();
                foreach (var e in console.entries)
                {
                    sb.AppendLine($"[{e.type}] [{e.category}] {e.message}");
                    if (!string.IsNullOrEmpty(e.stackTrace))
                        sb.AppendLine(e.stackTrace);
                    sb.AppendLine();
                }
                H8Utils.WriteFile(path, sb.ToString());

                if (!string.IsNullOrEmpty(console.editorLogTail))
                {
                    H8Utils.WriteFile(Path.Combine(outputPath, "editor_log_tail.txt"), console.editorLogTail);
                }
            }
            catch (Exception e) { Debug.LogWarning($"[H8Blackbox] Failed to write console logs: {e.Message}"); }
        }

        public static void WritePlayModeDiff(string outputPath, H8DiagnosticSnapshot bootSnap, H8DiagnosticSnapshot worldSnap, string prefix)
        {
            try
            {
                string path = Path.Combine(outputPath, $"playmode_time_diff_{prefix}.md");
                var sb = new StringBuilder();
                sb.AppendLine($"# PlayMode Time Diff ({prefix})");
                sb.AppendLine();
                sb.AppendLine($"**Initial Snapshot:** {bootSnap.timestamp}");
                sb.AppendLine($"**Final Snapshot:** {worldSnap.timestamp}");
                sb.AppendLine();
                H8Utils.WriteFile(path, sb.ToString());
            }
            catch (Exception e) { Debug.LogWarning($"[H8Blackbox] Failed to write diff: {e.Message}"); }
        }

        public static void WriteDirectVsBootstrapDiff(string outputPath, H8DiagnosticSnapshot directFinal, H8DiagnosticSnapshot bootstrapFinal)
        {
            try
            {
                string path = Path.Combine(outputPath, "direct_vs_bootstrap_diff.md");
                var sb = new StringBuilder();
                sb.AppendLine("# Direct vs Bootstrap Runtime Diff");
                sb.AppendLine();

                sb.AppendLine("## Executive Verdict");
                List<string> verdicts = new List<string>();
                
                if (bootstrapFinal.registry.registryPhase == 2 && directFinal.registry.registryPhase != 2)
                    verdicts.Add("**ENTRY_FLOW_CONFIRMED**: Bootstrap completes registry phase 2, Direct does not.");
                
                if (!bootstrapFinal.crest.oceanCrestActive && !directFinal.crest.oceanCrestActive)
                    verdicts.Add("**OCEAN_DISABLED_IN_BOTH**: Ocean_Crest is inactive in both direct and bootstrap.");
                else if (!directFinal.crest.oceanCrestActive && bootstrapFinal.crest.oceanCrestActive)
                    verdicts.Add("**OCEAN_FIXED_BY_BOOTSTRAP**: Ocean_Crest is active only when bootstrap runs.");
                    
                if (bootstrapFinal.mapMagic.activeTerrainCount == 0 && directFinal.mapMagic.activeTerrainCount == 0 && bootstrapFinal.mapMagic.graphAssigned && bootstrapFinal.mapMagic.mapMagicObjectActive)
                    verdicts.Add("**MAPMAGIC_FAILS_IN_BOTH**: MapMagic object and graph are active, but 0 terrains generated in both.");
                else if (directFinal.mapMagic.activeTerrainCount == 0 && bootstrapFinal.mapMagic.activeTerrainCount > 0)
                    verdicts.Add("**MAPMAGIC_FIXED_BY_BOOTSTRAP**: Terrains are generated only when bootstrap runs.");

                if (!directFinal.atmosphere.atmosphereManagerActive && bootstrapFinal.atmosphere.atmosphereManagerActive)
                    verdicts.Add("**ATMOSPHERE_FIXED_BY_BOOTSTRAP**: Atmosphere manager is active only when bootstrap runs.");

                if (verdicts.Count == 0)
                    verdicts.Add("**SHARED_SCENE_OR_ASSET_ISSUE**: Both modes fail similarly without obvious entry flow or bootstrap differentiation.");

                foreach (var v in verdicts) sb.AppendLine($"- {v}");
                sb.AppendLine();

                sb.AppendLine("## Key Differences Summary");
                sb.AppendLine("| Metric | Direct | Bootstrap |");
                sb.AppendLine("|---|---|---|");
                sb.AppendLine($"| Scene | {directFinal.project.activeScene} | {bootstrapFinal.project.activeScene} |");
                sb.AppendLine($"| Registry Phase | {directFinal.registry.registryPhaseName} | {bootstrapFinal.registry.registryPhaseName} |");
                
                int dMissing = 0, dNull = 0, dFilled = 0;
                foreach (var s in directFinal.registry.slots) { if (!s.memberFound) dMissing++; else if (s.isNull) dNull++; else dFilled++; }
                int bMissing = 0, bNull = 0, bFilled = 0;
                foreach (var s in bootstrapFinal.registry.slots) { if (!s.memberFound) bMissing++; else if (s.isNull) bNull++; else bFilled++; }
                
                sb.AppendLine($"| Registry Filled Slots | {dFilled} | {bFilled} |");
                sb.AppendLine($"| Registry Real Null Slots | {dNull} | {bNull} |");
                sb.AppendLine($"| Registry Missing Members | {dMissing} | {bMissing} |");
                sb.AppendLine($"| Active Terrains | {directFinal.mapMagic.activeTerrainCount} | {bootstrapFinal.mapMagic.activeTerrainCount} |");
                sb.AppendLine($"| Ocean Active | {directFinal.crest.oceanCrestActive} | {bootstrapFinal.crest.oceanCrestActive} |");
                sb.AppendLine($"| OceanKinematics Registered | {directFinal.crest.kinematicsRegistered} | {bootstrapFinal.crest.kinematicsRegistered} |");
                sb.AppendLine($"| Atmosphere Manager Active | {directFinal.atmosphere.atmosphereManagerActive} | {bootstrapFinal.atmosphere.atmosphereManagerActive} |");
                sb.AppendLine($"| Celestial Engine Active | {directFinal.atmosphere.celestialEngineActive} | {bootstrapFinal.atmosphere.celestialEngineActive} |");
                sb.AppendLine($"| Console Errors | {directFinal.console.totalErrors} | {bootstrapFinal.console.totalErrors} |");
                sb.AppendLine();

                sb.AppendLine("## Minimal Next Check");
                if (verdicts.Exists(v => v.Contains("ENTRY_FLOW_CONFIRMED")))
                    sb.AppendLine("- **ENTRY FLOW**: Inspect GameBootstrapper guard / PlayModeEntryGuard / start scene settings. Direct startup is blocked.");
                if (verdicts.Exists(v => v.Contains("OCEAN_DISABLED_IN_BOTH")))
                    sb.AppendLine("- **OCEAN CONFIG**: Inspect Ocean_Crest scene override or prefab activeSelf. It is turned off at the scene level.");
                if (verdicts.Exists(v => v.Contains("MAPMAGIC_FAILS_IN_BOTH")))
                    sb.AppendLine("- **MAPMAGIC RUNTIME**: Inspect MapMagic graph deserialization, generation camera assignment, or SystemDispatcher ticks.");
                if (verdicts.Exists(v => v.Contains("ATMOSPHERE_FIXED_BY_BOOTSTRAP")))
                    sb.AppendLine("- **ATMOSPHERE**: Depends heavily on bootstrap execution to instantiate or activate.");
                sb.AppendLine();

                sb.AppendLine("## Detailed Slot Differences");
                sb.AppendLine("| Slot | Direct Status | Bootstrap Status | Changed? | Member Found? |");
                sb.AppendLine("|---|---|---|---|---|");
                var dSlots = directFinal.registry.slots;
                var bSlots = bootstrapFinal.registry.slots;
                for (int i = 0; i < dSlots.Count; i++)
                {
                    var dSlot = dSlots[i];
                    var bSlot = bSlots.Find(x => x.slotName == dSlot.slotName) ?? new H8RegistrySlotInfo();
                    
                    string getStatus(H8RegistrySlotInfo s)
                    {
                        if (!s.memberFound) return "MISSING_MEMBER";
                        if (s.isNull) return "NULL";
                        return $"FILLED({s.typeName})";
                    }

                    string ds = getStatus(dSlot);
                    string bs = getStatus(bSlot);
                    string changed = (ds != bs) ? "YES" : "NO";
                    string mf = dSlot.memberFound ? "YES" : "NO";

                    sb.AppendLine($"| {dSlot.slotName} | {ds} | {bs} | {changed} | {mf} |");
                }

                H8Utils.WriteFile(path, sb.ToString());
            }
            catch (Exception e) { Debug.LogWarning($"[H8Blackbox] Failed to write direct vs bootstrap diff: {e.Message}"); }
        }

        public static void WriteFullComparisonHandoff(string outputPath, H8DiagnosticSnapshot directFinal, H8DiagnosticSnapshot bootstrapFinal, List<H8Finding> dFindings, List<H8Finding> bFindings)
        {
            try
            {
                string path = Path.Combine(outputPath, "compact_handoff_for_claude.md");
                var sb = new StringBuilder();
                sb.AppendLine("# Hecton8 Blackbox Full Comparison Handoff");
                sb.AppendLine("⚠️ **CRITICAL AI INSTRUCTIONS** ⚠️");
                sb.AppendLine("- Do not give beginner Unity advice.");
                sb.AppendLine("- Use only measured evidence.");
                sb.AppendLine("- Do not suggest creating terrain/water/sky manually.");
                sb.AppendLine("- First identify whether entry flow is root cause.");
                sb.AppendLine("- Then identify independent ocean/mapmagic/atmosphere failures.");
                sb.AppendLine();
                
                void AddFacts(string label, H8DiagnosticSnapshot s)
                {
                    sb.AppendLine($"## {label} Run Final Facts");
                    sb.AppendLine($"- Scene: `{s.project.activeScene}`");
                    sb.AppendLine($"- Registry Phase: `{s.registry.registryPhaseName}`");
                    var nulls = new List<string>();
                    int missingCount = 0;
                    foreach (var sl in s.registry.slots)
                    {
                        if (!sl.memberFound) missingCount++;
                        else if (sl.isNull) nulls.Add(sl.slotName);
                    }
                    sb.AppendLine($"- Null Slots: `{(nulls.Count > 0 ? string.Join(",", nulls) : "None")}`");
                    sb.AppendLine($"- Missing Members: `{missingCount}`");
                    sb.AppendLine($"- MapMagic Terrains: `{s.mapMagic.activeTerrainCount}`");
                    sb.AppendLine($"- MapMagic Graph: `{s.mapMagic.graphAssigned}`");
                    sb.AppendLine($"- Ocean_Crest Active: `{s.crest.oceanCrestActive}`");
                    sb.AppendLine($"- OceanRenderer Enabled: `{s.crest.oceanRendererEnabled}`");
                    sb.AppendLine($"- OceanKinematics Registered: `{s.crest.kinematicsRegistered}`");
                    sb.AppendLine($"- Atmosphere Active: `{s.atmosphere.atmosphereManagerActive}`");
                    sb.AppendLine($"- Console Errors: `{s.console.totalErrors}`");
                    sb.AppendLine();
                }

                AddFacts("Direct", directFinal);
                AddFacts("Bootstrap", bootstrapFinal);

                sb.AppendLine("## Critical Findings");
                sb.AppendLine("**Direct:**");
                foreach (var f in dFindings) if (f.severity == "Critical" || f.severity == "Error") sb.AppendLine($"- [{f.id}] {f.title}: {f.measuredValue}");
                sb.AppendLine("**Bootstrap:**");
                foreach (var f in bFindings) if (f.severity == "Critical" || f.severity == "Error") sb.AppendLine($"- [{f.id}] {f.title}: {f.measuredValue}");
                sb.AppendLine();

                sb.AppendLine("## Exact Questions For Claude");
                sb.AppendLine("1. Is direct scene startup invalid (bypassing bootstrap)?");
                sb.AppendLine("2. Does bootstrap fix registry/services?");
                sb.AppendLine("3. Is Ocean_Crest disabled independently of bootstrap?");
                sb.AppendLine("4. Is MapMagic failing even under bootstrap?");
                sb.AppendLine("5. What is the minimal safe fix order?");
                sb.AppendLine("6. What should not be touched?");
                sb.AppendLine();
                
                sb.AppendLine("## Fix Order Recommendation (Diagnostic Suggestion Only)");
                sb.AppendLine("1. If entry flow confirmed, fix/restore playmode entry guard first.");
                sb.AppendLine("2. Re-run full comparison.");
                sb.AppendLine("3. If ocean remains inactive in both, inspect Ocean_Crest active override.");
                sb.AppendLine("4. Re-run full comparison.");
                sb.AppendLine("5. If MapMagic still no terrain, inspect graph/dispatcher/generation camera.");
                sb.AppendLine("6. Re-run full comparison.");
                sb.AppendLine("7. If atmosphere missing only direct, it is entry-flow issue; if missing both, inspect atmosphere instantiation.");

                H8Utils.WriteFile(path, sb.ToString());
            }
            catch (Exception e) { Debug.LogWarning($"[H8Blackbox] Failed to write full comparison handoff: {e.Message}"); }
        }
    }
}
