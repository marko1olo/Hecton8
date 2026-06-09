using System.Collections.Generic;
using Den.Tools.Matrices;
using Hecton8.Core;
using Hecton8.World;
using MapMagic.Nodes;
using MapMagic.Products;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace MapMagic.Nodes.MatrixGenerators
{
    [System.Serializable]
    [GeneratorMenu(
        menu = "Hecton",
        name = "Macro Surface Materials",
        disengageable = true)]
    [UnityEngine.Scripting.Preserve]
    public sealed class HectonTerrainSurfaceMaterialMapMagicNode : Generator, IMultiOutlet, ICustomComplexity
    {
        private const string NativeMemoryOwner = nameof(HectonTerrainSurfaceMaterialMapMagicNode);
        private const string PrimaryLabel = "primaryMaterials";
        private const string SecondaryLabel = "secondaryMaterials";
        private const string PackedLabel = "packedControls";

        [Den.Tools.GUI.ValAttribute("Shell Sand", "Outlet")]
        public readonly Outlet<MatrixWorld> shellSandOut = new Outlet<MatrixWorld>();

        [Den.Tools.GUI.ValAttribute("Limestone Shelf", "Outlet")]
        public readonly Outlet<MatrixWorld> limestoneShelfOut = new Outlet<MatrixWorld>();

        [Den.Tools.GUI.ValAttribute("Clay Silt", "Outlet")]
        public readonly Outlet<MatrixWorld> claySiltOut = new Outlet<MatrixWorld>();

        [Den.Tools.GUI.ValAttribute("Hard Rock", "Outlet")]
        public readonly Outlet<MatrixWorld> hardRockOut = new Outlet<MatrixWorld>();

        [Den.Tools.GUI.ValAttribute("Brine Salt", "Outlet")]
        public readonly Outlet<MatrixWorld> brineSaltCrustOut = new Outlet<MatrixWorld>();

        [Den.Tools.GUI.ValAttribute("Nodule Plain", "Outlet")]
        public readonly Outlet<MatrixWorld> manganeseNodulePlainOut = new Outlet<MatrixWorld>();

        [Den.Tools.GUI.ValAttribute("Reef Rubble", "Outlet")]
        public readonly Outlet<MatrixWorld> reefRubbleOut = new Outlet<MatrixWorld>();

        [Den.Tools.GUI.ValAttribute("Seep Crust", "Outlet")]
        public readonly Outlet<MatrixWorld> seepCrustOut = new Outlet<MatrixWorld>();

        [Den.Tools.GUI.ValAttribute("Packed Rock", "Outlet")]
        public readonly Outlet<MatrixWorld> packedRockOut = new Outlet<MatrixWorld>();

        [Den.Tools.GUI.ValAttribute("Packed Sand", "Outlet")]
        public readonly Outlet<MatrixWorld> packedSandOut = new Outlet<MatrixWorld>();

        [Den.Tools.GUI.ValAttribute("Packed Silt", "Outlet")]
        public readonly Outlet<MatrixWorld> packedSiltOut = new Outlet<MatrixWorld>();

        [Den.Tools.GUI.ValAttribute("Packed Deposition", "Outlet")]
        public readonly Outlet<MatrixWorld> packedDepositionOut = new Outlet<MatrixWorld>();

        [System.NonSerialized] private IOutlet<object>[] _outletCache;

        [Den.Tools.GUI.ValAttribute("Macro Seed")]
        public int macroSeed = WorldMacroGeologyFields.DefaultAuthoringSeed;

        [Den.Tools.GUI.ValAttribute("Water Surface Y")]
        public float waterSurfaceY;

        [Den.Tools.GUI.ValAttribute("Mask Contrast")]
        public float maskContrast = 1f;

        public float Complexity => 1.6f;

        public float Progress(TileData data) => data.GetProgress(this);

        public IEnumerable<IOutlet<object>> Outlets()
        {
            if (_outletCache == null)
            {
                // COLD ALLOC: IOutlet<object>[12] - MapMagic surface material port enumeration cache - owner: HectonTerrainSurfaceMaterialMapMagicNode
                _outletCache = new IOutlet<object>[12];
                _outletCache[0] = shellSandOut;
                _outletCache[1] = limestoneShelfOut;
                _outletCache[2] = claySiltOut;
                _outletCache[3] = hardRockOut;
                _outletCache[4] = brineSaltCrustOut;
                _outletCache[5] = manganeseNodulePlainOut;
                _outletCache[6] = reefRubbleOut;
                _outletCache[7] = seepCrustOut;
                _outletCache[8] = packedRockOut;
                _outletCache[9] = packedSandOut;
                _outletCache[10] = packedSiltOut;
                _outletCache[11] = packedDepositionOut;
            }

            return _outletCache;
        }

        public override (string, int) GetCodeFileLine() => GetCodeFileLineBase();

        public override void Generate(TileData data, StopToken stop)
        {
            if (stop != null && stop.stop)
                return;

            MatrixWorld shellSand = CreateMatrix(data);
            MatrixWorld limestoneShelf = CreateMatrix(data);
            MatrixWorld claySilt = CreateMatrix(data);
            MatrixWorld hardRock = CreateMatrix(data);
            MatrixWorld brineSalt = CreateMatrix(data);
            MatrixWorld nodulePlain = CreateMatrix(data);
            MatrixWorld reefRubble = CreateMatrix(data);
            MatrixWorld seepCrust = CreateMatrix(data);
            MatrixWorld packedRock = CreateMatrix(data);
            MatrixWorld packedSand = CreateMatrix(data);
            MatrixWorld packedSilt = CreateMatrix(data);
            MatrixWorld packedDeposition = CreateMatrix(data);

            int cellCount = ResolveCellCount(shellSand, out int width, out int height);
            if (!enabled || cellCount <= 0)
            {
                FillFallback(shellSand.arr, packedSand.arr);
                StoreOutputs(data, shellSand, limestoneShelf, claySilt, hardRock, brineSalt, nodulePlain, reefRubble, seepCrust, packedRock, packedSand, packedSilt, packedDeposition);
                return;
            }

            NativeArray<float4> primary = default;
            NativeArray<float4> secondary = default;
            NativeArray<float4> packed = default;
            int primaryRegistrationId = 0;
            int secondaryRegistrationId = 0;
            int packedRegistrationId = 0;
            JobHandle handle = default;
            bool scheduled = false;

            try
            {
                primary = new NativeArray<float4>(cellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                secondary = new NativeArray<float4>(cellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                packed = new NativeArray<float4>(cellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                primaryRegistrationId = RegisterTempJobArray(primary, PrimaryLabel);
                secondaryRegistrationId = RegisterTempJobArray(secondary, SecondaryLabel);
                packedRegistrationId = RegisterTempJobArray(packed, PackedLabel);

                var job = new WorldTerrainSurfaceMaterialMaskJob
                {
                    Primary = primary,
                    Secondary = secondary,
                    PackedControl = packed,
                    Width = width,
                    Height = height,
                    CellSizeMeters = ResolveCellSizeMeters(shellSand),
                    WorldOriginXZ = new double2(shellSand.worldPos.x, shellSand.worldPos.z),
                    MacroGeologyParams = BuildMacroGeologyParams(),
                    MaskContrast = math.isfinite(maskContrast) ? math.max(0.05f, maskContrast) : 1f
                };

                handle = job.Schedule(cellCount, ResolveBatchCount(cellCount));
                scheduled = true;

                // COLD SYNC JOB: MapMagic Generate must publish concrete material matrices before returning to the graph.
                DispatcherJobFence.TryComplete(ref handle, forceComplete: true);
                scheduled = false;

                if (stop != null && stop.stop)
                    return;

                CopyMaterialMasksToMatrices(
                    primary,
                    secondary,
                    packed,
                    shellSand.arr,
                    limestoneShelf.arr,
                    claySilt.arr,
                    hardRock.arr,
                    brineSalt.arr,
                    nodulePlain.arr,
                    reefRubble.arr,
                    seepCrust.arr,
                    packedRock.arr,
                    packedSand.arr,
                    packedSilt.arr,
                    packedDeposition.arr);

                data.SetProgress(this, Complexity);
                StoreOutputs(data, shellSand, limestoneShelf, claySilt, hardRock, brineSalt, nodulePlain, reefRubble, seepCrust, packedRock, packedSand, packedSilt, packedDeposition);
            }
            finally
            {
                if (scheduled)
                    DispatcherJobFence.TryComplete(ref handle, forceComplete: true);

                DisposeTracked(ref primary, ref primaryRegistrationId);
                DisposeTracked(ref secondary, ref secondaryRegistrationId);
                DisposeTracked(ref packed, ref packedRegistrationId);
            }
        }

        private MatrixWorld CreateMatrix(TileData data)
        {
            UnityEngine.Vector3 worldPosition = new UnityEngine.Vector3(
                data.area.full.worldPos.x,
                0f,
                data.area.full.worldPos.z);
            UnityEngine.Vector3 worldSize = new UnityEngine.Vector3(
                data.area.full.worldSize.x,
                0f,
                data.area.full.worldSize.z);

            return new MatrixWorld(
                data.area.full.rect,
                worldPosition,
                worldSize);
        }

        private WorldMacroGeologyParams BuildMacroGeologyParams()
        {
            uint authoringSeed = unchecked((uint)math.max(1, macroSeed));
            uint worldSeed = WorldMacroGeologyFields.CombineWorldSeed(authoringSeed, ResolveRuntimeWorldSeed());
            WorldMacroGeologyParams parameters = WorldMacroGeologyParams.CreateDefault(worldSeed);
            parameters.WaterSurfaceY = math.isfinite(waterSurfaceY) ? waterSurfaceY : 0f;
            return parameters;
        }

        private static int ResolveRuntimeWorldSeed()
        {
            return global::HectonWorldGenerator.TryGetActiveRuntimeWorldSeed(out int runtimeWorldSeed)
                ? runtimeWorldSeed
                : 0;
        }

        private static int ResolveCellCount(MatrixWorld matrix, out int width, out int height)
        {
            width = math.max(1, matrix.rect.size.x);
            height = math.max(1, matrix.rect.size.z);
            int cellCount = matrix.arr != null ? matrix.arr.Length : 0;
            return cellCount > 0 && width * height <= cellCount ? cellCount : 0;
        }

        private static float ResolveCellSizeMeters(MatrixWorld matrix)
        {
            int safeWidth = math.max(1, matrix.rect.size.x - 1);
            return math.max(0.001f, matrix.worldSize.x / safeWidth);
        }

        private static int ResolveBatchCount(int cellCount)
        {
            return math.max(1, math.min(64, cellCount / 16));
        }

        private static int RegisterTempJobArray<T>(NativeArray<T> array, string label)
            where T : struct
        {
            int registrationId = NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, NativeAllocationLifetime.TempJob);
            if (registrationId <= 0)
                throw new System.InvalidOperationException($"Native memory sentinel registration failed for {label}.");

            return registrationId;
        }

        private static void DisposeTracked<T>(ref NativeArray<T> array, ref int registrationId)
            where T : struct
        {
            System.Exception cleanupException = null;

            if (registrationId > 0)
            {
                try
                {
                    NativeMemorySentinel.Unregister(registrationId);
                }
                catch (System.Exception exception)
                {
                    cleanupException = exception;
                }
                finally
                {
                    registrationId = 0;
                }
            }

            if (array.IsCreated)
            {
                try
                {
                    array.Dispose();
                }
                catch (System.Exception exception)
                {
                    if (cleanupException == null)
                        cleanupException = exception;
                }
                finally
                {
                    array = default;
                }
            }
            else
            {
                array = default;
            }

            if (cleanupException != null)
                throw cleanupException;
        }

        private static void CopyMaterialMasksToMatrices(
            NativeArray<float4> primary,
            NativeArray<float4> secondary,
            NativeArray<float4> packed,
            float[] shellSand,
            float[] limestoneShelf,
            float[] claySilt,
            float[] hardRock,
            float[] brineSalt,
            float[] nodulePlain,
            float[] reefRubble,
            float[] seepCrust,
            float[] packedRock,
            float[] packedSand,
            float[] packedSilt,
            float[] packedDeposition)
        {
            int count = primary.Length;
            count = math.min(count, secondary.Length);
            count = math.min(count, packed.Length);
            count = math.min(count, shellSand != null ? shellSand.Length : 0);
            count = math.min(count, limestoneShelf != null ? limestoneShelf.Length : 0);
            count = math.min(count, claySilt != null ? claySilt.Length : 0);
            count = math.min(count, hardRock != null ? hardRock.Length : 0);
            count = math.min(count, brineSalt != null ? brineSalt.Length : 0);
            count = math.min(count, nodulePlain != null ? nodulePlain.Length : 0);
            count = math.min(count, reefRubble != null ? reefRubble.Length : 0);
            count = math.min(count, seepCrust != null ? seepCrust.Length : 0);
            count = math.min(count, packedRock != null ? packedRock.Length : 0);
            count = math.min(count, packedSand != null ? packedSand.Length : 0);
            count = math.min(count, packedSilt != null ? packedSilt.Length : 0);
            count = math.min(count, packedDeposition != null ? packedDeposition.Length : 0);

            for (int i = 0; i < count; i++)
            {
                float4 primaryValue = math.saturate(primary[i]);
                float4 secondaryValue = math.saturate(secondary[i]);
                float4 packedValue = math.saturate(packed[i]);
                shellSand[i] = primaryValue.x;
                limestoneShelf[i] = primaryValue.y;
                claySilt[i] = primaryValue.z;
                hardRock[i] = primaryValue.w;
                brineSalt[i] = secondaryValue.x;
                nodulePlain[i] = secondaryValue.y;
                reefRubble[i] = secondaryValue.z;
                seepCrust[i] = secondaryValue.w;
                packedRock[i] = packedValue.x;
                packedSand[i] = packedValue.y;
                packedSilt[i] = packedValue.z;
                packedDeposition[i] = packedValue.w;
            }
        }

        private static void FillFallback(float[] shellSand, float[] packedSand)
        {
            int count = math.min(shellSand != null ? shellSand.Length : 0, packedSand != null ? packedSand.Length : 0);
            for (int i = 0; i < count; i++)
            {
                shellSand[i] = 1f;
                packedSand[i] = 1f;
            }
        }

        private void StoreOutputs(
            TileData data,
            MatrixWorld shellSand,
            MatrixWorld limestoneShelf,
            MatrixWorld claySilt,
            MatrixWorld hardRock,
            MatrixWorld brineSalt,
            MatrixWorld nodulePlain,
            MatrixWorld reefRubble,
            MatrixWorld seepCrust,
            MatrixWorld packedRock,
            MatrixWorld packedSand,
            MatrixWorld packedSilt,
            MatrixWorld packedDeposition)
        {
            data.StoreProduct(shellSandOut, shellSand);
            data.StoreProduct(limestoneShelfOut, limestoneShelf);
            data.StoreProduct(claySiltOut, claySilt);
            data.StoreProduct(hardRockOut, hardRock);
            data.StoreProduct(brineSaltCrustOut, brineSalt);
            data.StoreProduct(manganeseNodulePlainOut, nodulePlain);
            data.StoreProduct(reefRubbleOut, reefRubble);
            data.StoreProduct(seepCrustOut, seepCrust);
            data.StoreProduct(packedRockOut, packedRock);
            data.StoreProduct(packedSandOut, packedSand);
            data.StoreProduct(packedSiltOut, packedSilt);
            data.StoreProduct(packedDepositionOut, packedDeposition);
        }
    }
}
