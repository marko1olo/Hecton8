#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Hecton8.AI;
using Hecton8.Core;
using Hecton8.Core.Memory;

namespace Hecton8.Gameplay
{
    [InitializeOnLoad]
    internal static class ArmorPenetrationLayoutVerifier
    {
        static ArmorPenetrationLayoutVerifier()
        {
            Validate(logSuccess: false);
        }

        [MenuItem("Hecton8/Combat/Validate Armor LUT Layout")]
        public static void ValidateFromMenu()
        {
            Validate(logSuccess: true);
        }

        public static bool Validate(bool logSuccess)
        {
            bool valid =
                UnsafeUtility.SizeOf<ShinobuArmorPenetrationTable>() == 64 &&
                OffsetOf<ShinobuArmorPenetrationTable>(nameof(ShinobuArmorPenetrationTable.Cells)) == 0 &&
                OffsetOf<ShinobuArmorPenetrationTable>(nameof(ShinobuArmorPenetrationTable.Revision)) == 48 &&
                OffsetOf<ShinobuArmorPenetrationTable>(nameof(ShinobuArmorPenetrationTable.AuthoringHash)) == 52 &&
                OffsetOf<ShinobuArmorPenetrationTable>(nameof(ShinobuArmorPenetrationTable._pad0)) == 56 &&
                UnsafeUtility.SizeOf<ArmorProfileDTO>() == 64 &&
                OffsetOf<ArmorProfileDTO>(nameof(ArmorProfileDTO.SpeciesHashID)) == 0 &&
                OffsetOf<ArmorProfileDTO>(nameof(ArmorProfileDTO.BaseHealth)) == 4 &&
                OffsetOf<ArmorProfileDTO>(nameof(ArmorProfileDTO.BaseArmor)) == 8 &&
                OffsetOf<ArmorProfileDTO>(nameof(ArmorProfileDTO._pad0)) == 12 &&
                OffsetOf<ArmorProfileDTO>(nameof(ArmorProfileDTO.ArmorGridLUT)) == 16 &&
                UnsafeUtility.SizeOf<ArmorPenetrationTuningDTO>() == 64 &&
                UnsafeUtility.SizeOf<ArmorPenetrationTelemetryEntry>() == 64 &&
                UnsafeUtility.SizeOf<ArmorPenetrationResolvedHitDTO>() == 128 &&
                UnsafeUtility.SizeOf<ArmorPenetrationDebugHitDTO>() == 96;

            if (!valid)
            {
                Hecton8.Core.H8Debug.LogError("[ArmorPenetrationLayoutVerifier] Armor LUT DTO layout mismatch. SHINOBU_318 output rejected until fixed.");
                return false;
            }

            if (logSuccess)
                Hecton8.Core.H8Debug.Log("[ArmorPenetrationLayoutVerifier] ArmorProfileDTO=64B with material-row x angle-step 8x6 LUT at offset 16; ShinobuArmorPenetrationTable=64B; resolved hit=128B; telemetry=64B; debug hit=96B.");
            return true;
        }

        private static int OffsetOf<T>(string fieldName)
        {
            FieldInfo field = typeof(T).GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field != null ? UnsafeUtility.GetFieldOffset(field) : -1;
        }
    }

    public sealed class BallisticArmorXRayWindow : EditorWindow
    {
        private Slider _armorMultiplier;
        private Slider _weakPointScalar;
        private Slider _chitinDeflect;
        private Slider _steelDeflect;
        private Slider _quality;
        private FloatField _solveUs;
        private IntegerField _frame;
        private IntegerField _impactCount;
        private IntegerField _weakHits;
        private IntegerField _deflectCount;
        private Label _state;
        private double _nextRefresh;

        [MenuItem("Hecton8/Combat/Ballistic Armor X-Ray")]
        public static void Open()
        {
            BallisticArmorXRayWindow window = GetWindow<BallisticArmorXRayWindow>();
            window.titleContent = new GUIContent("Armor LUT X-Ray");
            window.minSize = new Vector2(360f, 260f);
        }

        private void OnEnable()
        {
            EditorApplication.update -= RefreshTelemetry;
            EditorApplication.update += RefreshTelemetry;
        }

        private void OnDisable()
        {
            EditorApplication.update -= RefreshTelemetry;
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.paddingLeft = 8f;
            root.style.paddingRight = 8f;
            root.style.paddingTop = 8f;
            root.style.paddingBottom = 8f;

            _armorMultiplier = CreateSlider("Armor Multiplier", 0f, 4f);
            _weakPointScalar = CreateSlider("Weak Point Scalar", 0.1f, 4f);
            _chitinDeflect = CreateSlider("Chitin Deflect", 0f, 63f);
            _steelDeflect = CreateSlider("Steel Deflect", 0f, 63f);
            _quality = CreateSlider("Global Quality", 0f, 1f);
            _state = new Label("Telemetry: waiting.");
            _frame = ReadOnlyInt("Frame");
            _impactCount = ReadOnlyInt("Impacts");
            _weakHits = ReadOnlyInt("Weak Hits");
            _deflectCount = ReadOnlyInt("Deflects");
            _solveUs = ReadOnlyFloat("Solve us");

            root.Add(_armorMultiplier);
            root.Add(_weakPointScalar);
            root.Add(_chitinDeflect);
            root.Add(_steelDeflect);
            root.Add(_quality);
            root.Add(new Button(() => CombatDamageRuntime.GenerateMockArmorImpacts(32)) { text = "Generate Mock Armor Burst" });
            root.Add(new Button(RunTortureProof) { text = "Run 10k LUT Torture" });
            root.Add(new Button(RunCasTortureProof) { text = "Run 100 CAS Torture" });
            root.Add(new Button(LoadCsv) { text = "Load fauna_armor_luts.csv" });
            root.Add(new Button(ArmorPenetrationLayoutVerifier.ValidateFromMenu) { text = "Validate 64B Layout" });
            root.Add(new Button(OOP_Hitbox_Scanner.RunFromMenu) { text = "Write Physics Optimization Report" });
            root.Add(_state);
            root.Add(_frame);
            root.Add(_impactCount);
            root.Add(_weakHits);
            root.Add(_deflectCount);
            root.Add(_solveUs);

            _armorMultiplier.RegisterValueChangedCallback(_ => ApplyTuning());
            _weakPointScalar.RegisterValueChangedCallback(_ => ApplyTuning());
            _chitinDeflect.RegisterValueChangedCallback(_ => ApplyTuning());
            _steelDeflect.RegisterValueChangedCallback(_ => ApplyTuning());
            _quality.RegisterValueChangedCallback(_ => ApplyTuning());
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
            if (!CombatDamageRuntime.TryGetArmorTuning(out ArmorPenetrationTuningDTO tuning))
                return;

            _armorMultiplier.SetValueWithoutNotify(tuning.GlobalArmorMultiplier);
            _weakPointScalar.SetValueWithoutNotify(tuning.WeakPointDamageScalar);
            _chitinDeflect.SetValueWithoutNotify(tuning.ChitinDeflectStrength);
            _steelDeflect.SetValueWithoutNotify(tuning.SteelDeflectStrength);
            _quality.SetValueWithoutNotify(tuning.GlobalQualityWeight);
        }

        private void ApplyTuning()
        {
            if (!CombatDamageRuntime.TryGetArmorTuning(out ArmorPenetrationTuningDTO tuning))
                return;

            tuning.GlobalArmorMultiplier = _armorMultiplier.value;
            tuning.WeakPointDamageScalar = _weakPointScalar.value;
            tuning.ChitinDeflectStrength = _chitinDeflect.value;
            tuning.SteelDeflectStrength = _steelDeflect.value;
            tuning.GlobalQualityWeight = _quality.value;
            tuning.Revision++;
            CombatDamageRuntime.WriteArmorTuning(in tuning);
            HomeostasisBrain.SetForcedGlobalQualityWeightForTuner(_quality.value, true);
        }

        private void LoadCsv()
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), "Data", "Balance", "fauna_armor_luts.csv");
            CombatDamageRuntime.TryLoadArmorProfilesCsv(path);
        }

        private void RunTortureProof()
        {
            if (!CombatDamageRuntime.RunArmorPenetrationTortureProof(10000, out ArmorPenetrationTelemetryEntry entry))
            {
                Hecton8.Core.H8Debug.LogWarning("[ArmorPenetrationTorture] Runtime not ready; register at least one combat target before running 10k LUT torture.");
                return;
            }

            Hecton8.Core.H8Debug.Log($"[ArmorPenetrationTorture] impacts={entry.ImpactCount} weak={entry.WeakPointHits} deflect={entry.DeflectCount} solveUs={entry.SolveMicroseconds} flags=0x{entry.Flags:X}");
        }

        private void RunCasTortureProof()
        {
            if (!CombatDamageRuntime.RunAtomicHealthCasTortureProof(100, out int successes, out float finalHealth))
            {
                Hecton8.Core.H8Debug.LogWarning($"[ArmorPenetrationCasTorture] FAILED successes={successes}/100 finalHealth={finalHealth}");
                return;
            }

            Hecton8.Core.H8Debug.Log($"[ArmorPenetrationCasTorture] PASS successes={successes}/100 finalHealth={finalHealth}");
        }

        private void RefreshTelemetry()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now < _nextRefresh || _state == null)
                return;

            _nextRefresh = now + 0.25d;
            if (!CombatDamageRuntime.TryGetLastArmorTelemetry(out ArmorPenetrationTelemetryEntry entry))
            {
                _state.text = "Telemetry: runtime not initialized.";
                return;
            }

            _state.text = (entry.Flags & 0x3u) != 0u ? "Telemetry: fault flag present." : "Telemetry: latest armor solve.";
            _frame.SetValueWithoutNotify(ToInt(entry.Frame));
            _impactCount.SetValueWithoutNotify(ToInt(entry.ImpactCount));
            _weakHits.SetValueWithoutNotify(ToInt(entry.WeakPointHits));
            _deflectCount.SetValueWithoutNotify(ToInt(entry.DeflectCount));
            _solveUs.SetValueWithoutNotify(entry.SolveMicroseconds);
        }

        private static int ToInt(uint value)
        {
            return value > int.MaxValue ? int.MaxValue : (int)value;
        }
    }

    public sealed class ArmorLutDebugGizmo : MonoBehaviour
    {
        public int MaxTargets = 16;
        public float CellScale = 0.08f;

        private unsafe void OnDrawGizmosSelected()
        {
            if (!CombatDamageRuntime.TryGetArmorDebugBuffers(
                    out NativeArray<ArmorProfileDTO>.ReadOnly profiles,
                    out NativeArray<double3>.ReadOnly targetAups,
                    out NativeArray<float3>.ReadOnly halfExtents,
                    out NativeArray<ArmorPenetrationDebugHitDTO>.ReadOnly hits,
                    out int targetCount))
            {
                return;
            }

            double3 origin = HectonFloatingOrigin.CurrentTotalOffsetDouble;
            int count = math.min(math.max(0, MaxTargets), targetCount);
            for (int targetIndex = 0; targetIndex < count; targetIndex++)
            {
                ArmorProfileDTO profile = profiles[targetIndex];
                float3 extents = halfExtents[targetIndex];
                double3 root = targetAups[targetIndex];
                for (int row = 0; row < ShinobuArmorPenetrationTable.MaterialRows; row++)
                {
                    for (int col = 0; col < ShinobuArmorPenetrationTable.AngleSteps; col++)
                    {
                        int lutIndex = (row * ShinobuArmorPenetrationTable.AngleSteps) + col;
                        byte raw = profile.ArmorGridLUT[lutIndex];
                        byte strength = (byte)(raw & 0x3F);
                        float t = strength * (1f / 63f);
                        Gizmos.color = Color.Lerp(new Color(0.1f, 0.7f, 0.25f, 0.55f), new Color(0.9f, 0.15f, 0.08f, 0.65f), t);
                        float x = math.lerp(-extents.x, extents.x, (col + 0.5f) / ShinobuArmorPenetrationTable.AngleSteps);
                        float y = math.lerp(-extents.y, extents.y, (row + 0.5f) / ShinobuArmorPenetrationTable.MaterialRows);
                        Vector3 center = ToRuntime(root + new double3(x, y, -extents.z), origin);
                        Gizmos.DrawWireCube(center, new Vector3(CellScale, CellScale, CellScale));
                    }
                }
            }

            Gizmos.color = Color.yellow;
            int hitCount = math.min(hits.Length, 64);
            for (int i = 0; i < hitCount; i++)
            {
                ArmorPenetrationDebugHitDTO hit = hits[i];
                if (hit.Frame == 0u)
                    continue;

                Vector3 point = ToRuntime(hit.ImpactAup, origin);
                Gizmos.DrawSphere(point, CellScale * 0.5f);
                Gizmos.DrawLine(point, point + new Vector3(hit.SurfaceNormal.x, hit.SurfaceNormal.y, hit.SurfaceNormal.z) * CellScale * 3f);
            }
        }

        private static Vector3 ToRuntime(double3 absolute, double3 origin)
        {
            double3 runtime = absolute - origin;
            return new Vector3((float)runtime.x, (float)runtime.y, (float)runtime.z);
        }
    }

    /// <summary>
    /// Headless Unity batch entry point for X_008 armor LUT runtime proof.
    /// </summary>
    public static class ArmorPenetrationBatchProofRunner
    {
        private const int TortureImpactCount = 10000;
        private const int CasPelletCount = 100;
        private const uint ArmorTelemetryFlagsOverBudget = 1u << 0;
        private const uint ArmorTelemetryFlagsNanGuard = 1u << 1;
        private const string ReportPath = "Docs/Reports/COMBAT_RUNTIME_PROOF_X_008.json";

        /// <summary>
        /// Executes the cold editor proof route used by Unity's -executeMethod command.
        /// </summary>
        public static void Run()
        {
            bool layoutOk = false;
            bool vaultOk = false;
            bool createdVault = false;
            bool targetRegistered = false;
            bool tortureOk = false;
            bool casOk = false;
            bool pass = false;
            int targetId = 0;
            int casSuccesses = 0;
            float casFinalHealth = 0f;
            string failure = string.Empty;
            GlobalDataVault createdBatchVault = null;
            GameObject targetObject = null;
            ArmorPenetrationBatchProofReceiver receiver = null;
            ArmorPenetrationTelemetryEntry tortureEntry = default;

            try
            {
                layoutOk = ArmorPenetrationLayoutVerifier.Validate(logSuccess: true);
                if (!layoutOk)
                {
                    failure = "Armor DTO layout verifier failed.";
                }
                else
                {
                    vaultOk = TryEnsureBatchDataVault(out createdBatchVault, out createdVault);
                    if (!vaultOk)
                    {
                        failure = "GlobalDataVault is unavailable for armor proof.";
                    }
                    else
                    {
                        targetObject = new GameObject("X_008_Armor_Runtime_Proof_Target");
                        receiver = targetObject.AddComponent<ArmorPenetrationBatchProofReceiver>();
                        targetObject.transform.position = Vector3.zero;
                        targetObject.transform.rotation = Quaternion.identity;
                        targetObject.transform.localScale = Vector3.one;
                        targetId = unchecked((int)UnityEngine.EntityId.ToULong(targetObject.GetEntityId()));

                        targetRegistered = CombatDamageRuntime.RegisterTarget(
                            targetId,
                            receiver,
                            currentHealth: 1000f,
                            maximumHealth: 1000f,
                            kind: CombatEntityKind.Fauna,
                            armorClass: CombatArmorClass.Shell,
                            armorValue: 48f,
                            shieldValue: 0f);

                        if (!targetRegistered)
                        {
                            failure = "Combat target registration failed.";
                        }
                        else
                        {
                            CombatDamageRuntime.SetCombatVisualQualityWeight(0.37f);
                            tortureOk = CombatDamageRuntime.RunArmorPenetrationTortureProof(TortureImpactCount, out tortureEntry);
                            if (!tortureOk)
                            {
                                failure = "10k armor LUT torture proof did not execute.";
                            }

                            casOk = CombatDamageRuntime.RunAtomicHealthCasTortureProof(CasPelletCount, out casSuccesses, out casFinalHealth);
                            if (!casOk && string.IsNullOrEmpty(failure))
                                failure = "100-pellet CAS torture proof failed.";

                            pass =
                                tortureOk &&
                                casOk &&
                                casSuccesses == CasPelletCount &&
                                math.abs(casFinalHealth) <= 0.0001f &&
                                (tortureEntry.Flags & (ArmorTelemetryFlagsOverBudget | ArmorTelemetryFlagsNanGuard)) == 0u;

                            if (!pass && string.IsNullOrEmpty(failure))
                                failure = ResolveProofFailure(in tortureEntry, casSuccesses, casFinalHealth);
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                failure = exception.GetType().Name + ": " + exception.Message;
                Hecton8.Core.H8Debug.LogException(exception);
            }
            finally
            {
                if (targetRegistered)
                    CombatDamageRuntime.UnregisterTarget(targetId, receiver);
                if (targetObject != null)
                    UnityEngine.Object.DestroyImmediate(targetObject);
                if (createdVault && createdBatchVault != null)
                {
                    GlobalRegistry.UnregisterDataVault(createdBatchVault);
                    createdBatchVault.Dispose();
                }

                WriteReport(
                    layoutOk,
                    vaultOk,
                    createdVault,
                    targetRegistered,
                    targetId,
                    tortureOk,
                    in tortureEntry,
                    casOk,
                    casSuccesses,
                    casFinalHealth,
                    pass,
                    failure);
            }

            if (pass)
                Hecton8.Core.H8Debug.Log("[ArmorPenetrationBatchProofRunner] PASS. Wrote " + ReportPath);
            else
                Hecton8.Core.H8Debug.LogError("[ArmorPenetrationBatchProofRunner] FAILED: " + failure + " Wrote " + ReportPath);

            if (Application.isBatchMode)
                EditorApplication.Exit(pass ? 0 : 1);
        }

        private static bool TryEnsureBatchDataVault(out GlobalDataVault createdVault, out bool ownsCreatedVault)
        {
            createdVault = null;
            ownsCreatedVault = false;
            IDataVault currentVault = GlobalRegistry.DataVault;
            if (currentVault != null && !currentVault.IsCompactionFenceActive)
                return true;

            createdVault = GlobalDataVault.Create();
            if (createdVault == null || createdVault.IsCompactionFenceActive)
                return false;

            GlobalRegistry.RegisterDataVault(createdVault);
            ownsCreatedVault = true;
            return GlobalRegistry.DataVault != null && !GlobalRegistry.DataVault.IsCompactionFenceActive;
        }

        private static string ResolveProofFailure(in ArmorPenetrationTelemetryEntry tortureEntry, int casSuccesses, float casFinalHealth)
        {
            if ((tortureEntry.Flags & ArmorTelemetryFlagsNanGuard) != 0u)
                return "10k armor LUT torture emitted NaN guard telemetry.";
            if ((tortureEntry.Flags & ArmorTelemetryFlagsOverBudget) != 0u)
                return "10k armor LUT torture exceeded 10us evaluator budget.";
            if (casSuccesses != CasPelletCount)
                return "CAS success count mismatch.";
            if (math.abs(casFinalHealth) > 0.0001f)
                return "CAS final health mismatch.";
            return "Unknown runtime proof failure.";
        }

        private static void WriteReport(
            bool layoutOk,
            bool vaultOk,
            bool createdVault,
            bool targetRegistered,
            int targetId,
            bool tortureOk,
            in ArmorPenetrationTelemetryEntry tortureEntry,
            bool casOk,
            int casSuccesses,
            float casFinalHealth,
            bool pass,
            string failure)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string absolutePath = Path.Combine(projectRoot, ReportPath);
            string directory = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            StringBuilder json = new StringBuilder(1536);
            json.AppendLine("{");
            json.AppendLine("  \"agent\": \"X_008\",");
            json.AppendLine("  \"domain\": \"ECHELON 5 COMBAT & SURVIVAL PHYSIOLOGY\",");
            json.Append("  \"status\": ");
            AppendQuoted(json, pass ? "PASS" : "FAILED");
            json.AppendLine(",");
            json.AppendLine("  \"proof\": {");
            json.AppendLine("    \"layoutOk\": " + Bool(layoutOk) + ",");
            json.AppendLine("    \"vaultOk\": " + Bool(vaultOk) + ",");
            json.AppendLine("    \"createdBatchVault\": " + Bool(createdVault) + ",");
            json.AppendLine("    \"targetRegistered\": " + Bool(targetRegistered) + ",");
            json.AppendLine("    \"targetId\": " + targetId.ToString(CultureInfo.InvariantCulture) + ",");
            json.AppendLine("    \"tortureRequestedImpacts\": " + TortureImpactCount.ToString(CultureInfo.InvariantCulture) + ",");
            json.AppendLine("    \"tortureExecuted\": " + Bool(tortureOk) + ",");
            json.AppendLine("    \"tortureImpactCount\": " + tortureEntry.ImpactCount.ToString(CultureInfo.InvariantCulture) + ",");
            json.AppendLine("    \"tortureWeakPointHits\": " + tortureEntry.WeakPointHits.ToString(CultureInfo.InvariantCulture) + ",");
            json.AppendLine("    \"tortureDeflectCount\": " + tortureEntry.DeflectCount.ToString(CultureInfo.InvariantCulture) + ",");
            json.AppendLine("    \"tortureFlags\": " + tortureEntry.Flags.ToString(CultureInfo.InvariantCulture) + ",");
            json.AppendLine("    \"tortureSolveMicroseconds\": " + Float(tortureEntry.SolveMicroseconds) + ",");
            json.AppendLine("    \"tortureGlobalQualityWeight\": " + Float(tortureEntry.GlobalQualityWeight) + ",");
            json.AppendLine("    \"casRequestedPellets\": " + CasPelletCount.ToString(CultureInfo.InvariantCulture) + ",");
            json.AppendLine("    \"casExecuted\": " + Bool(casOk) + ",");
            json.AppendLine("    \"casSuccesses\": " + casSuccesses.ToString(CultureInfo.InvariantCulture) + ",");
            json.AppendLine("    \"casFinalHealth\": " + Float(casFinalHealth));
            json.AppendLine("  },");
            json.Append("  \"failure\": ");
            AppendQuoted(json, failure);
            json.AppendLine();
            json.AppendLine("}");
            File.WriteAllText(absolutePath, json.ToString(), Encoding.UTF8);
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        }

        private static string Bool(bool value)
        {
            return value ? "true" : "false";
        }

        private static string Float(float value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static void AppendQuoted(StringBuilder builder, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                builder.Append("\"\"");
                return;
            }

            builder.Append('"');
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '\\' || c == '"')
                    builder.Append('\\');
                if (c == '\r')
                {
                    builder.Append("\\r");
                    continue;
                }

                if (c == '\n')
                {
                    builder.Append("\\n");
                    continue;
                }

                builder.Append(c);
            }

            builder.Append('"');
        }

        private sealed class ArmorPenetrationBatchProofReceiver : MonoBehaviour, IDamageReceiver, ICombatHitProfileSource
        {
            public Vector3 CombatForward => Vector3.forward;

            public float CombatHeight => 2f;

            public void ReceiveDamage(in DamagePacket packet)
            {
            }
        }
    }

    internal static class OOP_Hitbox_Scanner
    {
        private const string ReportPath = "Docs/Reports/PHYSICS_OPTIMIZATION_REPORT.json";

        [MenuItem("Hecton8/Combat/Scan OOP Hitboxes")]
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
                Path.Combine(root, "Assets", "_Project", "Scripts", "Fauna"),
                Path.Combine(root, "Assets", "_Project", "Scripts", "ToolHitUtility.cs")
            };

            string physicsRaycastNeedle = "Physics" + ".Raycast";
            string raycastCommandNeedle = "Raycast" + "Command";
            int physicsRaycastHits = CountTextHits(sourceRoots, physicsRaycastNeedle);
            int raycastCommandHits = CountTextHits(sourceRoots, raycastCommandNeedle);
            int sendMessageDamageHits = CountTextHits(sourceRoots, "SendMessage(\"ApplyDamage");
            int idamageableHits = CountTextHits(sourceRoots, "IDamageable");
            int managedTakeDamageHits = CountTextHits(sourceRoots, "TakeDamage(");
            int inverseTransformHitPointHits = CountTextHits(sourceRoots, "InverseTransformPoint(hitPoint)");
            string toolHitUtilityPath = Path.Combine(root, "Assets", "_Project", "Scripts", "ToolHitUtility.cs");
            int registeredToolHitInverseTransformPointHits = CountTextHitsWithinMethod(
                toolHitUtilityPath,
                "private static bool TryQueueCentralDamage",
                "InverseTransformPoint(hitPoint)");
            int registeredToolHitZeroAupFallbackHits = CountTextHitsWithinMethod(
                toolHitUtilityPath,
                "private static bool TryQueueCentralDamage",
                "double3.zero");
            int unregisteredLegacyFallbackInverseTransformPointHits = math.max(
                0,
                inverseTransformHitPointHits - registeredToolHitInverseTransformPointHits);
            int primitiveHitboxPrefabs = CountPrimitiveHitboxPrefabs();

            string absoluteReport = Path.Combine(root, ReportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(absoluteReport));
            StringBuilder json = new StringBuilder(1024);
            json.AppendLine("{");
            json.AppendLine("  \"agent\": \"SHINOBU_318\",");
            json.AppendLine("  \"domain\": \"Armor Penetration LUT\",");
            json.AppendLine("  \"route\": \"CombatDamageSignal -> CombatDamageRuntime partial -> 8x6 ArmorProfileDTO LUT -> native health CAS\",");
            json.AppendLine("  \"checks\": {");
            json.AppendLine($"    \"physicsRaycastHits\": {physicsRaycastHits},");
            json.AppendLine($"    \"raycastCommandHits\": {raycastCommandHits},");
            json.AppendLine($"    \"sendMessageApplyDamageHits\": {sendMessageDamageHits},");
            json.AppendLine($"    \"idamageableHits\": {idamageableHits},");
            json.AppendLine($"    \"managedTakeDamageHits\": {managedTakeDamageHits},");
            json.AppendLine($"    \"inverseTransformPointHitPointHits\": {inverseTransformHitPointHits},");
            json.AppendLine($"    \"registeredToolHitInverseTransformPointHits\": {registeredToolHitInverseTransformPointHits},");
            json.AppendLine($"    \"registeredToolHitZeroAupFallbackHits\": {registeredToolHitZeroAupFallbackHits},");
            json.AppendLine($"    \"unregisteredLegacyFallbackInverseTransformPointHits\": {unregisteredLegacyFallbackInverseTransformPointHits},");
            json.AppendLine($"    \"primitiveHitboxPrefabSuspects\": {primitiveHitboxPrefabs}");
            json.AppendLine("  },");
            json.AppendLine("  \"verdict\": \"Registered armor route must use mathematical AUP/LUT evaluation; unregistered legacy IDamageReceiver fallback is reported separately.\"");
            json.AppendLine("}");
            File.WriteAllText(absoluteReport, json.ToString());
            AssetDatabase.Refresh();
            Hecton8.Core.H8Debug.Log($"[OOP_Hitbox_Scanner] Wrote {ReportPath}");
        }

        private static int CountTextHits(string[] roots, string needle)
        {
            int hits = 0;
            for (int i = 0; i < roots.Length; i++)
            {
                string path = roots[i];
                if (File.Exists(path))
                {
                    CountTextHitsInFile(path, needle, ref hits);
                    continue;
                }

                if (!Directory.Exists(path))
                    continue;

                string[] files = Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories);
                for (int fileIndex = 0; fileIndex < files.Length; fileIndex++)
                {
                    if (string.Equals(Path.GetFileName(files[fileIndex]), "ArmorPenetrationEditorFacade.cs", StringComparison.OrdinalIgnoreCase))
                        continue;

                    CountTextHitsInFile(files[fileIndex], needle, ref hits);
                }
            }

            return hits;
        }

        private static int CountTextHitsWithinMethod(string filePath, string methodSignature, string needle)
        {
            if (!File.Exists(filePath))
                return 0;

            string text = File.ReadAllText(filePath);
            int methodStart = text.IndexOf(methodSignature, StringComparison.Ordinal);
            if (methodStart < 0)
                return 0;

            const char openBrace = (char)123;
            const char closeBrace = (char)125;
            int bodyStart = text.IndexOf(openBrace, methodStart);
            if (bodyStart < 0)
                return 0;

            int depth = 0;
            for (int i = bodyStart; i < text.Length; i++)
            {
                char current = text[i];
                if (current == openBrace)
                {
                    depth++;
                    continue;
                }

                if (current != closeBrace)
                    continue;

                depth--;
                if (depth == 0)
                    return CountTextHitsInRange(text, bodyStart, i - bodyStart + 1, needle);
            }

            return 0;
        }

        private static void CountTextHitsInFile(string filePath, string needle, ref int hits)
        {
            if (string.Equals(Path.GetFileName(filePath), "ArmorPenetrationEditorFacade.cs", StringComparison.OrdinalIgnoreCase))
                return;

            string text = File.ReadAllText(filePath);
            hits += CountTextHitsInRange(text, 0, text.Length, needle);
        }

        private static int CountTextHitsInRange(string text, int start, int length, string needle)
        {
            int hits = 0;
            int cursor = start;
            int end = math.min(text.Length, start + length);
            while (cursor < end && (cursor = text.IndexOf(needle, cursor, end - cursor, StringComparison.Ordinal)) >= 0)
            {
                hits++;
                cursor += needle.Length;
            }

            return hits;
        }

        private static int CountPrimitiveHitboxPrefabs()
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project" });
            int suspects = 0;
            var cachedColliders = new System.Collections.Generic.List<Collider>(64);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null || ComponentReferenceUtility.ResolveOwnedComponent<FaunaBrain>(prefab.transform) == null)
                    continue;

                prefab.GetComponentsInChildren<Collider>(true, cachedColliders);
                int primitiveCount = 0;
                for (int c = 0; c < cachedColliders.Count; c++)
                {
                    Collider collider = cachedColliders[c];
                    if (collider is CapsuleCollider || collider is SphereCollider || collider is BoxCollider)
                        primitiveCount++;
                }

                if (primitiveCount > 1)
                    suspects++;
            }

            return suspects;
        }
    }
}
#endif
