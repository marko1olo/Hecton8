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
using Unity.Jobs;
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

        [MenuItem("Hecton8/Construction/Validate Base Module Catalog Layout")]
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

        [MenuItem("Hecton8/Construction/Base Module Catalog")]
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
            CollectTemplates(_templates);
            RebuildList();
        }

        /// <summary>
        /// The single template-discovery implementation. The GUI list and the bake both call this, so the scan
        /// filter and the hash ordering cannot drift apart between the window and the batchmode entry point.
        /// </summary>
        private static void CollectTemplates(List<BaseModuleTemplate> templates)
        {
            templates.Clear();
            string[] guids = AssetDatabase.FindAssets("t:BaseModuleTemplate");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                BaseModuleTemplate template = AssetDatabase.LoadAssetAtPath<BaseModuleTemplate>(path);
                if (template != null)
                    templates.Add(template);
            }

            templates.Sort((a, b) => a.ResolvePersistentHashId().CompareTo(b.ResolvePersistentHashId()));
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
                    " | Hash " + template.ResolvePersistentHashId() +
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

        /// <summary>
        /// GUI caller. Owns nothing but the label text; the bake itself is
        /// <see cref="TryBakeCatalogBinary"/>, shared with the batchmode entry point.
        /// </summary>
        private void BakeCatalogBinary()
        {
            bool baked = TryBakeCatalogBinary(
                out int moduleCount,
                out int socketCount,
                out int costCount,
                out int byteLength,
                out string binaryPath,
                out string failureReason);

            Refresh();
            _summaryLabel.text = baked
                ? $"Baked {moduleCount} modules, {socketCount} sockets, {costCount} costs, {byteLength} bytes -> {binaryPath}"
                : $"Bake FAILED: {failureReason}";
        }

        /// <summary>
        /// The one and only catalog bake implementation, hoisted off the EditorWindow instance so it is reachable
        /// without a GUI. It owns no byte layout: the header, the three record sections and the checksum are all
        /// written by <see cref="WriteBinary"/>, which remains the single writer in the project. Two callers:
        /// the GUI button in <see cref="CreateGUI"/> and <see cref="BaseModuleCatalogBatchBake"/> for
        /// <c>-executeMethod</c>. Do not copy this body anywhere; call it.
        /// </summary>
        internal static bool TryBakeCatalogBinary(
            out int moduleCount,
            out int socketCount,
            out int costCount,
            out int byteLength,
            out string binaryPath,
            out string failureReason)
        {
            moduleCount = 0;
            socketCount = 0;
            costCount = 0;
            byteLength = 0;
            binaryPath = DefaultBinaryPath;
            failureReason = string.Empty;

            if (!BaseModuleCatalogLayoutValidator.ValidateLayout(true))
            {
                failureReason = "DTO layout validation failed; the layout error above names the offending field.";
                return false;
            }

            List<BaseModuleTemplate> templates = new List<BaseModuleTemplate>(128);
            CollectTemplates(templates);

            List<ModuleDefinitionDTO> modules = new List<ModuleDefinitionDTO>(templates.Count);
            List<SocketDefinitionDTO> sockets = new List<SocketDefinitionDTO>(templates.Count * 6);
            List<ModuleCostDTO> costs = new List<ModuleCostDTO>(templates.Count);
            NativeArray<ModuleCostDTO> csvCosts = default;

            try
            {
                TryLoadCsvCosts(ref csvCosts, out int csvCostCount);
                for (int i = 0; i < templates.Count; i++)
                {
                    BaseModuleTemplate template = templates[i];
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
                byteLength = WriteBinary(modules, sockets, costs, DefaultBinaryPath);
                AssetDatabase.Refresh();
                moduleCount = modules.Count;
                socketCount = sockets.Count;
                costCount = costs.Count;
                return true;
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

        private static unsafe void DisposeTrackedArray<T>(ref NativeArray<T> array) where T : struct
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

        private static ModuleCostDTO ResolveCost(BaseModuleTemplate template, NativeArray<ModuleCostDTO> csvCosts, int csvCostCount)
        {
            uint prefabHash = unchecked((uint)template.ResolvePersistentHashId());
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

        /// <summary>
        /// The single writer of the catalog byte layout. Returns the number of bytes written so callers can log
        /// and verify the artifact without recomputing offsets. Recomputing them elsewhere would be a second
        /// copy of the layout.
        /// </summary>
        private static unsafe int WriteBinary(
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
            File.WriteAllBytes(fullPath, bytes);
            return totalBytes;
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

    /// <summary>
    /// Batchmode entry point for the base module catalog bake. Before this existed, the only way to produce
    /// <c>BaseModuleCatalog.h8bin</c> was a private instance method behind a button on an EditorWindow, so the
    /// artifact could not be produced headlessly at all.
    ///
    /// This is a wrapper and nothing else. It declares no magic, no version, no field offsets, no record
    /// strides and no checksum:
    /// <list type="bullet">
    /// <item>the bytes are produced by <see cref="BaseModuleCatalogEditorWindow.TryBakeCatalogBinary"/>, which
    /// delegates the actual layout to the project's single writer, <c>WriteBinary</c>;</item>
    /// <item>the bytes are then checked by scheduling the shipping reader itself,
    /// <see cref="BaseModuleCatalogRuntime.HydrateModuleCatalogJob"/>, over the file that was just written.</item>
    /// </list>
    /// Because the check runs the real reader rather than a transcription of it, a writer/reader disagreement
    /// surfaces here instead of silently producing a file nothing can hydrate.
    /// </summary>
    public static class BaseModuleCatalogBatchBake
    {
        private const string Marker = "[SHINOBU_216]";
        private const string BakeTag = "BASE_MODULE_CATALOG_BAKE";
        private const string VerifyTag = "BASE_MODULE_CATALOG_VERIFY";

        [MenuItem("Hecton8/Construction/Bake And Verify Base Module Catalog Binary")]
        public static void BakeAndVerifyFromMenu()
        {
            ExecuteGuarded();
        }

        /// <summary>
        /// Batchmode target: <c>-executeMethod Hecton8.Editor.BaseModuleCatalogBatchBake.BakeAndVerifyFromCommandLine</c>.
        /// public, static, no parameters, as <c>-executeMethod</c> requires. Exits non-zero when the bake fails,
        /// when the bake produced an empty catalog, or when the shipping reader rejects the produced bytes.
        /// </summary>
        public static void BakeAndVerifyFromCommandLine()
        {
            EditorApplication.Exit(ExecuteGuarded() ? 0 : 1);
        }

        /// <summary>
        /// An unhandled exception inside <c>-executeMethod</c> would abort before any marked line reached the log,
        /// leaving the caller with a bare stack trace and no verdict. The bake can throw for real: the CSV cost
        /// path in <c>AllocateTrackedArray</c> throws when <c>NativeMemorySentinel</c> refuses a registration, and
        /// that path activates the moment <c>Data/module_build_costs.csv</c> exists. Convert that into the same
        /// FAIL line and exit code as every other failure.
        /// </summary>
        private static bool ExecuteGuarded()
        {
            try
            {
                return Execute();
            }
            catch (System.Exception exception)
            {
                Debug.LogError($"{Marker} {BakeTag} FAIL threw {exception.GetType().Name}: {exception.Message}");
                Debug.LogException(exception);
                return false;
            }
        }

        private static bool Execute()
        {
            if (!BaseModuleCatalogEditorWindow.TryBakeCatalogBinary(
                    out int moduleCount,
                    out int socketCount,
                    out int costCount,
                    out int byteLength,
                    out string binaryPath,
                    out string failureReason))
            {
                Debug.LogError($"{Marker} {BakeTag} FAIL {failureReason}");
                return false;
            }

            Debug.Log(
                $"{Marker} {BakeTag} OK modules={moduleCount} sockets={socketCount} costs={costCount} " +
                $"bytes={byteLength} path={binaryPath}");

            // The empty catalog is the silent no-op this entry point exists to make impossible. With zero
            // templates the writer still emits a structurally valid header-only file, and the reader accepts it:
            // ComputeChecksum returns 0 for a payload of length 0, and the reader skips its checksum comparison
            // when the stored checksum is 0, so the result is reported as Hydrated with ModuleCount 0. "It ran
            // and logged no error" is therefore not evidence that anything was baked - the count is.
            if (moduleCount <= 0)
            {
                Debug.LogError(
                    $"{Marker} {BakeTag} FAIL zero modules baked. AssetDatabase found no usable " +
                    "t:BaseModuleTemplate assets, so the output is a header-only stub that would still hydrate " +
                    "as an empty catalog. Treat this as a failed bake, not an empty one.");
                return false;
            }

            return TryVerifyWithShippingReader(binaryPath, moduleCount, socketCount, costCount);
        }

        /// <summary>
        /// Runs <see cref="BaseModuleCatalogRuntime.HydrateModuleCatalogJob"/>, the code the game itself uses to
        /// read this file, against the freshly written bytes, at the same vault capacities
        /// <see cref="BaseModuleCatalogRuntime.ScheduleHydrateCatalog"/> allocates. A catalog too large for the
        /// real runtime lanes therefore fails here too, without this method knowing what the capacities are.
        /// </summary>
        private static unsafe bool TryVerifyWithShippingReader(
            string assetPath,
            int expectedModules,
            int expectedSockets,
            int expectedCosts)
        {
            string fullPath = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
            if (!File.Exists(fullPath))
            {
                Debug.LogError($"{Marker} {VerifyTag} FAIL no file on disk after a reportedly successful bake: {fullPath}");
                return false;
            }

            byte[] fileBytes = File.ReadAllBytes(fullPath);
            int headerSize = UnsafeUtility.SizeOf<ModuleCatalogBinaryHeader>();
            if (fileBytes.Length < headerSize)
            {
                Debug.LogError(
                    $"{Marker} {VerifyTag} FAIL file is {fileBytes.Length} bytes, shorter than the " +
                    $"{headerSize}-byte header.");
                return false;
            }

            ModuleCatalogBinaryHeader header;
            fixed (byte* filePtr = fileBytes)
            {
                header = UnsafeUtility.ReadArrayElement<ModuleCatalogBinaryHeader>(filePtr, 0);
            }

            if (header.ByteLength != (uint)fileBytes.Length)
            {
                Debug.LogError(
                    $"{Marker} {VerifyTag} FAIL header.ByteLength={header.ByteLength} but the file on disk is " +
                    $"{fileBytes.Length} bytes.");
                return false;
            }

            NativeArray<byte> source = default;
            NativeArray<ModuleCatalogStateDTO> state = default;
            NativeArray<ModuleDefinitionDTO> modules = default;
            NativeArray<SocketDefinitionDTO> sockets = default;
            NativeArray<ModuleCostDTO> costs = default;
            NativeArray<uint> hashToIndex = default;
            try
            {
                // COLD ALLOC: reader-capacity mirrors of the runtime catalog lanes - one-shot batchmode bake
                // verification, disposed in the finally below - owner: BaseModuleCatalogBatchBake.
                source = new NativeArray<byte>(fileBytes.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                source.CopyFrom(fileBytes);
                state = new NativeArray<ModuleCatalogStateDTO>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                modules = new NativeArray<ModuleDefinitionDTO>(
                    BaseModuleCatalogRuntime.DefaultModuleCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                sockets = new NativeArray<SocketDefinitionDTO>(
                    BaseModuleCatalogRuntime.DefaultSocketCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                costs = new NativeArray<ModuleCostDTO>(
                    BaseModuleCatalogRuntime.DefaultCostCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                hashToIndex = new NativeArray<uint>(
                    BaseModuleCatalogRuntime.DefaultHashCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);

                BaseModuleCatalogRuntime.HydrateModuleCatalogJob reader =
                    new BaseModuleCatalogRuntime.HydrateModuleCatalogJob
                    {
                        SourceBytes = source,
                        SourceByteLength = fileBytes.Length,
                        State = state,
                        Modules = modules,
                        Sockets = sockets,
                        Costs = costs,
                        HashToIndex = hashToIndex
                    };
                reader.Schedule().Complete();

                ModuleCatalogStateDTO result = state[0];
                ModuleCatalogHydrationStatus status = (ModuleCatalogHydrationStatus)result.HydrationStatus;
                bool hydrated = status == ModuleCatalogHydrationStatus.Hydrated &&
                                result.ModuleCount == (uint)expectedModules &&
                                result.SocketCount == (uint)expectedSockets &&
                                result.CostCount == (uint)expectedCosts &&
                                result.SourceByteLength == (uint)fileBytes.Length;

                if (!hydrated)
                {
                    Debug.LogError(
                        $"{Marker} {VerifyTag} FAIL status={status}({result.HydrationStatus}) " +
                        $"lastError=0x{result.LastErrorCode:X8} readModules={result.ModuleCount}/{expectedModules} " +
                        $"readSockets={result.SocketCount}/{expectedSockets} readCosts={result.CostCount}/{expectedCosts} " +
                        $"readBytes={result.SourceByteLength}/{fileBytes.Length} path={assetPath}. The writer and the " +
                        "shipping reader disagree about the catalog byte layout; do not ship this artifact.");
                    return false;
                }

                Debug.Log(
                    $"{Marker} {VerifyTag} OK status={status} modules={result.ModuleCount} " +
                    $"sockets={result.SocketCount} costs={result.CostCount} sourceBytes={result.SourceByteLength} " +
                    $"catalogHash=0x{result.CatalogHash:X8} path={assetPath}");
                return true;
            }
            finally
            {
                if (source.IsCreated)
                    source.Dispose();
                if (state.IsCreated)
                    state.Dispose();
                if (modules.IsCreated)
                    modules.Dispose();
                if (sockets.IsCreated)
                    sockets.Dispose();
                if (costs.IsCreated)
                    costs.Dispose();
                if (hashToIndex.IsCreated)
                    hashToIndex.Dispose();
            }
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

            uint prefabHash = unchecked((uint)template.ResolvePersistentHashId());
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

        [MenuItem("Hecton8/Construction/Write Hierarchy Data Scanner Report")]
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
