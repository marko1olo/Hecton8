#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Core.Memory.Layout;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEditor.Build;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Editor
{
    public sealed class Arm64MemoryAlignmentXRayWindow : EditorWindow
    {
        private const string NativeMemoryOwner = nameof(Arm64MemoryAlignmentXRayWindow);
        private const string MockInputLabel = "mockAlignedInput";
        private const string MockOutputLabel = "mockAlignedOutput";
        private const string ReportPath = "Docs/Reports/ARM64_ALIGNMENT_XRAY_REPORT.txt";
        private static readonly Color GoodColor = new Color(0.08f, 0.2f, 0.15f, 1f);
        private static readonly Color BadColor = new Color(0.42f, 0.05f, 0.04f, 1f);
        private static readonly Color ByteColor = new Color(0.18f, 0.29f, 0.35f, 1f);
        private static readonly Color EmptyByteColor = new Color(0.06f, 0.06f, 0.06f, 1f);
        private static readonly Color MisalignedByteColor = new Color(1f, 0.12f, 0.08f, 1f);
        private static readonly MethodInfo UnsafeSizeOfMethod =
            typeof(Arm64MemoryAlignmentXRayWindow).GetMethod(
                nameof(UnsafeSizeOfGeneric),
                BindingFlags.Static | BindingFlags.NonPublic);

        private Label _summary;
        private ScrollView _scroll;

        [MenuItem("Hecton8/Diagnostics/Memory Alignment X-Ray")]
        public static void ShowWindow()
        {
            Arm64MemoryAlignmentXRayWindow window = GetWindow<Arm64MemoryAlignmentXRayWindow>();
            window.titleContent = new GUIContent("Memory Alignment X-Ray");
            window.Refresh();
        }

        [MenuItem("Hecton8/Diagnostics/Run ARM64 Alignment CLI Report")]
        public static void RunArm64MemoryAlignmentCli()
        {
            List<Arm64LayoutRecord> records = ScanRecords();
            string report = BuildReport(records, out int issueCount);
            WriteReport(report);
            if (issueCount > 0)
                throw new BuildFailedException(report);

            Hecton8.Core.H8Debug.Log(report);
        }

        [MenuItem("Hecton8/Diagnostics/Generate Mock Layout Stress Test")]
        public static void GenerateMockLayoutStressTest()
        {
            int badOffset = ResolveFieldOffset(typeof(MockMisalignedLayout).GetField(nameof(MockMisalignedLayout.Value)));
            int goodOffset = ResolveFieldOffset(typeof(MockAlignedLayout).GetField(nameof(MockAlignedLayout.Value)));
            if (badOffset != 4)
                throw new BuildFailedException("MockMisalignedLayout failed to prove the 8-byte offset fault.");

            if (goodOffset != 0 || UnsafeUtility.SizeOf<MockAlignedLayout>() != 32)
                throw new BuildFailedException("MockAlignedLayout failed ARM64 correction proof.");

            NativeArray<MockAlignedLayout> input = AllocateTrackedTempJobArray<MockAlignedLayout>(64, NativeArrayOptions.ClearMemory, MockInputLabel);
            NativeArray<double> output = AllocateTrackedTempJobArray<double>(64, NativeArrayOptions.UninitializedMemory, MockOutputLabel);
            try
            {
                for (int i = 0; i < input.Length; i++)
                {
                    input[i] = new MockAlignedLayout
                    {
                        Value = i + 1.0d,
                        Gain = 0.5d,
                        Flags = 1u
                    };
                }

                new MockAlignedStressJob
                {
                    Input = input,
                    Output = output
                }.Schedule(input.Length, 16).Complete();

                if (output[63] <= 0d)
                    throw new BuildFailedException("MockAlignedStressJob produced invalid output.");
            }
            finally
            {
                DisposeTracked(ref input);
                DisposeTracked(ref output);
            }
        }

        private static NativeArray<T> AllocateTrackedTempJobArray<T>(int length, NativeArrayOptions options, string label) where T : struct
        {
            NativeArray<T> array = new NativeArray<T>(length, Allocator.TempJob, options);
            if (!array.IsCreated)
                throw new InvalidOperationException("[Arm64MemoryAlignmentXRayWindow] NativeArray allocation failed for " + label + ".");

            try
            {
                int sentinelId = NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, NativeAllocationLifetime.TempJob);
                if (sentinelId <= 0)
                    throw new InvalidOperationException("[Arm64MemoryAlignmentXRayWindow] NativeMemorySentinel rejected NativeArray registration for " + label + ".");
            }
            catch
            {
                array.Dispose();
                throw;
            }

            return array;
        }

        private static unsafe void DisposeTracked<T>(ref NativeArray<T> array) where T : struct
        {
            if (!array.IsCreated)
                return;

            void* trackedPointer = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(array);
            System.Exception nativeSentinelCleanupException0 = null;

            try
            {
                NativeMemorySentinel.UnregisterPointer(trackedPointer);
            }
            catch (System.Exception nativeSentinelException0)
            {
                nativeSentinelCleanupException0 = nativeSentinelException0;
            }

            try
            {
                array.Dispose();
            }
            catch (System.Exception nativeSentinelException0)
            {
                if (nativeSentinelCleanupException0 == null)
                    nativeSentinelCleanupException0 = nativeSentinelException0;
            }
            finally
            {
                array = default;
            }

            if (nativeSentinelCleanupException0 != null)
                throw nativeSentinelCleanupException0;
        }

        private void OnEnable()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.paddingLeft = 8;
            rootVisualElement.style.paddingRight = 8;
            rootVisualElement.style.paddingTop = 8;
            rootVisualElement.style.paddingBottom = 8;

            VisualElement toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            Button refresh = new Button(Refresh) { text = "Refresh" };
            Button cli = new Button(RunArm64MemoryAlignmentCli) { text = "Run Strict CLI" };
            Button selfAudit = new Button(Arm64AlignmentSelfAuditReport.WriteSelfAuditReportMenu) { text = "Write SHINOBU_204 Self Audit" };
            toolbar.Add(refresh);
            toolbar.Add(cli);
            toolbar.Add(selfAudit);
            rootVisualElement.Add(toolbar);

            _summary = new Label();
            _summary.style.unityFontStyleAndWeight = FontStyle.Bold;
            _summary.style.marginTop = 8;
            rootVisualElement.Add(_summary);

            _scroll = new ScrollView();
            _scroll.style.flexGrow = 1;
            _scroll.style.marginTop = 8;
            rootVisualElement.Add(_scroll);
            Refresh();
        }

        private void Refresh()
        {
            if (_scroll == null || _summary == null)
                return;

            List<Arm64LayoutRecord> records = ScanRecords();
            string report = BuildReport(records, out int issueCount);
            WriteReport(report);

            _summary.text = "Records: " + records.Count + " | Issues: " + issueCount + " | Report: " + ReportPath;
            _scroll.Clear();

            for (int i = 0; i < records.Count; i++)
                _scroll.Add(BuildRecordElement(records[i]));
        }

        private static VisualElement BuildRecordElement(Arm64LayoutRecord record)
        {
            VisualElement root = new VisualElement();
            root.style.marginBottom = 6;
            root.style.paddingLeft = 6;
            root.style.paddingRight = 6;
            root.style.paddingTop = 6;
            root.style.paddingBottom = 6;
            root.style.backgroundColor = record.Issues.Count == 0 ? GoodColor : BadColor;

            Label header = new Label(record.Type.FullName + " | size " + record.Size);
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            root.Add(header);

            VisualElement map = new VisualElement();
            map.style.flexDirection = FlexDirection.Row;
            map.style.marginTop = 4;
            for (int i = 0; i < 64; i++)
            {
                VisualElement cell = new VisualElement();
                cell.style.width = 6;
                cell.style.height = 12;
                cell.style.marginRight = 1;
                cell.style.backgroundColor = ResolveByteColor(record, i);
                map.Add(cell);
            }
            root.Add(map);

            for (int i = 0; i < record.Fields.Count; i++)
            {
                Arm64FieldRecord field = record.Fields[i];
                root.Add(new Label(field.Name + " @ " + field.Offset + " size " + field.Size));
            }

            for (int i = 0; i < record.Issues.Count; i++)
                root.Add(new Label("ISSUE: " + record.Issues[i]));

            return root;
        }

        private static Color ResolveByteColor(Arm64LayoutRecord record, int byteIndex)
        {
            for (int i = 0; i < record.Fields.Count; i++)
            {
                Arm64FieldRecord field = record.Fields[i];
                if (byteIndex >= field.Offset && byteIndex < field.Offset + field.Size)
                    return field.Misaligned ? MisalignedByteColor : ByteColor;
            }

            return EmptyByteColor;
        }

        private static List<Arm64LayoutRecord> ScanRecords()
        {
            List<Arm64LayoutRecord> records = new List<Arm64LayoutRecord>(256);
            global::System.Reflection.Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int assemblyIndex = 0; assemblyIndex < assemblies.Length; assemblyIndex++)
            {
                Type[] types;
                try
                {
                    types = assemblies[assemblyIndex].GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types;
                }

                if (types == null)
                    continue;

                for (int typeIndex = 0; typeIndex < types.Length; typeIndex++)
                {
                    Type type = types[typeIndex];
                    if (!IsAlignmentTarget(type))
                        continue;

                    records.Add(ScanType(type));
                }
            }

            records.Sort((a, b) => string.CompareOrdinal(a.Type.FullName, b.Type.FullName));
            return records;
        }

        private static bool IsAlignmentTarget(Type type)
        {
            if (type == null ||
                !type.IsValueType ||
                type.IsEnum ||
                type.IsGenericTypeDefinition)
            {
                return false;
            }

            StructLayoutAttribute layout = type.StructLayoutAttribute;
            if (layout == null)
                return false;

            if (typeof(ISignal).IsAssignableFrom(type))
                return true;

            if (type.IsDefined(typeof(BinaryBlittableSafeAttribute), false))
                return true;

            string name = type.Name;
            return name.IndexOf("DTO", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("TelemetryEntry", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("SignalEvent", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Payload", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static Arm64LayoutRecord ScanType(Type type)
        {
            Arm64LayoutRecord record = new Arm64LayoutRecord(type);
            StructLayoutAttribute layout = type.StructLayoutAttribute;
            record.Size = ResolveUnsafeSize(type);

            if (layout == null)
            {
                record.Issues.Add("Missing StructLayout.");
                return record;
            }

            if (layout.Pack == 1)
                record.Issues.Add("Pack=1 is forbidden.");

            if (layout.Value != LayoutKind.Explicit)
                record.Issues.Add("LayoutKind.Explicit required for DTO/signal/vault payload.");

            if (!IsAllowedSize(record.Size))
                record.Issues.Add("Size must be exactly 16, 32, 64, or 128 bytes.");

            FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                if (field.IsStatic)
                    continue;

                int offset = ResolveFieldOffset(field);
                int size = ResolveUnsafeSize(field.FieldType);
                bool misaligned = RequiresEightByteAlignment(field.FieldType) && (offset & 7) != 0;
                record.Fields.Add(new Arm64FieldRecord(field.Name, offset, Math.Max(1, size), misaligned));

                if (layout.Value == LayoutKind.Explicit && offset < 0)
                    record.Issues.Add(field.Name + " missing FieldOffset.");

                if (misaligned)
                    record.Issues.Add(field.Name + " requires 8-byte alignment but offset is " + offset + ".");
            }

            return record;
        }

        private static string BuildReport(List<Arm64LayoutRecord> records, out int issueCount)
        {
            issueCount = 0;
            StringBuilder builder = new StringBuilder(8192);
            builder.AppendLine("ARM64_MEMORY_ALIGNMENT_XRAY");
            builder.AppendLine("Allowed sizes: 16, 32, or exact 64-byte cache-line multiples");
            for (int i = 0; i < records.Count; i++)
            {
                Arm64LayoutRecord record = records[i];
                if (record.Issues.Count == 0)
                    continue;

                issueCount += record.Issues.Count;
                builder.Append(record.Type.FullName).Append(" size=").Append(record.Size).AppendLine();
                for (int j = 0; j < record.Issues.Count; j++)
                    builder.Append("  - ").AppendLine(record.Issues[j]);
            }

            if (issueCount == 0)
                builder.AppendLine("NO_FINDINGS");

            return builder.ToString();
        }

        private static void WriteReport(string report)
        {
            string directory = Path.GetDirectoryName(ReportPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(ReportPath, report, new UTF8Encoding(false));
        }

        private static bool IsAllowedSize(int size)
        {
            return size == 16 || size == 32 || (size >= 64 && (size & 63) == 0);
        }

        private static bool RequiresEightByteAlignment(Type type)
        {
            if (type == typeof(double) ||
                type == typeof(long) ||
                type == typeof(ulong) ||
                type == typeof(IntPtr) ||
                type == typeof(UIntPtr))
            {
                return true;
            }

            string fullName = type.FullName ?? type.Name;
            return fullName.EndsWith(".double2", StringComparison.Ordinal) ||
                   fullName.EndsWith(".double3", StringComparison.Ordinal) ||
                   fullName.EndsWith(".double4", StringComparison.Ordinal) ||
                   fullName.EndsWith(".AbsoluteUniversePosition", StringComparison.Ordinal) ||
                   fullName.IndexOf("Aup", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static int ResolveFieldOffset(FieldInfo field)
        {
            try
            {
                return UnsafeUtility.GetFieldOffset(field);
            }
            catch
            {
                FieldOffsetAttribute offset = field.GetCustomAttribute<FieldOffsetAttribute>();
                return offset != null ? offset.Value : -1;
            }
        }

        private static int ResolveUnsafeSize(Type type)
        {
            try
            {
                MethodInfo generic = UnsafeSizeOfMethod.MakeGenericMethod(type);
                return (int)generic.Invoke(null, null);
            }
            catch
            {
                try
                {
                    return Marshal.SizeOf(type);
                }
                catch
                {
                    return -1;
                }
            }
        }

        private static int UnsafeSizeOfGeneric<T>() where T : struct
        {
            return UnsafeUtility.SizeOf<T>();
        }

        private sealed class Arm64LayoutRecord
        {
            public readonly Type Type;
            public readonly List<Arm64FieldRecord> Fields = new List<Arm64FieldRecord>(16);
            public readonly List<string> Issues = new List<string>(4);
            public int Size;

            public Arm64LayoutRecord(Type type)
            {
                Type = type;
            }
        }

        private readonly struct Arm64FieldRecord
        {
            public readonly string Name;
            public readonly int Offset;
            public readonly int Size;
            public readonly bool Misaligned;

            public Arm64FieldRecord(string name, int offset, int size, bool misaligned)
            {
                Name = name;
                Offset = offset;
                Size = size;
                Misaligned = misaligned;
            }
        }

        [StructLayout(LayoutKind.Explicit, Size = 16)]
        private struct MockMisalignedLayout
        {
            [FieldOffset(0)] public uint Flags;
            [FieldOffset(4)] public double Value;
            [FieldOffset(12)] private uint _pad0;
        }

        [StructLayout(LayoutKind.Explicit, Size = 32)]
        private struct MockAlignedLayout
        {
            [FieldOffset(0)] public double Value;
            [FieldOffset(8)] public double Gain;
            [FieldOffset(16)] public uint Flags;
            [FieldOffset(20)] private uint _pad0;
            [FieldOffset(24)] private ulong _pad1;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct MockAlignedStressJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<MockAlignedLayout> Input;
            [WriteOnly] public NativeArray<double> Output;

            public void Execute(int index)
            {
                MockAlignedLayout item = Input[index];
                Output[index] = (item.Value * item.Gain) + item.Flags;
            }
        }
    }
}
#endif
