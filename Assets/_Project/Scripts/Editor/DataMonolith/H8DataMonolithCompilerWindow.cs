#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Hecton8.Data;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.EditorValidation
{
    public sealed unsafe class H8DataMonolithCompilerWindow : EditorWindow
    {
        private const string MenuPath = "Hecton8/Data Monolith/Compiler Window";
        private const string SchemaFolder = "Data/Balance/Schemas";

        private Label _status;
        private ScrollView _sourceList;
        private ScrollView _layoutList;
        private ScrollView _binaryList;

        [MenuItem(MenuPath)]
        public static void Open()
        {
            H8DataMonolithCompilerWindow window = GetWindow<H8DataMonolithCompilerWindow>();
            window.titleContent = new GUIContent("Data Monolith");
            window.minSize = new Vector2(760f, 520f);
            window.RefreshAll();
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.paddingLeft = 8f;
            rootVisualElement.style.paddingRight = 8f;
            rootVisualElement.style.paddingTop = 8f;
            rootVisualElement.style.paddingBottom = 8f;

            VisualElement toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            Button bakeButton = MakeButton("BAKE MONOLITH", Bake);
            bakeButton.style.width = 160f;
            toolbar.Add(bakeButton);
            toolbar.Add(MakeButton("Schemas", GenerateSchemas));
            toolbar.Add(MakeButton("Inspect", InspectBinary));
            toolbar.Add(MakeButton("Refresh", RefreshAll));
            rootVisualElement.Add(toolbar);

            _status = new Label("Idle");
            _status.style.marginTop = 6f;
            rootVisualElement.Add(_status);

            TwoColumnPane panes = new TwoColumnPane();
            _sourceList = CreateScroll("Sources");
            _layoutList = CreateScroll("Struct Layout");
            _binaryList = CreateScroll("Binary Inspector");
            panes.Left.Add(_sourceList);
            panes.Right.Add(_layoutList);
            panes.Right.Add(_binaryList);
            rootVisualElement.Add(panes);

            RefreshAll();
        }

        private static Button MakeButton(string text, Action action)
        {
            Button button = new Button(action) { text = text };
            button.style.marginRight = 6f;
            return button;
        }

        private static ScrollView CreateScroll(string title)
        {
            ScrollView scroll = new ScrollView();
            scroll.style.flexGrow = 1f;
            scroll.style.marginTop = 8f;
            scroll.Add(MakeHeader(title));
            return scroll;
        }

        private static Label MakeHeader(string text)
        {
            Label label = new Label(text);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            return label;
        }

        private void Bake()
        {
            bool ok = H8DataMonolithCompiler.BakeAll(logSummary: true);
            _status.text = ok ? "Bake OK: " + H8DataMonolithCompiler.OutputAssetPath : "Bake failed: " + H8DataMonolithCompiler.LastError;
            RefreshAll();
        }

        private void GenerateSchemas()
        {
            Directory.CreateDirectory(SchemaFolder);
            WriteTemplate("Items_template.csv", "Id,version_id,Name,Description,CategoryId,Cost,StackMax,MassKg,IconIndex,AccessFrequency");
            WriteTemplate("Fauna_template.csv", "Id,version_id,Name,Description,SwimSpeed,TurnRate,Aggression01,FleeDistanceM,BiolumIntensity,AccessFrequency");
            WriteTemplate("Economy_template.csv", "Id,version_id,Name,Description,BasePrice,Scarcity01,Demand01,SupplyRefreshSeconds,AccessFrequency");
            WriteTemplate("Physics_template.csv", "Id,version_id,Name,Description,MassKg,AddedMass,LinearDrag,Buoyancy,CrushDepthM,AccessFrequency");
            WriteStructTemplate<H8ItemRecord>("H8ItemRecord_struct_template.csv");
            WriteStructTemplate<H8CreatureTraitRecord>("H8CreatureTraitRecord_struct_template.csv");
            WriteStructTemplate<H8EconomyRecord>("H8EconomyRecord_struct_template.csv");
            WriteStructTemplate<H8PhysicsConstantsRecord>("H8PhysicsConstantsRecord_struct_template.csv");
            WriteLayoutManifest();
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            _status.text = "Schemas written: " + SchemaFolder;
            RefreshAll();
        }

        private static void WriteTemplate(string fileName, string header)
        {
            string path = Path.Combine(SchemaFolder, fileName);
            File.WriteAllText(path, header + Environment.NewLine, Encoding.UTF8);
        }

        private static void WriteStructTemplate<T>(string fileName)
            where T : unmanaged
        {
            FieldInfo[] fields = typeof(T).GetFields(BindingFlags.Instance | BindingFlags.Public);
            Array.Sort(fields, CompareFieldOffsets);
            StringBuilder builder = new StringBuilder(512);
            for (int i = 0; i < fields.Length; i++)
            {
                if (i != 0)
                    builder.Append(',');

                builder.Append(fields[i].Name);
            }

            builder.AppendLine();
            File.WriteAllText(Path.Combine(SchemaFolder, fileName), builder.ToString(), Encoding.UTF8);
        }

        private static void WriteLayoutManifest()
        {
            StringBuilder builder = new StringBuilder(4096);
            AppendStructLayout<H8ItemRecord>(builder);
            AppendStructLayout<H8CreatureTraitRecord>(builder);
            AppendStructLayout<H8EconomyRecord>(builder);
            AppendStructLayout<H8PhysicsConstantsRecord>(builder);
            File.WriteAllText(Path.Combine(SchemaFolder, "BinaryLayout_manifest.txt"), builder.ToString(), Encoding.UTF8);
        }

        private static void AppendStructLayout<T>(StringBuilder builder)
            where T : unmanaged
        {
            Type type = typeof(T);
            builder.Append(type.Name).Append(" size=").Append(UnsafeUtility.SizeOf<T>()).AppendLine();
            FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public);
            Array.Sort(fields, CompareFieldOffsets);
            for (int i = 0; i < fields.Length; i++)
            {
                FieldOffsetAttribute offset = fields[i].GetCustomAttribute<FieldOffsetAttribute>();
                builder.Append("  ")
                    .Append(offset != null ? offset.Value : -1)
                    .Append(" : ")
                    .Append(fields[i].FieldType.Name)
                    .Append(' ')
                    .Append(fields[i].Name)
                    .AppendLine();
            }
        }

        private static int CompareFieldOffsets(FieldInfo left, FieldInfo right)
        {
            int leftOffset = left.GetCustomAttribute<FieldOffsetAttribute>()?.Value ?? -1;
            int rightOffset = right.GetCustomAttribute<FieldOffsetAttribute>()?.Value ?? -1;
            return leftOffset.CompareTo(rightOffset);
        }

        private void RefreshAll()
        {
            RefreshSources();
            RefreshLayout();
            InspectBinary();
        }

        private void RefreshSources()
        {
            if (_sourceList == null)
                return;

            _sourceList.Clear();
            _sourceList.Add(MakeHeader("Sources"));
            AppendSourceRoot(H8DataMonolithCompiler.SourceFolder);
            AppendSourceRoot(H8DataMonolithCompiler.BalanceSourceFolder);
        }

        private void AppendSourceRoot(string root)
        {
            if (!Directory.Exists(root))
            {
                _sourceList.Add(new Label(root + " missing"));
                return;
            }

            string[] csv = Directory.GetFiles(root, "*.csv", SearchOption.AllDirectories);
            Array.Sort(csv, StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < csv.Length; i++)
            {
                if (!H8DataMonolithCompiler.IsSourcePath(csv[i]))
                    continue;

                _sourceList.Add(new Label(csv[i].Replace('\\', '/') + "  utc=" + File.GetLastWriteTimeUtc(csv[i]).ToString("u")));
            }
        }

        private void RefreshLayout()
        {
            if (_layoutList == null)
                return;

            _layoutList.Clear();
            _layoutList.Add(MakeHeader("Struct Layout"));
            AddLayoutLine<H8DataBlobHeader>();
            AddLayoutLine<H8DataBlobDirectory>();
            AddLayoutLine<H8DataSectionEntry>();
            AddLayoutLine<H8ItemRecord>();
            AddLayoutLine<H8CreatureTraitRecord>();
            AddLayoutLine<H8EconomyRecord>();
            AddLayoutLine<H8PhysicsConstantsRecord>();
            AddLayoutLine<H8DataMonolithTelemetryEntry>();
        }

        private void AddLayoutLine<T>()
            where T : unmanaged
        {
            _layoutList.Add(new Label(typeof(T).Name + " size=" + UnsafeUtility.SizeOf<T>()));
        }

        private void InspectBinary()
        {
            if (_binaryList == null)
                return;

            _binaryList.Clear();
            _binaryList.Add(MakeHeader("Binary Inspector"));
            if (!string.IsNullOrEmpty(H8DataMonolithCompiler.LastError))
                _binaryList.Add(new Label("Last validation error: " + H8DataMonolithCompiler.LastError));
            string path = H8DataMonolithCompiler.OutputAssetPath;
            if (!File.Exists(path))
            {
                _binaryList.Add(new Label("Missing: " + path));
                return;
            }

            FileInfo info = new FileInfo(path);
            if (info.Length > int.MaxValue)
            {
                _binaryList.Add(new Label("Invalid: too large for editor inspector, bytes=" + info.Length));
                return;
            }

            byte[] bytes = new byte[(int)info.Length];
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                int total = 0;
                while (total < bytes.Length)
                {
                    int read = stream.Read(bytes, total, bytes.Length - total);
                    if (read <= 0)
                        break;

                    total += read;
                }

                if (total != bytes.Length)
                {
                    _binaryList.Add(new Label("Invalid: incomplete editor read, bytes=" + total + "/" + bytes.Length));
                    return;
                }
            }
            if (bytes.Length < H8DataLayoutConstants.HeaderSizeBytes + H8DataLayoutConstants.DirectorySizeBytes)
            {
                _binaryList.Add(new Label("Invalid: too small, bytes=" + bytes.Length));
                return;
            }

            fixed (byte* ptr = bytes)
            {
                H8DataBlobHeader header = UnsafeUtility.ReadArrayElement<H8DataBlobHeader>(ptr, 0);
                H8DataBlobDirectory directory = UnsafeUtility.ReadArrayElement<H8DataBlobDirectory>(ptr + H8DataLayoutConstants.HeaderSizeBytes, 0);
                uint2 hash = xxHash3.Hash64(ptr + H8DataLayoutConstants.HeaderSizeBytes, bytes.Length - H8DataLayoutConstants.HeaderSizeBytes);
                ulong checksum = ((ulong)hash.y << 32) | hash.x;
                _binaryList.Add(new Label("bytes=" + bytes.Length + " sections=" + directory.SectionCount));
                _binaryList.Add(new Label("magic=0x" + header.Magic.ToString("X8") + " version=" + header.FormatVersion + " headerBytes=" + header.HeaderBytes));
                _binaryList.Add(new Label("checksum=" + (checksum == header.Checksum64 ? "PASS" : "FAIL") + " 0x" + header.Checksum64.ToString("X16")));
                if (directory.SectionTableOffset <= bytes.Length && directory.SectionTableBytes <= bytes.Length - directory.SectionTableOffset)
                {
                    H8DataSectionEntry* sections = (H8DataSectionEntry*)(ptr + directory.SectionTableOffset);
                    int count = Mathf.Min(directory.SectionCount, 64);
                    for (int i = 0; i < count; i++)
                    {
                        H8DataSectionEntry section = sections[i];
                        _binaryList.Add(new Label(((H8DataSectionId)section.SectionId) + " count=" + section.Count + " size=" + section.RecordSize + " offset=" + section.OffsetBytes));
                    }
                }
            }
        }

        private sealed class TwoColumnPane : VisualElement
        {
            public readonly VisualElement Left = new VisualElement();
            public readonly VisualElement Right = new VisualElement();

            public TwoColumnPane()
            {
                style.flexDirection = FlexDirection.Row;
                style.flexGrow = 1f;
                Left.style.flexGrow = 1f;
                Left.style.marginRight = 8f;
                Right.style.flexGrow = 1f;
                Add(Left);
                Add(Right);
            }
        }
    }
}
#endif
