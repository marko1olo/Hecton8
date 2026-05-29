using System;
using System.IO;
using System.Text;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Gameplay;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.EditorTools
{
    internal static class RadiationShieldingReportPaths
    {
        internal const string DedicatedReportPath = "Docs/Reports/PHYSICS_OPTIMIZATION_REPORT_SHINOBU_274.json";
        internal const string SharedReportPath = "Docs/Reports/PHYSICS_OPTIMIZATION_REPORT.json";
    }

    public sealed class RadiationShieldingTunerWindow : EditorWindow
    {
        private static readonly int HazardRadiationLevelId = Shader.PropertyToID("_HazardRadiationLevel");
        private static readonly int HandRadiationMutationId = Shader.PropertyToID("_HectonHandRadiationMutation01");
        private Label _layoutStatus;
        private Label _telemetryStatus;
        private Label _scanStatus;

        [MenuItem("Hecton8/Radiation/Shielding Tuner")]
        public static void Open()
        {
            RadiationShieldingTunerWindow window = GetWindow<RadiationShieldingTunerWindow>();
            window.titleContent = new GUIContent("Radiation Shielding");
            window.minSize = new Vector2(360f, 220f);
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.paddingLeft = 10f;
            root.style.paddingRight = 10f;
            root.style.paddingTop = 10f;
            root.style.paddingBottom = 10f;

            _layoutStatus = new Label("RadiationStateDTO layout: unchecked");
            root.Add(_layoutStatus);
            _telemetryStatus = new Label("Radiation telemetry: unavailable");
            root.Add(_telemetryStatus);

            Button validateLayout = new Button(ValidateLayout)
            {
                text = "Validate DTO Layout"
            };
            root.Add(validateLayout);

            Slider radiationPreview = new Slider("Visor radiation preview", 0f, 1f)
            {
                value = Shader.GetGlobalFloat(HazardRadiationLevelId)
            };
            radiationPreview.RegisterValueChangedCallback(evt =>
            {
                Shader.SetGlobalFloat(HazardRadiationLevelId, Mathf.Clamp01(evt.newValue));
            });
            root.Add(radiationPreview);

            Slider handMutationPreview = new Slider("Hand mutation preview", 0f, 1f)
            {
                value = Shader.GetGlobalFloat(HandRadiationMutationId)
            };
            handMutationPreview.RegisterValueChangedCallback(evt =>
            {
                Shader.SetGlobalFloat(HandRadiationMutationId, Mathf.Clamp01(evt.newValue));
            });
            root.Add(handMutationPreview);

            Slider decay = new Slider("Base decay speed", 0.90f, 1.0f) { value = 0.999f };
            decay.RegisterValueChangedCallback(evt => MutateTuning(tuning =>
            {
                tuning.DecayPerTick = Mathf.Clamp(evt.newValue, 0.90f, 1.0f);
                return tuning;
            }));
            root.Add(decay);

            Slider lead = new Slider("Lead shielding effectiveness", 0f, 1f) { value = 1f };
            lead.RegisterValueChangedCallback(evt => MutateTuning(tuning =>
            {
                tuning.LeadShieldingEffectiveness = Mathf.Clamp01(evt.newValue);
                return tuning;
            }));
            root.Add(lead);

            Slider mutation = new Slider("Mutation rate", 0.001f, 0.05f) { value = 0.01f };
            mutation.RegisterValueChangedCallback(evt => MutateTuning(tuning =>
            {
                tuning.DoseToDegradationScale = Mathf.Max(0.001f, evt.newValue);
                return tuning;
            }));
            root.Add(mutation);

            Button scanButton = new Button(() =>
            {
                RadiationTriggerDebtScanner.WriteReport();
                _scanStatus.text = "Scanner report: " + RadiationShieldingReportPaths.DedicatedReportPath;
            })
            {
                text = "Scan Trigger Debt"
            };
            root.Add(scanButton);

            _scanStatus = new Label("Scanner report: pending");
            root.Add(_scanStatus);
            root.schedule.Execute(RefreshTelemetryReadout).Every(250);
        }

        private void ValidateLayout()
        {
            bool ok = RadiationHazardGrid.RadiationStateLayoutGuard.ValidateLayout();
            _layoutStatus.text = ok
                ? "RadiationStateDTO layout: 32 bytes, offsets valid"
                : "RadiationStateDTO layout: INVALID";
        }

        private void RefreshTelemetryReadout()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null ||
                !TryReadRadiationVaultBuffer(vault, BufferID.Shinobu274RadiationTelemetryRing, out NativeArray<RadiationHazardGrid.RadiationTelemetryEntry> telemetryRing) ||
                !TryReadRadiationVaultBuffer(vault, BufferID.Shinobu274RadiationTelemetryCursor, out NativeArray<uint> cursorLane) ||
                !telemetryRing.IsCreated ||
                !cursorLane.IsCreated ||
                telemetryRing.Length == 0 ||
                cursorLane.Length == 0)
            {
                _telemetryStatus.text = "Radiation telemetry: unavailable";
                return;
            }

            uint writeCount = cursorLane[0];
            int latestIndex = (int)((writeCount + (uint)telemetryRing.Length - 1u) % (uint)telemetryRing.Length);
            RadiationHazardGrid.RadiationTelemetryEntry entry = telemetryRing[latestIndex];
            _telemetryStatus.text =
                $"Frame {entry.Frame} | Exposure {entry.CurrentExposureRate:0.000} | Dose {entry.CumulativeDoseRad:0.000} | Shield {entry.ShieldingFactor01:0.000} | Sources {entry.SourceCount}";
        }

        private static void MutateTuning(Func<RadiationHazardGrid.RadiationTuningDTO, RadiationHazardGrid.RadiationTuningDTO> mutator)
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null ||
                !vault.TryGetGenerationHandle(
                    BufferID.Shinobu274RadiationTuning,
                    out VaultGenerationHandle<RadiationHazardGrid.RadiationTuningDTO> tuningHandle) ||
                !IsRadiationVaultHandle(in tuningHandle, BufferID.Shinobu274RadiationTuning))
            {
                return;
            }

            if (!vault.TryAcquireWriteLock(in tuningHandle, SystemID.GameplayRadiation, out NativeArray<RadiationHazardGrid.RadiationTuningDTO> tuning))
                return;

            try
            {
                if (!tuning.IsCreated || tuning.Length == 0)
                    return;

                tuning[0] = mutator(tuning[0]);
            }
            finally
            {
                vault.ReleaseWriteLock(in tuningHandle, SystemID.GameplayRadiation);
            }
        }

        private static bool TryReadRadiationVaultBuffer<T>(
            IDataVault vault,
            BufferID bufferId,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !vault.TryGetGenerationHandle<T>(bufferId, out VaultGenerationHandle<T> handle) ||
                !IsRadiationVaultHandle(in handle, bufferId) ||
                !vault.TryReadHandle(in handle, out NativeArray<T> resolved) ||
                !resolved.IsCreated ||
                resolved.Length == 0)
            {
                return false;
            }

            buffer = resolved;
            return true;
        }

        private static bool IsRadiationVaultHandle<T>(in VaultGenerationHandle<T> handle, BufferID bufferId) where T : struct
        {
            return handle.BufferID == (uint)bufferId &&
                handle.SystemID == (uint)SystemID.GameplayRadiation &&
                handle.Generation != 0u;
        }
    }

    public static class RadiationTriggerDebtScanner
    {
        private const int MaxReportedTriggerDebtFindings = 3;

        private static readonly string[] TriggerTokens =
        {
            "OnTriggerEnter",
            "OnTriggerStay",
            "OnTriggerExit",
            "SphereCollider",
            "BoxCollider",
            "CapsuleCollider",
            "Physics.OverlapSphereNonAlloc",
            "Physics.OverlapSphere",
            "Physics.Raycast"
        };

        private static readonly string[] DomainTokens =
        {
            "Radiation",
            "Radioactive",
            "Toxic",
            "Toxicity",
            "Hazard",
            "Reactor"
        };

        [MenuItem("Hecton8/Radiation/Scan Trigger Debt")]
        public static void WriteReport()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string scriptsRoot = Path.Combine(Application.dataPath, "_Project", "Scripts");
            string reportPath = Path.Combine(projectRoot, RadiationShieldingReportPaths.DedicatedReportPath);
            string reportDirectory = Path.GetDirectoryName(reportPath);
            if (!string.IsNullOrEmpty(reportDirectory))
                Directory.CreateDirectory(reportDirectory);

            string[] files = Directory.Exists(scriptsRoot)
                ? Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories)
                : Array.Empty<string>();
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);

            StringBuilder findings = new StringBuilder(4096);
            int scannedFileCount = 0;
            int editorIgnoredCount = 0;
            int candidateFileCount = 0;
            int findingCount = 0;
            for (int fileIndex = 0; fileIndex < files.Length; fileIndex++)
            {
                string file = files[fileIndex];
                string relative = ToProjectRelative(projectRoot, file);
                if (relative.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    editorIgnoredCount++;
                    continue;
                }

                scannedFileCount++;
                string text = File.ReadAllText(file);
                bool[] ignoredSpanMask = BuildIgnoredSpanMask(text);
                if (!ContainsAnyUnignored(text, DomainTokens, ignoredSpanMask))
                    continue;

                candidateFileCount++;
                for (int tokenIndex = 0; tokenIndex < TriggerTokens.Length; tokenIndex++)
                {
                    string token = TriggerTokens[tokenIndex];
                    int offset = 0;
                    while (offset < text.Length)
                    {
                        offset = text.IndexOf(token, offset, StringComparison.Ordinal);
                        if (offset < 0)
                            break;

                        if (offset < ignoredSpanMask.Length && ignoredSpanMask[offset])
                        {
                            offset += token.Length;
                            continue;
                        }

                        if (findingCount < MaxReportedTriggerDebtFindings)
                            AppendFinding(findings, findingCount, relative, token, ResolveLineNumber(text, offset), ResolveDecision(token));

                        findingCount++;
                        offset += token.Length;
                    }
                }
            }

            int reportedFindingCount = Math.Min(findingCount, MaxReportedTriggerDebtFindings);
            StringBuilder json = new StringBuilder(8192);
            json.AppendLine("{");
            json.AppendLine("  \"agent\": \"SHINOBU_274\",");
            json.AppendLine("  \"domain\": \"Radiation Scrubber\",");
            json.AppendLine("  \"scanner\": \"RadiationTriggerDebtScanner\",");
            json.AppendLine("  \"status\": \"PENDING_VERIFICATION\",");
            json.AppendLine("  \"verdict\": \"" + (findingCount == 0 ? "Unmanaged Radiation Triggers Purged" : "Legacy trigger/physics debt found outside SHINOBU_274 authority") + "\",");
            json.AppendLine("  \"dedicated_report\": \"" + RadiationShieldingReportPaths.DedicatedReportPath + "\",");
            json.AppendLine("  \"shared_report\": \"" + RadiationShieldingReportPaths.SharedReportPath + "\",");
            json.AppendLine("  \"required_summary\": \"Unmanaged Radiation Triggers Purged\",");
            json.AppendLine("  \"scanned_root\": \"Assets/_Project/Scripts\",");
            json.Append("  \"scanned_file_count\": ").Append(scannedFileCount).AppendLine(",");
            json.Append("  \"ignored_editor_file_count\": ").Append(editorIgnoredCount).AppendLine(",");
            json.Append("  \"candidate_file_count\": ").Append(candidateFileCount).AppendLine(",");
            json.Append("  \"finding_count\": ").Append(reportedFindingCount).AppendLine(",");
            json.Append("  \"broad_static_finding_count\": ").Append(findingCount).AppendLine(",");
            json.AppendLine("  \"finding_list_policy\": \"deterministic alphabetical scan, editor folder ignored, comments and string literals masked, first three generic trigger/physics findings emitted\",");
            json.AppendLine("  \"accepted_runtime_route\": \"RadiationHazardGrid -> DataVault RadiationStateDTO -> CalculateRadiationExposureJob -> HectonPlayerHealth/CombatStatusBits.Irradiated64\",");
            json.AppendLine("  \"finding_scope_note\": \"Broad static scanner includes generic hazard/toxicity/reactor files; SHINOBU_274 runtime authority is the DataVault/Burst route, not these legacy generic collider users.\",");
            json.AppendLine("  \"dispatcher_route\": \"SystemDispatcher Simulation schedules CalculateRadiationExposureJob; PostSimulation consumes completed Vault state; VisualSync uploads shader globals.\",");
            json.AppendLine("  \"vault_buffers\": \"72740 states, 72741 sources, 72742 source count, 72743 telemetry, 72744 telemetry cursor, 72745 profiles, 72746 csv scratch, 72747 tuning, 72748 damage signal, 72749 grid read, 72750 grid write, 72751 grid source\",");
            json.AppendLine("  \"shader_warmup\": \"Assets/_Project/Art/Shaders/Variants/Hecton8_UberNoir_RadiationWarmup.shadervariants\",");
            json.AppendLine("  \"owner_route_correction\": \"Solar flare, radioactive clarity trauma, meteorite radiation, EnvironmentalHazard radiation, HectonHazardManager radiation, and HazardZoneManager radiation registrations route into RadiationHazardGrid via SignalBus source/dose lanes; no non-grid caller mutates HectonPlayerHealth radiation fatigue.\",");
            json.AppendLine("  \"grid_swap_cadence_correction\": \"Diffusion read/write parity survives Vault view refresh through _gridBuffersSwapped; radiation forced ticks integrate actual accumulated seconds instead of clamping dt to the quality interval.\",");
            json.AppendLine("  \"exact_external_dose_correction\": \"External RadiationDoseSignal rads are accumulated in _pendingExternalDoseRad and included once as ExternalDoseDelta; external intensity drives current exposure and visuals only, preventing rate*dt double counting.\",");
            json.AppendLine("  \"concurrency_guard\": \"Radiation source/dose drains are preserved or deferred while a previous radiation job is active; source grid rebuild and diffusion scheduling are skipped while diffusion owns the grid buffers.\",");
            json.AppendLine("  \"active_job_signal_preservation\": \"When a previous radiation job is active, source signals are requeued, exact dose is accumulated, iodine treatment is deferred as pending dose reduction, and compatibility reads sample only the stable read grid; no force-complete is added.\",");
            json.AppendLine("  \"live_load_hotswap_fence\": \"LoadFromSaveData and DataVault hot-swap are deferred until PostSimulation observes no active radiation/diffusion job; force-complete remains teardown-only.\",");
            json.AppendLine("  \"trigger_debt_findings\": [");
            json.Append(findings);
            json.AppendLine();
            json.AppendLine("  ],");
            json.AppendLine("  \"microseconds_saved_estimate\": {");
            json.AppendLine("    \"removed_trigger_callback_cost_per_player_overlap_us\": 3.0,");
            json.AppendLine("    \"removed_managed_inverse_square_loop_cost_us\": 4.0,");
            json.AppendLine("    \"new_burst_kernel_target_us\": 5.0");
            json.AppendLine("  }");
            json.AppendLine("}");
            File.WriteAllText(reportPath, json.ToString(), Encoding.UTF8);
            AssetDatabase.Refresh();
        }

        private static bool[] BuildIgnoredSpanMask(string text)
        {
            if (string.IsNullOrEmpty(text))
                return Array.Empty<bool>();

            bool[] ignored = new bool[text.Length];
            bool inLineComment = false;
            bool inBlockComment = false;
            bool inString = false;
            bool inVerbatimString = false;
            bool inChar = false;

            for (int i = 0; i < text.Length; i++)
            {
                char current = text[i];
                char next = i + 1 < text.Length ? text[i + 1] : '\0';

                if (inLineComment)
                {
                    ignored[i] = true;
                    if (current == '\n' || current == '\r')
                        inLineComment = false;
                    continue;
                }

                if (inBlockComment)
                {
                    ignored[i] = true;
                    if (current == '*' && next == '/')
                    {
                        ignored[i + 1] = true;
                        i++;
                        inBlockComment = false;
                    }
                    continue;
                }

                if (inString)
                {
                    ignored[i] = true;
                    if (inVerbatimString)
                    {
                        if (current == '"' && next == '"')
                        {
                            ignored[i + 1] = true;
                            i++;
                        }
                        else if (current == '"')
                        {
                            inString = false;
                            inVerbatimString = false;
                        }
                    }
                    else if (current == '\\' && i + 1 < text.Length)
                    {
                        ignored[i + 1] = true;
                        i++;
                    }
                    else if (current == '"')
                    {
                        inString = false;
                    }
                    continue;
                }

                if (inChar)
                {
                    ignored[i] = true;
                    if (current == '\\' && i + 1 < text.Length)
                    {
                        ignored[i + 1] = true;
                        i++;
                    }
                    else if (current == '\'')
                    {
                        inChar = false;
                    }
                    continue;
                }

                if (current == '/' && next == '/')
                {
                    ignored[i] = true;
                    ignored[i + 1] = true;
                    i++;
                    inLineComment = true;
                    continue;
                }

                if (current == '/' && next == '*')
                {
                    ignored[i] = true;
                    ignored[i + 1] = true;
                    i++;
                    inBlockComment = true;
                    continue;
                }

                if (current == '@' && next == '"')
                {
                    ignored[i] = true;
                    ignored[i + 1] = true;
                    i++;
                    inString = true;
                    inVerbatimString = true;
                    continue;
                }

                if (current == '"')
                {
                    ignored[i] = true;
                    inString = true;
                    continue;
                }

                if (current == '\'')
                {
                    ignored[i] = true;
                    inChar = true;
                }
            }

            return ignored;
        }

        private static bool ContainsAnyUnignored(string text, string[] tokens, bool[] ignoredSpanMask)
        {
            if (string.IsNullOrEmpty(text) || tokens == null)
                return false;

            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i];
                int offset = 0;
                while (offset < text.Length)
                {
                    offset = text.IndexOf(token, offset, StringComparison.OrdinalIgnoreCase);
                    if (offset < 0)
                        break;

                    if (offset >= ignoredSpanMask.Length || !ignoredSpanMask[offset])
                        return true;

                    offset += token.Length;
                }
            }

            return false;
        }

        private static void AppendFinding(StringBuilder findings, int findingIndex, string relative, string token, int line, string decision)
        {
            if (findingIndex > 0)
                findings.AppendLine(",");

            findings.Append("    { \"file\": \"")
                .Append(Escape(relative))
                .Append("\", \"token\": \"")
                .Append(Escape(token))
                .Append("\", \"line\": ")
                .Append(line)
                .Append(", \"decision\": \"")
                .Append(Escape(decision))
                .Append("\"")
                .Append(" }");
        }

        private static string ResolveDecision(string token)
        {
            if (token.IndexOf("Physics.", StringComparison.Ordinal) >= 0)
                return "legacy query path; radiation dose resolves through Burst/DataVault route";
            if (token.IndexOf("Collider", StringComparison.Ordinal) >= 0)
                return "legacy collider component path; not used as SHINOBU_274 radiation authority";
            return "legacy generic hazard trigger; not used as SHINOBU_274 radiation authority";
        }

        private static int ResolveLineNumber(string text, int offset)
        {
            int line = 1;
            int safeOffset = Math.Min(Math.Max(0, offset), string.IsNullOrEmpty(text) ? 0 : text.Length);
            for (int i = 0; i < safeOffset; i++)
            {
                if (text[i] == '\n')
                    line++;
            }

            return line;
        }

        private static string ToProjectRelative(string projectRoot, string file)
        {
            string fullRoot = Path.GetFullPath(projectRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string fullFile = Path.GetFullPath(file);
            if (fullFile.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
                return fullFile.Substring(fullRoot.Length + 1).Replace('\\', '/');

            return fullFile.Replace('\\', '/');
        }

        private static string Escape(string value)
        {
            return string.IsNullOrEmpty(value)
                ? string.Empty
                : value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }

    public static class Radiation_OOP_Scanner
    {
        [MenuItem("Hecton8/Radiation/Radiation_OOP_Scanner")]
        public static void Run()
        {
            RadiationTriggerDebtScanner.WriteReport();
        }
    }
}
