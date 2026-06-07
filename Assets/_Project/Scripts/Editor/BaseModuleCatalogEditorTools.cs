#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Hecton8.Building;
using Hecton8.Construction;
using Hecton8.Core;
using Hecton8.Gameplay;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Editor
{
    [InitializeOnLoad]
    internal static class BaseModuleCatalogLayoutValidator
    {
        static BaseModuleCatalogLayoutValidator()
        {
            EditorApplication.delayCall += ValidateFromEditorLoad;
        }

        [MenuItem("HECTON-8/Construction/Validate Base Module Catalog Layout")]
        internal static void ValidateFromMenu()
        {
            ValidateLayout(true);
        }

        private static void ValidateFromEditorLoad()
        {
            ValidateLayout(false);
        }

        internal static bool ValidateLayout(bool verbose)
        {
            bool valid = BaseModuleCatalogRuntime.ValidateLayout(
                out int moduleSize,
                out int socketSize,
                out int costSize,
                out int stateSize,
                out int telemetrySize);

            valid &= CheckOffset<ModuleDefinitionDTO>(nameof(ModuleDefinitionDTO.PrefabHashID), 0);
            valid &= CheckOffset<ModuleDefinitionDTO>(nameof(ModuleDefinitionDTO.ModuleClassHash), 4);
            valid &= CheckOffset<ModuleDefinitionDTO>(nameof(ModuleDefinitionDTO.BoundingBoxExtents), 8);
            valid &= CheckOffset<ModuleDefinitionDTO>(nameof(ModuleDefinitionDTO.SocketCount), 20);
            valid &= CheckOffset<ModuleDefinitionDTO>(nameof(ModuleDefinitionDTO.SocketStartIndex), 24);
            valid &= CheckOffset<ModuleDefinitionDTO>(nameof(ModuleDefinitionDTO.BaseStrength), 28);
            valid &= CheckOffset<ModuleDefinitionDTO>(nameof(ModuleDefinitionDTO.AllowedBiomesMask), 32);
            valid &= CheckOffset<SocketDefinitionDTO>(nameof(SocketDefinitionDTO.LocalOffset), 0);
            valid &= CheckOffset<SocketDefinitionDTO>(nameof(SocketDefinitionDTO.Normal), 12);
            valid &= CheckOffset<SocketDefinitionDTO>(nameof(SocketDefinitionDTO.AllowedConnectionsMask), 24);

            if (!valid)
            {
                Debug.LogError(
                    $"[SHINOBU_216] Base module catalog layout invalid. Module={moduleSize}, Socket={socketSize}, Cost={costSize}, State={stateSize}, Telemetry={telemetrySize}");
                return false;
            }

            if (verbose)
            {
                Debug.Log(
                    $"[SHINOBU_216] Base module catalog layout OK. Module={moduleSize}, Socket={socketSize}, Cost={costSize}, State={stateSize}, Telemetry={telemetrySize}");
            }

            return true;
        }

        private static bool CheckOffset<T>(string fieldName, int expectedOffset)
        {
            int actual = Marshal.OffsetOf(typeof(T), fieldName).ToInt32();
            if (actual == expectedOffset)
                return true;

            Debug.LogError($"[SHINOBU_216] {typeof(T).Name}.{fieldName} offset {actual}, expected {expectedOffset}.");
            return false;
        }
    }

    internal sealed class BaseModuleCatalogEditorWindow : EditorWindow
    {
        private const string NativeMemoryOwner = nameof(BaseModuleCatalogEditorWindow);
        private const string CsvCostsLabel = "csvCosts";
        private const string DefaultBinaryPath = "Assets/_Project/Data/Construction/BaseModuleCatalog.h8bin";
        private const string DefaultCsvPath = "Data/module_build_costs.csv";
        private readonly List<BaseModuleTemplate> _templates = new List<BaseModuleTemplate>(128);
        private ScrollView _scroll;
        private Label _summaryLabel;

        [MenuItem("HECTON-8/Construction/Base Module Catalog")]
        private static void Open()
        {
            GetWindow<BaseModuleCatalogEditorWindow>("Base Module Catalog");
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.paddingLeft = 8;
            rootVisualElement.style.paddingRight = 8;
            rootVisualElement.style.paddingTop = 8;
            rootVisualElement.style.paddingBottom = 8;

            _summaryLabel = new Label("No scan yet.");
            rootVisualElement.Add(_summaryLabel);

            VisualElement row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            row.Add(new Button(Refresh) { text = "Refresh" });
            row.Add(new Button(BakeCatalogBinary) { text = "BAKE CATALOG BINARY" });
            row.Add(new Button(HierarchyDataScanner.WriteReport) { text = "Scan Hierarchy Data" });
            row.Add(new Button(BaseModuleCatalogLayoutValidator.ValidateFromMenu) { text = "Validate Layout" });
            row.Add(new Button(RunSelfAudit) { text = "Self Audit" });
            rootVisualElement.Add(row);

            _scroll = new ScrollView();
            rootVisualElement.Add(_scroll);
            Refresh();
        }

        private void Refresh()
        {
            _templates.Clear();
            string[] guids = AssetDatabase.FindAssets("t:BaseModuleTemplate");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                BaseModuleTemplate template = AssetDatabase.LoadAssetAtPath<BaseModuleTemplate>(path);
                if (template != null)
                    _templates.Add(template);
            }

            _templates.Sort((a, b) => a.TemplateHashId.CompareTo(b.TemplateHashId));
            RebuildList();
        }

        private void RebuildList()
        {
            if (_scroll == null)
                return;

            _scroll.Clear();
            _summaryLabel.text = $"Templates: {_templates.Count}. Runtime DTO source: BaseModuleTemplate authoring facade -> .h8bin.";
            for (int i = 0; i < _templates.Count; i++)
            {
                BaseModuleTemplate template = _templates[i];
                VisualElement block = new VisualElement();
                block.style.borderBottomWidth = 1;
                block.style.borderBottomColor = new Color(0.18f, 0.18f, 0.18f, 1f);
                block.style.paddingBottom = 4;
                block.style.marginBottom = 4;

                Vector3 bounds = template.ProxyBoundsSize;
                BaseModuleTemplate.SocketDefinition[] sockets = template.SocketDefinitions;
                block.Add(new Label(
                    template.name +
                    " | Hash " + template.TemplateHashId +
                    " | Bounds " + bounds.x.ToString("0.###", CultureInfo.InvariantCulture) +
                    "," + bounds.y.ToString("0.###", CultureInfo.InvariantCulture) +
                    "," + bounds.z.ToString("0.###", CultureInfo.InvariantCulture) +
                    " | Sockets " + (sockets != null ? sockets.Length : 0)));

                if (sockets != null)
                {
                    for (int s = 0; s < sockets.Length; s++)
                    {
                        BaseModuleTemplate.SocketDefinition socket = sockets[s];
                        Vector3 p = socket.LocalPosition;
                        block.Add(new Label(
                            "  [" + s +
                            "] " + socket.Direction +
                            " offset=(" + p.x.ToString("0.###", CultureInfo.InvariantCulture) +
                            "," + p.y.ToString("0.###", CultureInfo.InvariantCulture) +
                            "," + p.z.ToString("0.###", CultureInfo.InvariantCulture) +
                            ") mask=0x" + BaseModuleCatalogRuntime.ComputeCompatibilityMask(socket.CompatibleType).ToString("X8")));
                    }
                }

                _scroll.Add(block);
            }
        }

        private void RunSelfAudit()
        {
            bool ok = BaseModuleCatalogRuntime.RunSelfAudit(out ModuleCatalogSelfAuditDTO audit);
            _summaryLabel.text = $"SelfAudit ok={ok} flags=0x{audit.Flags:X8} module={audit.ModuleDefinitionBytes} socket={audit.SocketDefinitionBytes} cost={audit.ModuleCostBytes}";
        }

        private unsafe void BakeCatalogBinary()
        {
            if (!BaseModuleCatalogLayoutValidator.ValidateLayout(true))
                return;

            Refresh();
            List<ModuleDefinitionDTO> modules = new List<ModuleDefinitionDTO>(_templates.Count);
            List<SocketDefinitionDTO> sockets = new List<SocketDefinitionDTO>(_templates.Count * 6);
            List<ModuleCostDTO> costs = new List<ModuleCostDTO>(_templates.Count);
            NativeArray<ModuleCostDTO> csvCosts = default;
            int csvCostCount = 0;

            try
            {
                TryLoadCsvCosts(ref csvCosts, out csvCostCount);
                for (int i = 0; i < _templates.Count; i++)
                {
                    BaseModuleTemplate template = _templates[i];
                    int socketStart = sockets.Count;
                    if (!BaseModuleCatalogRuntime.TryBuildModuleFromTemplate(template, socketStart, out ModuleDefinitionDTO module))
                        continue;

                    BaseModuleTemplate.SocketDefinition[] socketDefinitions = template.SocketDefinitions;
                    if (socketDefinitions != null)
                    {
                        for (int s = 0; s < socketDefinitions.Length; s++)
                        {
                            if (BaseModuleCatalogRuntime.TryBuildSocketFromTemplate(template, s, out SocketDefinitionDTO socket))
                                sockets.Add(socket);
                        }
                    }

                    modules.Add(module);
                    costs.Add(ResolveCost(template, csvCosts, csvCostCount));
                }

                SortByPrefabHash(modules);
                SortByPrefabHash(costs);
                WriteBinary(modules, sockets, costs, DefaultBinaryPath);
                AssetDatabase.Refresh();
                _summaryLabel.text = $"Baked {modules.Count} modules, {sockets.Count} sockets, {costs.Count} costs -> {DefaultBinaryPath}";
            }
            finally
            {
                DisposeTrackedArray(ref csvCosts);
            }
        }

        private static void TryLoadCsvCosts(ref NativeArray<ModuleCostDTO> costs, out int count)
        {
            count = 0;
            string path = Path.Combine(Directory.GetCurrentDirectory(), DefaultCsvPath);
            if (!File.Exists(path))
                return;

            byte[] bytes = File.ReadAllBytes(path);
            costs = AllocateTrackedArray<ModuleCostDTO>(512, Allocator.Temp, NativeArrayOptions.UninitializedMemory, CsvCostsLabel, NativeAllocationLifetime.Temp);
            BaseModuleCatalogRuntime.TryParseBuildCostCsv(bytes, costs, out count);
        }

        private static NativeArray<T> AllocateTrackedArray<T>(int length, Allocator allocator, NativeArrayOptions options, string label, NativeAllocationLifetime lifetime) where T : struct
        {
            NativeArray<T> array = new NativeArray<T>(length, allocator, options);
            if (!array.IsCreated)
                throw new InvalidOperationException("[BaseModuleCatalogEditorWindow] NativeArray allocation failed for " + label + ".");

            try
            {
                int sentinelId = NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, lifetime);
                if (sentinelId <= 0)
                    throw new InvalidOperationException("[BaseModuleCatalogEditorWindow] NativeMemorySentinel rejected NativeArray registration for " + label + ".");
            }
            catch
            {
                array.Dispose();
                throw;
            }

            return array;
        }

        private static void DisposeTrackedArray<T>(ref NativeArray<T> array) where T : struct
        {
            if (!array.IsCreated)
                return;

            try
            {
                NativeMemorySentinel.UnregisterNativeArray(array);
            }
            finally
            {
                array.Dispose();
                array = default;
            }
        }

        private static ModuleCostDTO ResolveCost(BaseModuleTemplate template, NativeArray<ModuleCostDTO> csvCosts, int csvCostCount)
        {
            uint prefabHash = unchecked((uint)template.TemplateHashId);
            if (csvCosts.IsCreated)
            {
                for (int i = 0; i < csvCostCount; i++)
                {
                    ModuleCostDTO csvCost = csvCosts[i];
                    if (csvCost.PrefabHashID == prefabHash)
                        return csvCost;
                }
            }

            ModuleCostDTO cost = default;
            cost.PrefabHashID = prefabHash;
            BaseModuleTemplate.ItemHashCost[] authored = template.BuildCost;
            int authoredCount = authored != null ? math.min(4, authored.Length) : 0;
            cost.CostCount = (uint)authoredCount;
            for (int i = 0; i < authoredCount; i++)
            {
                BaseModuleTemplate.ItemHashCost item = authored[i];
                SetCostPair(ref cost, i, unchecked((uint)item.ItemHashId), item.Amount);
            }

            return cost;
        }

        private static void SetCostPair(ref ModuleCostDTO cost, int index, uint itemHash, int quantity)
        {
            switch (index)
            {
                case 0:
                    cost.ItemHash0 = itemHash;
                    cost.Quantity0 = quantity;
                    break;
                case 1:
                    cost.ItemHash1 = itemHash;
                    cost.Quantity1 = quantity;
                    break;
                case 2:
                    cost.ItemHash2 = itemHash;
                    cost.Quantity2 = quantity;
                    break;
                case 3:
                    cost.ItemHash3 = itemHash;
                    cost.Quantity3 = quantity;
                    break;
            }
        }

        private static void SortByPrefabHash(List<ModuleDefinitionDTO> modules)
        {
            modules.Sort((a, b) => a.PrefabHashID.CompareTo(b.PrefabHashID));
        }

        private static void SortByPrefabHash(List<ModuleCostDTO> costs)
        {
            costs.Sort((a, b) => a.PrefabHashID.CompareTo(b.PrefabHashID));
        }

        private static unsafe void WriteBinary(
            List<ModuleDefinitionDTO> modules,
            List<SocketDefinitionDTO> sockets,
            List<ModuleCostDTO> costs,
            string assetPath)
        {
            int headerSize = UnsafeUtility.SizeOf<ModuleCatalogBinaryHeader>();
            int moduleOffset = headerSize;
            int socketOffset = moduleOffset + modules.Count * UnsafeUtility.SizeOf<ModuleDefinitionDTO>();
            int costOffset = socketOffset + sockets.Count * UnsafeUtility.SizeOf<SocketDefinitionDTO>();
            int totalBytes = costOffset + costs.Count * UnsafeUtility.SizeOf<ModuleCostDTO>();
            byte[] bytes = new byte[totalBytes];

            ModuleCatalogBinaryHeader header = default;
            header.Magic = BaseModuleCatalogRuntime.BinaryMagic;
            header.Version = BaseModuleCatalogRuntime.BinaryVersion;
            header.ModuleCount = (uint)modules.Count;
            header.SocketCount = (uint)sockets.Count;
            header.CostCount = (uint)costs.Count;
            header.ModuleByteOffset = (uint)moduleOffset;
            header.SocketByteOffset = (uint)socketOffset;
            header.CostByteOffset = (uint)costOffset;
            header.Flags = BaseModuleCatalogRuntime.CatalogImmutableFlag | BaseModuleCatalogRuntime.BinaryLittleEndianFlag;
            header.CatalogHash = 0u;
            header.ByteLength = (uint)totalBytes;

            fixed (byte* ptr = bytes)
            {
                UnsafeUtility.WriteArrayElement(ptr, 0, header);
                for (int i = 0; i < modules.Count; i++)
                    UnsafeUtility.WriteArrayElement(ptr + moduleOffset, i, modules[i]);
                for (int i = 0; i < sockets.Count; i++)
                    UnsafeUtility.WriteArrayElement(ptr + socketOffset, i, sockets[i]);
                for (int i = 0; i < costs.Count; i++)
                    UnsafeUtility.WriteArrayElement(ptr + costOffset, i, costs[i]);

                header.Checksum = ComputeChecksum(ptr, totalBytes);
                header.CatalogHash = header.Checksum;
                UnsafeUtility.WriteArrayElement(ptr, 0, header);
            }

            string fullPath = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
            string directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            WriteBytesAtomic(fullPath, bytes);
        }

        private static void WriteBytesAtomic(string path, byte[] bytes)
        {
            string tempPath = path + ".tmp";
            try
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);

                File.WriteAllBytes(tempPath, bytes);
                if (File.Exists(path))
                    File.Replace(tempPath, path, null, true);
                else
                    File.Move(tempPath, path);
            }
            catch
            {
                TryDeleteFileNoThrow(tempPath);
                throw;
            }
        }

        private static void TryDeleteFileNoThrow(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
            }
        }

        private static unsafe uint ComputeChecksum(byte* bytes, int length)
        {
            int headerSize = UnsafeUtility.SizeOf<ModuleCatalogBinaryHeader>();
            if (length <= headerSize)
                return 0u;

            uint2 hash = xxHash3.Hash64(bytes + headerSize, (long)(length - headerSize));
            return hash.x ^ hash.y;
        }
    }

    internal static class BaseModuleCatalogGizmos
    {
        [DrawGizmo(GizmoType.Selected | GizmoType.Active)]
        private static void DrawModuleMarkerGizmo(ModuleMarker marker, GizmoType gizmoType)
        {
            if (marker == null || marker.Data == null || marker.Data.ModuleTemplate == null)
                return;

            DrawTemplate(marker.transform, marker.Data.ModuleTemplate);
        }

        private static void DrawTemplate(Transform transform, BaseModuleTemplate template)
        {
            if (TryDrawCatalogTemplate(transform, template))
                return;

            Vector3 center = transform.TransformPoint(template.ProxyBoundsCenter);
            Vector3 size = template.ProxyBoundsSize;
            Gizmos.color = Color.green;
            Matrix4x4 previous = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(center, transform.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, size);
            Gizmos.matrix = previous;

            BaseModuleTemplate.SocketDefinition[] sockets = template.SocketDefinitions;
            if (sockets == null)
                return;

            for (int i = 0; i < sockets.Length; i++)
            {
                if (!BaseModuleCatalogRuntime.TryBuildSocketFromTemplate(template, i, out SocketDefinitionDTO socket))
                    continue;

                Vector3 position = transform.TransformPoint(socket.LocalOffset);
                Vector3 normal = transform.TransformDirection(socket.Normal);
                Gizmos.color = Color.blue;
                Gizmos.DrawWireSphere(position, 0.12f);
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(position, position + normal * 0.5f);
            }
        }

        private static bool TryDrawCatalogTemplate(Transform transform, BaseModuleTemplate template)
        {
            if (transform == null || template == null)
                return false;

            uint prefabHash = unchecked((uint)template.TemplateHashId);
            if (!BaseModuleCatalogRuntime.TryGetModuleSocketRangeFromVault(
                    GlobalRegistry.DataVault,
                    prefabHash,
                    out NativeArray<SocketDefinitionDTO>.ReadOnly sockets,
                    out int socketStart,
                    out int socketCount,
                    out ModuleDefinitionDTO module))
                return false;

            float3 extents = module.BoundingBoxExtents;
            Vector3 size = new Vector3(extents.x * 2f, extents.y * 2f, extents.z * 2f);
            Gizmos.color = Color.green;
            Matrix4x4 previous = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, size);
            Gizmos.matrix = previous;

            int end = math.min(socketStart + socketCount, sockets.Length);
            for (int i = socketStart; i < end; i++)
            {
                SocketDefinitionDTO socket = sockets[i];
                Vector3 position = transform.TransformPoint(socket.LocalOffset);
                Vector3 normal = transform.TransformDirection(socket.Normal);
                Gizmos.color = Color.blue;
                Gizmos.DrawWireSphere(position, 0.12f);
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(position, position + normal * 0.5f);
            }

            return true;
        }
    }

    internal static class HierarchyDataScanner
    {
        private const string ReportPath = "Docs/Reports/CONSTRUCTION_OPTIMIZATION_REPORT.json";

        [MenuItem("HECTON-8/Construction/Write Hierarchy Data Scanner Report")]
        internal static void WriteReport()
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab");
            StringBuilder builder = new StringBuilder(16 * 1024);
            int scanned = 0;
            int offenders = 0;
            builder.AppendLine("{");
            builder.AppendLine("  \"agent\": \"SHINOBU_216\",");
            builder.AppendLine("  \"scanner\": \"Hierarchy_Data_Scanner\",");
            builder.AppendLine("  \"offenders\": [");

            bool first = true;
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    continue;

                if (!IsConstructionPrefab(prefab))
                    continue;

                scanned++;
                int matchCount = CountSocketNamedChildren(prefab.transform);
                if (matchCount <= 0)
                    continue;

                offenders++;
                if (!first)
                    builder.AppendLine(",");
                first = false;
                builder.Append("    { \"path\": \"").Append(Escape(path)).Append("\", \"socketNamedChildren\": ").Append(matchCount).Append(" }");
            }

            builder.AppendLine();
            builder.AppendLine("  ],");
            builder.Append("  \"prefabsScanned\": ").Append(scanned).AppendLine(",");
            builder.Append("  \"offenderCount\": ").Append(offenders).AppendLine(",");
            builder.AppendLine("  \"rule\": \"Runtime snap points must live in BaseModuleCatalog DTOs, not Transform children.\"");
            builder.AppendLine("}");

            string fullPath = Path.Combine(Directory.GetCurrentDirectory(), ReportPath);
            string directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(fullPath, builder.ToString());
            AssetDatabase.Refresh();
            Debug.Log($"[SHINOBU_216] Hierarchy data scanner wrote {ReportPath}. Scanned={scanned}, Offenders={offenders}.");
        }

        private static bool IsConstructionPrefab(GameObject prefab)
        {
            return prefab.GetComponentInChildren<ModuleMarker>(true) != null ||
                   prefab.GetComponentInChildren<BaseModule>(true) != null ||
                   prefab.name.IndexOf("Module", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   prefab.name.IndexOf("Habitat", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static int CountSocketNamedChildren(Transform root)
        {
            int count = 0;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (IsForbiddenSocketName(child.name))
                    count++;
                count += CountSocketNamedChildren(child);
            }

            return count;
        }

        private static bool IsForbiddenSocketName(string name)
        {
            return name.IndexOf("Socket", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("SnapPoint", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Connection", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string Escape(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
#endif
