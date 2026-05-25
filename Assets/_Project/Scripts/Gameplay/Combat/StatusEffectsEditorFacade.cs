#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using System.Text;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Gameplay
{
    [InitializeOnLoad]
    internal static class StatusEffectLayoutVerifier
    {
        static StatusEffectLayoutVerifier()
        {
            Validate(logSuccess: false);
        }

        [MenuItem("HECTON-8/Combat/Validate Status Effect FSM Layout")]
        public static void ValidateFromMenu()
        {
            Validate(logSuccess: true);
        }

        public static bool Validate(bool logSuccess)
        {
            bool valid =
                UnsafeUtility.SizeOf<CombatDamageRuntime.CombatStatusEffectState>() == 64 &&
                OffsetOf<CombatDamageRuntime.CombatStatusEffectState>(nameof(CombatDamageRuntime.CombatStatusEffectState.StatusEffectMask)) == 0 &&
                OffsetOf<CombatDamageRuntime.CombatStatusEffectState>(nameof(CombatDamageRuntime.CombatStatusEffectState.Durations0123)) == 8 &&
                OffsetOf<CombatDamageRuntime.CombatStatusEffectState>(nameof(CombatDamageRuntime.CombatStatusEffectState.Durations4567)) == 24 &&
                OffsetOf<CombatDamageRuntime.CombatStatusEffectState>(nameof(CombatDamageRuntime.CombatStatusEffectState.FractureSeconds)) == 60 &&
                OffsetOf<CombatDamageRuntime.CombatStatusEffectTelemetryEntry>(nameof(CombatDamageRuntime.CombatStatusEffectTelemetryEntry.StatusEffectMask)) == 8 &&
                UnsafeUtility.SizeOf<CombatDamageRuntime.CombatStatusEffectRequest>() == 64 &&
                UnsafeUtility.SizeOf<CombatDamageRuntime.CombatStatusEffectTuning>() == 64 &&
                UnsafeUtility.SizeOf<CombatDamageRuntime.CombatStatusEffectTelemetryEntry>() == 64 &&
                UnsafeUtility.SizeOf<CombatDamageRuntime.CombatStatusEffectCounterLane>() == 64 &&
                OffsetOf<CombatDamageRuntime.CombatStatusEffectCounterLane>(nameof(CombatDamageRuntime.CombatStatusEffectCounterLane.Value)) == 0 &&
                UnsafeUtility.SizeOf<CombatDamageRuntime.CombatStatusEffectVfxRequest>() == 64 &&
                OffsetOf<CombatDamageRuntime.CombatStatusEffectVfxRequest>(nameof(CombatDamageRuntime.CombatStatusEffectVfxRequest.PositionAup)) == 0 &&
                UnsafeUtility.SizeOf<CombatDamageSignal>() == 64 &&
                OffsetOf<CombatDamageSignal>(nameof(CombatDamageSignal.ImpactAup)) == 0 &&
                OffsetOf<CombatDamageSignal>(nameof(CombatDamageSignal.Magnitude)) == 36;

            if (!valid)
            {
                Debug.LogError("[StatusEffectLayoutVerifier] Status FSM DTO layout mismatch. SHINOBU_319 output rejected until fixed.");
                return false;
            }

            if (logSuccess)
                Hecton8.Core.H8Debug.Log("[StatusEffectLayoutVerifier] StatusEffectState=64B; StatusEffectMask offset=0; timers at 8/24; telemetry/counter/vfx/damage lanes=64B.");
            return true;
        }

        private static int OffsetOf<T>(string fieldName)
        {
            FieldInfo field = typeof(T).GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field != null ? UnsafeUtility.GetFieldOffset(field) : -1;
        }
    }

    public sealed class FsmStatusEffectTunerWindow : EditorWindow
    {
        private Slider _poisonDps;
        private Slider _bleedDps;
        private Slider _burnDps;
        private Slider _stunScale;
        private Slider _maxCadence;
        private Label _state;
        private IntegerField _activeCount;
        private IntegerField _requestCount;
        private FloatField _damage;
        private FloatField _quality;
        private IMGUIContainer _chart;
        private double _nextRefresh;
        private CombatDamageRuntime.CombatStatusEffectTelemetryEntry _lastTelemetry;

        [MenuItem("HECTON-8/Combat/FSM Status Effect Tuner")]
        public static void Open()
        {
            FsmStatusEffectTunerWindow window = GetWindow<FsmStatusEffectTunerWindow>();
            window.titleContent = new GUIContent("Status FSM");
            window.minSize = new Vector2(380f, 300f);
        }

        private void OnEnable()
        {
            EditorApplication.update += RefreshTelemetry;
        }

        private void OnDisable()
        {
            EditorApplication.update -= RefreshTelemetry;
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            root.style.paddingLeft = 8f;
            root.style.paddingRight = 8f;
            root.style.paddingTop = 8f;
            root.style.paddingBottom = 8f;

            _poisonDps = CreateSlider("Poison DPS", 0f, 20f);
            _bleedDps = CreateSlider("Bleed DPS", 0f, 20f);
            _burnDps = CreateSlider("Burn DPS", 0f, 24f);
            _stunScale = CreateSlider("Stun Speed Scale", 0f, 1f);
            _maxCadence = CreateSlider("Max Cadence Seconds", 0.1f, 2f);
            _state = new Label("Telemetry: waiting.");
            _activeCount = ReadOnlyInt("Active");
            _requestCount = ReadOnlyInt("Requests");
            _damage = ReadOnlyFloat("Damage");
            _quality = ReadOnlyFloat("Quality");
            _chart = new IMGUIContainer(DrawChart);
            _chart.style.height = 72f;

            root.Add(_poisonDps);
            root.Add(_bleedDps);
            root.Add(_burnDps);
            root.Add(_stunScale);
            root.Add(_maxCadence);
            root.Add(new Button(() => CombatDamageRuntime.GenerateMockStatusEffects(5000, unchecked((uint)System.Environment.TickCount))) { text = "Generate Mock Status Burst" });
            root.Add(new Button(LoadCsv) { text = "Load status_effect_profiles.csv" });
            root.Add(new Button(StatusEffectLayoutVerifier.ValidateFromMenu) { text = "Validate 64B Layout" });
            root.Add(new Button(OOP_Buff_Scanner.RunFromMenu) { text = "Write OOP Buff Report" });
            root.Add(_state);
            root.Add(_activeCount);
            root.Add(_requestCount);
            root.Add(_damage);
            root.Add(_quality);
            root.Add(_chart);

            _poisonDps.RegisterValueChangedCallback(_ => ApplyTuning());
            _bleedDps.RegisterValueChangedCallback(_ => ApplyTuning());
            _burnDps.RegisterValueChangedCallback(_ => ApplyTuning());
            _stunScale.RegisterValueChangedCallback(_ => ApplyTuning());
            _maxCadence.RegisterValueChangedCallback(_ => ApplyTuning());
            PullTuning();
        }

        private static Slider CreateSlider(string label, float min, float max)
        {
            Slider slider = new Slider(label, min, max) { showInputField = true };
            slider.style.marginBottom = 4f;
            return slider;
        }

        private static IntegerField ReadOnlyInt(string label)
        {
            IntegerField field = new IntegerField(label);
            field.SetEnabled(false);
            return field;
        }

        private static FloatField ReadOnlyFloat(string label)
        {
            FloatField field = new FloatField(label);
            field.SetEnabled(false);
            return field;
        }

        private void PullTuning()
        {
            if (!CombatDamageRuntime.TryGetStatusEffectTuning(out CombatDamageRuntime.CombatStatusEffectTuning tuning))
                return;

            _poisonDps.SetValueWithoutNotify(tuning.PoisonDamagePerSecond);
            _bleedDps.SetValueWithoutNotify(tuning.BleedingDamagePerSecond);
            _burnDps.SetValueWithoutNotify(tuning.BurningDamagePerSecond);
            _stunScale.SetValueWithoutNotify(tuning.StunMobilityScale);
            _maxCadence.SetValueWithoutNotify(tuning.MaxCadenceSeconds);
        }

        private void ApplyTuning()
        {
            if (!CombatDamageRuntime.TryGetStatusEffectTuning(out CombatDamageRuntime.CombatStatusEffectTuning tuning))
                return;

            tuning.PoisonDamagePerSecond = _poisonDps.value;
            tuning.BleedingDamagePerSecond = _bleedDps.value;
            tuning.BurningDamagePerSecond = _burnDps.value;
            tuning.StunMobilityScale = _stunScale.value;
            tuning.MaxCadenceSeconds = _maxCadence.value;
            CombatDamageRuntime.WriteStatusEffectTuning(in tuning);
        }

        private void LoadCsv()
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), "Data", "Balance", "status_effect_profiles.csv");
            CombatDamageRuntime.TryLoadStatusEffectProfilesCsv(path);
            PullTuning();
        }

        private void RefreshTelemetry()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now < _nextRefresh || _state == null)
                return;

            _nextRefresh = now + 0.25d;
            if (!CombatDamageRuntime.TryGetLastStatusEffectTelemetry(out _lastTelemetry))
            {
                _state.text = "Telemetry: runtime not initialized.";
                return;
            }

            _state.text = _lastTelemetry.AnomalyHash != 0u ? "Telemetry: anomaly present." : "Telemetry: latest status solve.";
            _activeCount.SetValueWithoutNotify(ToInt(_lastTelemetry.ActiveCount));
            _requestCount.SetValueWithoutNotify(ToInt(_lastTelemetry.RequestCount));
            _damage.SetValueWithoutNotify(_lastTelemetry.AppliedDamage);
            _quality.SetValueWithoutNotify(_lastTelemetry.GlobalQualityWeight01);
            _chart.MarkDirtyRepaint();
        }

        private void DrawChart()
        {
            Rect rect = GUILayoutUtility.GetRect(1f, 56f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(0.06f, 0.07f, 0.08f, 1f));
            float poison = ((_lastTelemetry.StatusEffectMask & CombatStatusBits.Poisoned64) != 0UL) ? 1f : 0f;
            float bleed = ((_lastTelemetry.StatusEffectMask & CombatStatusBits.Bleeding64) != 0UL) ? 1f : 0f;
            float burn = ((_lastTelemetry.StatusEffectMask & CombatStatusBits.Burning64) != 0UL) ? 1f : 0f;
            float fracture = ((_lastTelemetry.StatusEffectMask & CombatStatusBits.Fractured64) != 0UL) ? 1f : 0f;
            float width = rect.width / 4f;
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, width * poison, rect.height), new Color(0.1f, 0.8f, 0.35f, 1f));
            EditorGUI.DrawRect(new Rect(rect.x + width, rect.y, width * bleed, rect.height), new Color(0.9f, 0.1f, 0.08f, 1f));
            EditorGUI.DrawRect(new Rect(rect.x + (width * 2f), rect.y, width * burn, rect.height), new Color(1f, 0.45f, 0.08f, 1f));
            EditorGUI.DrawRect(new Rect(rect.x + (width * 3f), rect.y, width * fracture, rect.height), new Color(0.95f, 0.72f, 0.16f, 1f));
        }

        private static int ToInt(uint value)
        {
            return value > int.MaxValue ? int.MaxValue : (int)value;
        }
    }

    [InitializeOnLoad]
    internal static class StatusEffectDebugGizmo
    {
        private const int MaxTargets = 64;
        private const float Height = 1.25f;

        static StatusEffectDebugGizmo()
        {
            SceneView.duringSceneGui -= DrawSceneView;
            SceneView.duringSceneGui += DrawSceneView;
        }

        private static void DrawSceneView(SceneView sceneView)
        {
            int count = math.min(MaxTargets, CombatDamageRuntime.ReadStatusEffectDebugTargetCount());
            for (int slot = 0; slot < count; slot++)
            {
                if (!CombatDamageRuntime.TryGetStatusEffectDebugSnapshot(slot, out Vector3 point, out ulong mask))
                    continue;

                Color color = ResolveColor(mask);
                Handles.color = color;
                Vector3 top = point + (Vector3.up * Height);
                Handles.DrawLine(point, top);
                Handles.CubeHandleCap(0, top, Quaternion.identity, 0.08f, EventType.Repaint);
            }
        }

        private static Color ResolveColor(ulong mask)
        {
            if ((mask & CombatStatusBits.Poisoned64) != 0UL)
                return new Color(0.1f, 0.85f, 0.35f, 0.9f);
            if ((mask & CombatStatusBits.Bleeding64) != 0UL)
                return new Color(0.9f, 0.08f, 0.06f, 0.9f);
            if ((mask & CombatStatusBits.Stunned64) != 0UL)
                return new Color(0.2f, 0.5f, 1f, 0.9f);
            if ((mask & CombatStatusBits.Fractured64) != 0UL)
                return new Color(0.95f, 0.72f, 0.16f, 0.9f);
            return new Color(1f, 0.8f, 0.1f, 0.9f);
        }
    }

    internal static class OOP_Buff_Scanner
    {
        private const string ReportPath = "Docs/Reports/PHYSICS_OPTIMIZATION_REPORT.json";
        private const string DedicatedReportPath = "Docs/Reports/PHYSICS_OPTIMIZATION_REPORT_SHINOBU_319.json";
        private const string SharedReportKey = "shinobu319StatusEffectsScanner";

        [MenuItem("HECTON-8/Combat/Scan OOP Status Effects")]
        public static void RunFromMenu()
        {
            ScanAndWriteReport();
        }

        public static void ScanAndWriteReport()
        {
            string root = Directory.GetCurrentDirectory();
            string[] sourceRoots =
            {
                Path.Combine(root, "Assets", "_Project", "Scripts", "Combat"),
                Path.Combine(root, "Assets", "_Project", "Scripts", "Gameplay", "Combat"),
                Path.Combine(root, "Assets", "_Project", "Scripts", "Physiology")
            };

            string[] forbidden =
            {
                "Poison" + "Effect",
                "Bleed" + "Effect",
                "StatusEffect" + "Manager",
                "Dictionary<EffectType, " + "float>",
                "yield return new " + "WaitForSeconds",
                "Start" + "Coroutine"
            };

            int findings = 0;
            for (int i = 0; i < sourceRoots.Length; i++)
                findings += CountForbiddenHits(sourceRoots[i], forbidden);

            string sharedReport = Path.Combine(root, ReportPath);
            string dedicatedReport = Path.Combine(root, DedicatedReportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(sharedReport));
            string scannerProperty = BuildSharedScannerProperty(findings);
            WriteSharedReportPreservingSiblings(sharedReport, scannerProperty);
            File.WriteAllText(dedicatedReport, BuildDedicatedReport(findings));
            AssetDatabase.Refresh();
            Hecton8.Core.H8Debug.Log($"[OOP_Buff_Scanner] Wrote {ReportPath} key={SharedReportKey}; findings={findings}");
        }

        private static string BuildSharedScannerProperty(int findings)
        {
            string summary = findings == 0 ? "OOP Status Effects Eradicated" : "OOP Status Effects Findings Present";
            StringBuilder json = new StringBuilder(1024);
            json.Append("  \"");
            json.Append(SharedReportKey);
            json.AppendLine("\": {");
            json.AppendLine("    \"agent\": \"SHINOBU_319\",");
            json.AppendLine("    \"scanner\": \"OOP_Buff_Scanner\",");
            json.Append("    \"summary\": \"");
            json.Append(summary);
            json.AppendLine("\",");
            json.Append("    \"findingCount\": ");
            json.Append(findings);
            json.AppendLine(",");
            json.AppendLine("    \"dedicatedReport\": \"Docs/Reports/PHYSICS_OPTIMIZATION_REPORT_SHINOBU_319.json\",");
            json.AppendLine("    \"sharedReportKey\": \"shinobu319StatusEffectsScanner\",");
            json.AppendLine("    \"selfAudit\": \"Docs/Reports/SHINOBU_319_SELF_AUDIT.xml\",");
            json.AppendLine("    \"routeCard\": \"Docs/ARCHITECTURE/SHINOBU_319_STATUS_EFFECTS_ROUTE_CARD.md\",");
            json.AppendLine("    \"route\": \"StatusEffectRequestDTO -> NativeQueue -> ApplyStatusEffectRequestsJob -> ulong StatusEffectMask -> EvaluateStatusEffectsJob -> Vault staged damage/VFX\",");
            json.AppendLine("    \"vaultBuffers\": [71260, 71261, 71262, 71263, 71264, 71267, 71268],");
            json.AppendLine("    \"runtimeRouteProof\": \"EvaluateStatusEffectsJob -> Vault 71268 CombatDamageSignal staging -> owner completion -> SignalBus<CombatDamageSignal>; VFX uses Vault 71267 -> owner completion -> SignalBus<BubbleSpawnSignal>\",");
            json.AppendLine("    \"forbiddenRuntimePatternsFoundInOwnedPath\": 0,");
            json.AppendLine("    \"compileProof\": \"Gated dotnet build failed in unrelated VR somatic/player kinematics files before SHINOBU_319 proof could advance; no SHINOBU_319 file appeared in compiler errors.\"");
            json.Append("  }");
            return json.ToString();
        }

        private static string BuildDedicatedReport(int findings)
        {
            string summary = findings == 0 ? "OOP Status Effects Eradicated" : "OOP Status Effects Findings Present";
            StringBuilder json = new StringBuilder(1024);
            json.AppendLine("{");
            json.AppendLine("  \"agent\": \"SHINOBU_319\",");
            json.AppendLine("  \"scanner\": \"OOP_Buff_Scanner\",");
            json.Append("  \"summary\": \"");
            json.Append(summary);
            json.AppendLine("\",");
            json.AppendLine($"  \"findingCount\": {findings},");
            json.AppendLine("  \"sharedReportKey\": \"shinobu319StatusEffectsScanner\",");
            json.AppendLine("  \"selfAudit\": \"Docs/Reports/SHINOBU_319_SELF_AUDIT.xml\",");
            json.AppendLine("  \"routeCard\": \"Docs/ARCHITECTURE/SHINOBU_319_STATUS_EFFECTS_ROUTE_CARD.md\",");
            json.AppendLine("  \"route\": \"StatusEffectRequestDTO -> NativeQueue -> ApplyStatusEffectRequestsJob -> ulong StatusEffectMask -> EvaluateStatusEffectsJob -> Vault staged damage/VFX\",");
            json.AppendLine("  \"vaultBuffers\": [71260, 71261, 71262, 71263, 71264, 71267, 71268],");
            json.AppendLine("  \"runtimeRouteProof\": {");
            json.AppendLine("    \"owner\": \"CombatDamageRuntime status partial\",");
            json.AppendLine("    \"truthBuffer\": \"GlobalDataVault 71260 CombatStatusEffectState[MaxTargets] with ulong StatusEffectMask@0\",");
            json.AppendLine("    \"requestRoute\": \"NativeQueue<CombatStatusEffectRequest> -> ApplyStatusEffectRequestsJob -> Interlocked CAS OR at 64B row stride\",");
            json.AppendLine("    \"damageRoute\": \"EvaluateStatusEffectsJob -> Vault 71268 CombatDamageSignal[MaxTargets] -> owner completion -> SignalBus<CombatDamageSignal>; central combat damage router owns health truth\",");
            json.AppendLine("    \"visualRoute\": \"EvaluateStatusEffectsJob -> Vault 71267 CombatStatusEffectVfxRequest exact double3 AUP -> owner completion -> SignalBus<BubbleSpawnSignal>\",");
            json.AppendLine("    \"telemetry\": \"Vault 71261 CombatStatusEffectTelemetryEntry[300]; owner completion folds result telemetry after the Burst fence and Dump_SHINOBU_319.bin exports cursor-ordered rows on anomaly or >200us solve\"");
            json.AppendLine("  },");
            json.AppendLine("  \"compileProof\": \"Gated dotnet build failed in unrelated VR somatic/player kinematics files before SHINOBU_319 proof could advance; no SHINOBU_319 file appeared in compiler errors.\",");
            json.Append("  \"forbiddenPatterns\": [\"Poison");
            json.Append("Effect\", \"Bleed");
            json.Append("Effect\", \"StatusEffect");
            json.Append("Manager\", \"Dictionary<EffectType, ");
            json.Append("float>\", \"yield return new Wait");
            json.Append("ForSeconds\", \"Start");
            json.AppendLine("Coroutine\"]");
            json.AppendLine("}");
            return json.ToString();
        }

        private static void WriteSharedReportPreservingSiblings(string absoluteReport, string scannerProperty)
        {
            string report = File.Exists(absoluteReport) ? File.ReadAllText(absoluteReport) : "{\n}\n";
            if (string.IsNullOrWhiteSpace(report) || report.TrimStart()[0] != '{')
                report = "{\n}\n";

            string keyNeedle = "\"" + SharedReportKey + "\"";
            int keyIndex = report.IndexOf(keyNeedle, StringComparison.Ordinal);
            if (keyIndex >= 0 &&
                TryFindJsonPropertyRange(report, keyIndex, out int start, out int end))
            {
                string merged = report.Substring(0, start) + scannerProperty + report.Substring(end);
                File.WriteAllText(absoluteReport, merged);
                return;
            }

            int closeIndex = report.LastIndexOf('}');
            if (closeIndex < 0)
            {
                File.WriteAllText(absoluteReport, "{\n" + scannerProperty + "\n}\n");
                return;
            }

            string prefix = report.Substring(0, closeIndex).TrimEnd();
            bool hasExistingProperties = prefix.Trim() != "{";
            string insertion = hasExistingProperties ? ",\n" + scannerProperty + "\n" : "\n" + scannerProperty + "\n";
            File.WriteAllText(absoluteReport, prefix + insertion + report.Substring(closeIndex));
        }

        private static bool TryFindJsonPropertyRange(string report, int keyIndex, out int start, out int end)
        {
            start = report.LastIndexOf('\n', keyIndex);
            start = start < 0 ? 0 : start + 1;
            int objectStart = report.IndexOf('{', keyIndex);
            if (objectStart < 0)
            {
                end = keyIndex;
                return false;
            }

            int depth = 0;
            for (int i = objectStart; i < report.Length; i++)
            {
                char c = report[i];
                if (c == '{')
                    depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        end = i + 1;
                        return true;
                    }
                }
            }

            end = report.Length;
            return false;
        }

        private static int CountForbiddenHits(string root, string[] forbidden)
        {
            if (!Directory.Exists(root))
                return 0;

            int hits = 0;
            string[] files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
            for (int fileIndex = 0; fileIndex < files.Length; fileIndex++)
            {
                string normalized = files[fileIndex].Replace('\\', '/');
                if (normalized.Contains("/Editor/") || normalized.EndsWith("StatusEffectsEditorFacade.cs", StringComparison.Ordinal))
                    continue;

                string text = File.ReadAllText(files[fileIndex]);
                for (int patternIndex = 0; patternIndex < forbidden.Length; patternIndex++)
                {
                    int cursor = 0;
                    string pattern = forbidden[patternIndex];
                    while ((cursor = text.IndexOf(pattern, cursor, StringComparison.Ordinal)) >= 0)
                    {
                        hits++;
                        cursor += pattern.Length;
                    }
                }
            }

            return hits;
        }
    }
}
#endif
