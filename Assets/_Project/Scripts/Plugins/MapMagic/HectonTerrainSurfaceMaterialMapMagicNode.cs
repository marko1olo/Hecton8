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
        private const string Control1Label = "control1";
        private const string Control2Label = "control2";

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

        [Den.Tools.GUI.ValAttribute("Control 1 X", "Outlet")]
        public readonly Outlet<MatrixWorld> control1XOut = new Outlet<MatrixWorld>();

        [Den.Tools.GUI.ValAttribute("Control 1 Y", "Outlet")]
        public readonly Outlet<MatrixWorld> control1YOut = new Outlet<MatrixWorld>();

        [Den.Tools.GUI.ValAttribute("Control 1 Z", "Outlet")]
        public readonly Outlet<MatrixWorld> control1ZOut = new Outlet<MatrixWorld>();

        [Den.Tools.GUI.ValAttribute("Control 1 W", "Outlet")]
        public readonly Outlet<MatrixWorld> control1WOut = new Outlet<MatrixWorld>();

        [Den.Tools.GUI.ValAttribute("Control 2 X", "Outlet")]
        public readonly Outlet<MatrixWorld> control2XOut = new Outlet<MatrixWorld>();

        [Den.Tools.GUI.ValAttribute("Control 2 Y", "Outlet")]
        public readonly Outlet<MatrixWorld> control2YOut = new Outlet<MatrixWorld>();

        [Den.Tools.GUI.ValAttribute("Control 2 Z", "Outlet")]
        public readonly Outlet<MatrixWorld> control2ZOut = new Outlet<MatrixWorld>();

        [Den.Tools.GUI.ValAttribute("Control 2 W", "Outlet")]
        public readonly Outlet<MatrixWorld> control2WOut = new Outlet<MatrixWorld>();

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
                // COLD ALLOC: IOutlet<object>[16] - MapMagic surface material port enumeration cache - owner: HectonTerrainSurfaceMaterialMapMagicNode
                _outletCache = new IOutlet<object>[16];
                _outletCache[0] = shellSandOut;
                _outletCache[1] = limestoneShelfOut;
                _outletCache[2] = claySiltOut;
                _outletCache[3] = hardRockOut;
                _outletCache[4] = brineSaltCrustOut;
                _outletCache[5] = manganeseNodulePlainOut;
                _outletCache[6] = reefRubbleOut;
                _outletCache[7] = seepCrustOut;
                _outletCache[8] = control1XOut;
                _outletCache[9] = control1YOut;
                _outletCache[10] = control1ZOut;
                _outletCache[11] = control1WOut;
                _outletCache[12] = control2XOut;
                _outletCache[13] = control2YOut;
                _outletCache[14] = control2ZOut;
                _outletCache[15] = control2WOut;
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
            MatrixWorld control1X = CreateMatrix(data);
            MatrixWorld control1Y = CreateMatrix(data);
            MatrixWorld control1Z = CreateMatrix(data);
            MatrixWorld control1W = CreateMatrix(data);
            MatrixWorld control2X = CreateMatrix(data);
            MatrixWorld control2Y = CreateMatrix(data);
            MatrixWorld control2Z = CreateMatrix(data);
            MatrixWorld control2W = CreateMatrix(data);

            int cellCount = ResolveCellCount(shellSand, out int width, out int height);
            if (!enabled || cellCount <= 0)
            {
                FillFallback(shellSand.arr, control1X.arr);
                StoreOutputs(data, shellSand, limestoneShelf, claySilt, hardRock, brineSalt, nodulePlain, reefRubble, seepCrust, control1X, control1Y, control1Z, control1W, control2X, control2Y, control2Z, control2W);
                return;
            }

            NativeArray<float4> primary = default;
            NativeArray<float4> secondary = default;
            NativeArray<float4> control1 = default;
            NativeArray<float4> control2 = default;
            int primaryRegistrationId = 0;
            int secondaryRegistrationId = 0;
            int control1RegistrationId = 0;
            int control2RegistrationId = 0;
            JobHandle handle = default;
            bool scheduled = false;

            try
            {
                primary = new NativeArray<float4>(cellCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                secondary = new NativeArray<float4>(cellCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                control1 = new NativeArray<float4>(cellCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                control2 = new NativeArray<float4>(cellCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                primaryRegistrationId = RegisterPersistentArray(primary, PrimaryLabel);
                secondaryRegistrationId = RegisterPersistentArray(secondary, SecondaryLabel);
                control1RegistrationId = RegisterPersistentArray(control1, Control1Label);
                control2RegistrationId = RegisterPersistentArray(control2, Control2Label);

                var job = new WorldTerrainSurfaceMaterialMaskJob
                {
                    Primary = primary,
                    Secondary = secondary,
                    Control1 = control1,
                    Control2 = control2,
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
                    control1,
                    control2,
                    shellSand.arr,
                    limestoneShelf.arr,
                    claySilt.arr,
                    hardRock.arr,
                    brineSalt.arr,
                    nodulePlain.arr,
                    reefRubble.arr,
                    seepCrust.arr,
                    control1X.arr,
                    control1Y.arr,
                    control1Z.arr,
                    control1W.arr,
                    control2X.arr,
                    control2Y.arr,
                    control2Z.arr,
                    control2W.arr);

                data.SetProgress(this, Complexity);
                StoreOutputs(data, shellSand, limestoneShelf, claySilt, hardRock, brineSalt, nodulePlain, reefRubble, seepCrust, control1X, control1Y, control1Z, control1W, control2X, control2Y, control2Z, control2W);
            }
            finally
            {
                if (scheduled)
                    DispatcherJobFence.TryComplete(ref handle, forceComplete: true);

                DisposeTracked(ref primary, ref primaryRegistrationId);
                DisposeTracked(ref secondary, ref secondaryRegistrationId);
                DisposeTracked(ref control1, ref control1RegistrationId);
                DisposeTracked(ref control2, ref control2RegistrationId);
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

        private static int RegisterPersistentArray<T>(NativeArray<T> array, string label)
            where T : struct
        {
            int registrationId = NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, NativeAllocationLifetime.TransientArena);
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
            NativeArray<float4> control1,
            NativeArray<float4> control2,
            float[] shellSand,
            float[] limestoneShelf,
            float[] claySilt,
            float[] hardRock,
            float[] brineSalt,
            float[] nodulePlain,
            float[] reefRubble,
            float[] seepCrust,
            float[] c1x,
            float[] c1y,
            float[] c1z,
            float[] c1w,
            float[] c2x,
            float[] c2y,
            float[] c2z,
            float[] c2w)
        {
            int count = primary.Length;
            count = math.min(count, secondary.Length);
            count = math.min(count, control1.Length);
            count = math.min(count, control2.Length);
            count = math.min(count, shellSand != null ? shellSand.Length : 0);
            
            for (int i = 0; i < count; i++)
            {
                float4 primaryValue = math.saturate(primary[i]);
                float4 secondaryValue = math.saturate(secondary[i]);
                float4 c1Value = math.saturate(control1[i]);
                float4 c2Value = math.saturate(control2[i]);
                if (shellSand != null) shellSand[i] = primaryValue.x;
                if (limestoneShelf != null) limestoneShelf[i] = primaryValue.y;
                if (claySilt != null) claySilt[i] = primaryValue.z;
                if (hardRock != null) hardRock[i] = primaryValue.w;
                if (brineSalt != null) brineSalt[i] = secondaryValue.x;
                if (nodulePlain != null) nodulePlain[i] = secondaryValue.y;
                if (reefRubble != null) reefRubble[i] = secondaryValue.z;
                if (seepCrust != null) seepCrust[i] = secondaryValue.w;
                if (c1x != null) c1x[i] = c1Value.x;
                if (c1y != null) c1y[i] = c1Value.y;
                if (c1z != null) c1z[i] = c1Value.z;
                if (c1w != null) c1w[i] = c1Value.w;
                if (c2x != null) c2x[i] = c2Value.x;
                if (c2y != null) c2y[i] = c2Value.y;
                if (c2z != null) c2z[i] = c2Value.z;
                if (c2w != null) c2w[i] = c2Value.w;
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
            MatrixWorld c1x,
            MatrixWorld c1y,
            MatrixWorld c1z,
            MatrixWorld c1w,
            MatrixWorld c2x,
            MatrixWorld c2y,
            MatrixWorld c2z,
            MatrixWorld c2w)
        {
            data.StoreProduct(shellSandOut, shellSand);
            data.StoreProduct(limestoneShelfOut, limestoneShelf);
            data.StoreProduct(claySiltOut, claySilt);
            data.StoreProduct(hardRockOut, hardRock);
            data.StoreProduct(brineSaltCrustOut, brineSalt);
            data.StoreProduct(manganeseNodulePlainOut, nodulePlain);
            data.StoreProduct(reefRubbleOut, reefRubble);
            data.StoreProduct(seepCrustOut, seepCrust);
            data.StoreProduct(control1XOut, c1x);
            data.StoreProduct(control1YOut, c1y);
            data.StoreProduct(control1ZOut, c1z);
            data.StoreProduct(control1WOut, c1w);
            data.StoreProduct(control2XOut, c2x);
            data.StoreProduct(control2YOut, c2y);
            data.StoreProduct(control2ZOut, c2z);
            data.StoreProduct(control2WOut, c2w);
        }
    }
}
