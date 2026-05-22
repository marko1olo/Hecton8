#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Hecton8.VFX.Debris
{
    public sealed class ShinobuVoxelSculptorWindow : EditorWindow
    {
        private const int GridResolution = ShinobuDeltaCrusher.DefaultChunkResolution;
        private const int GridCellCount = ShinobuDeltaCrusher.DefaultChunkCellCount;
        private const int RlePairCapacity = GridCellCount * 2;
        private const int StatsLength = 4;
        private const int CounterLength = 1;
        private const string TuningCsvRelativePath = "Assets/_Project/Data/VFX/ShinobuDeltaCrusherTuning.csv";
        private const string TuningBinaryRelativePath = "Assets/_Project/Data/VFX/ShinobuDeltaCrusherTuning.h8bin";
        private const uint TuningBinaryMagic = 0x44584335u; // "DXC5"
        private const uint TuningBinaryVersion = 1u;
        private const uint TuningCsvSchemaHash = 0x57A54D63u; // FNV-1a: gravity_y,bounce,max_debris,mass_units_per_particle

        private float _brushRadius = 6f;
        private int _deltaDensity = -48;
        private int _massUnitsPerParticle = 16;
        private int _maxDebris = ShinobuDeltaCrusher.MinimumQualityDebrisCap;
        private float _gravityY = -5.25f;
        private float _bounce = ShinobuDeltaCrusher.DefaultBounce;
        private int _lastRawBytes;
        private int _lastRleBytes;
        private int _lastRleRatioPermille;
        private int _lastRleFlags;
        private int _lastRemovedMass;
        private int _lastDebrisCount;
        private int _lastDebrisDtoBytes = 32;
        private int _lastCsvRows;
        private int _lastBinaryBytes;
        private uint _lastBinaryHash;
        private string _lastStatus = "Idle";

        [MenuItem("Tools/Hecton-8/Voxel Sculptor")]
        public static void Open()
        {
            GetWindow<ShinobuVoxelSculptorWindow>("Voxel Sculptor");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Brush", EditorStyles.boldLabel);
            _brushRadius = EditorGUILayout.Slider("Brush Size", _brushRadius, 1f, 15f);
            _deltaDensity = EditorGUILayout.IntSlider("Delta Density", _deltaDensity, -255, -1);
            _massUnitsPerParticle = EditorGUILayout.IntSlider("Mass Per Particle", _massUnitsPerParticle, 1, 128);

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Debris", EditorStyles.boldLabel);
            _maxDebris = EditorGUILayout.IntSlider("Max Debris", _maxDebris, ShinobuDeltaCrusher.MinimumQualityDebrisCap, ShinobuDeltaCrusher.MaximumQualityDebrisCap);
            _gravityY = EditorGUILayout.Slider("Gravity Y", _gravityY, -30f, 0f);
            _bounce = EditorGUILayout.Slider("Bounce", _bounce, 0f, 1f);

            EditorGUILayout.Space(6f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Simulate Carve at Camera Center"))
                    RunCarvePreview();

                if (GUILayout.Button("Validate RLE"))
                    RunRleValidation();

                if (GUILayout.Button("Save Tuning"))
                    SaveTuningToVault();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Export CSV"))
                    ExportCurrentTuningCsv();

                if (GUILayout.Button("Import CSV + Binary"))
                    ImportCsvAndBakeBinary();
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Inspector", EditorStyles.boldLabel);
            EditorGUILayout.IntField("Raw Bytes", _lastRawBytes);
            EditorGUILayout.IntField("RLE Bytes", _lastRleBytes);
            EditorGUILayout.IntField("RLE Ratio Permille", _lastRleRatioPermille);
            EditorGUILayout.IntField("RLE Flags", _lastRleFlags);
            EditorGUILayout.IntField("Removed Mass", _lastRemovedMass);
            EditorGUILayout.IntField("Debris Count", _lastDebrisCount);
            EditorGUILayout.IntField("Debris DTO Bytes", _lastDebrisDtoBytes);
            EditorGUILayout.LabelField("CSV Source", TuningCsvRelativePath);
            EditorGUILayout.LabelField("Binary Output", TuningBinaryRelativePath);
            EditorGUILayout.IntField("Schema Version", (int)TuningBinaryVersion);
            EditorGUILayout.LongField("CSV Schema Hash", TuningCsvSchemaHash);
            EditorGUILayout.IntField("CSV Data Rows", _lastCsvRows);
            EditorGUILayout.IntField("Binary Bytes", _lastBinaryBytes);
            EditorGUILayout.LongField("Binary FNV1a", _lastBinaryHash);
            EditorGUILayout.LabelField("Tuning DTO Layout", "CarveDebrisTuningDTO: 32B, float3/float/int/int/uint/uint");
            EditorGUILayout.LabelField("Status", _lastStatus);
        }

        private unsafe void RunCarvePreview()
        {
            NativeArray<sbyte> densities = default;
            NativeArray<sbyte> decompressed = default;
            NativeArray<int> accumulator = default;
            NativeArray<int> removedMass = default;
            NativeArray<int> debrisCount = default;
            NativeArray<int> stats = default;
            NativeArray<int> writtenCount = default;
            NativeArray<DebrisParticleDTO> particles = default;
            NativeList<short> rlePairs = default;

            try
            {
                AllocateScratch(
                    out densities,
                    out decompressed,
                    out accumulator,
                    out removedMass,
                    out debrisCount,
                    out stats,
                    out writtenCount,
                    out particles,
                    out rlePairs);

                new MockVoxelGridGeneratorJob
                {
                    Densities = densities,
                    Density = ShinobuDeltaCrusher.MockSolidDensity
                }.Run(densities.Length);

                new InitializeDensityAccumulatorJob
                {
                    SourceDensities = densities,
                    DensityAccumulator = accumulator
                }.Run(accumulator.Length);

                removedMass[0] = 0;
                debrisCount[0] = 0;

                int radius = Mathf.CeilToInt(_brushRadius);
                int3 center = ResolveCameraCenterCell();
                int3 minCell = math.max(0, center - radius);
                int3 maxCell = math.min(GridResolution - 1, center + radius);
                int3 span = (maxCell - minCell) + 1;
                int candidateCount = span.x * span.y * span.z;

                new VoxelSphericalCarveJob
                {
                    MinCell = minCell,
                    Span = span,
                    Dimensions = new int3(GridResolution),
                    Center = new float3(center) + 0.5f,
                    Radius = _brushRadius,
                    DeltaDensity = _deltaDensity,
                    DensityAccumulatorPtr = (int*)NativeArrayUnsafeUtility.GetUnsafePtr(accumulator),
                    RemovedMassPtr = (int*)NativeArrayUnsafeUtility.GetUnsafePtr(removedMass)
                }.Run(candidateCount);

                new ApplyCarveDensityDeltasJob
                {
                    DensityAccumulator = accumulator,
                    OutputDensities = densities
                }.Run(densities.Length);

                RunRleValidation(densities, decompressed, stats, writtenCount, rlePairs);
                new DebrisMassToCountJob
                {
                    RemovedMass = removedMass,
                    DebrisCount = debrisCount,
                    MassUnitsPerParticle = _massUnitsPerParticle,
                    MaxDebris = _maxDebris
                }.Run();

                int clampedDebrisCount = math.clamp(debrisCount[0], 0, particles.Length);
                new DebrisEmitFromMassJob
                {
                    Particles = particles,
                    DebrisCount = debrisCount,
                    Origin = new float3(0f, 2f, 0f),
                    Radius = _brushRadius * 0.1f,
                    Impulse = new float3(0f, 1f, 0f),
                    MaterialHash = ShinobuDeltaCrusher.TitaniumOreHash,
                    Seed = 0x5348494Eu
                }.Run(math.max(1, clampedDebrisCount));

                new DebrisPhysicsFakeJob
                {
                    Particles = particles,
                    Count = clampedDebrisCount,
                    DeltaTime = 1f / 60f,
                    Gravity = new float3(0f, _gravityY, 0f),
                    Bounce = _bounce,
                    SleepSpeedSq = ShinobuDeltaCrusher.DefaultSleepSpeedSq,
                    Sampler = default
                }.Run(math.max(1, clampedDebrisCount));

                _lastRemovedMass = removedMass[0];
                _lastDebrisCount = clampedDebrisCount;
                _lastStatus = "Carve preview executed";
            }
            finally
            {
                DisposeScratch(
                    ref densities,
                    ref decompressed,
                    ref accumulator,
                    ref removedMass,
                    ref debrisCount,
                    ref stats,
                    ref writtenCount,
                    ref particles,
                    ref rlePairs);
            }
        }

        private int3 ResolveCameraCenterCell()
        {
            Camera camera = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.camera : null;
            if (camera == null)
                return new int3(GridResolution >> 1);

            Vector3 probe = camera.transform.position + camera.transform.forward * Mathf.Max(1f, _brushRadius);
            int half = GridResolution >> 1;
            return new int3(
                Mathf.Clamp(Mathf.RoundToInt(probe.x) + half, 0, GridResolution - 1),
                Mathf.Clamp(Mathf.RoundToInt(probe.y) + half, 0, GridResolution - 1),
                Mathf.Clamp(Mathf.RoundToInt(probe.z) + half, 0, GridResolution - 1));
        }

        private void RunRleValidation()
        {
            NativeArray<sbyte> densities = default;
            NativeArray<sbyte> decompressed = default;
            NativeArray<int> stats = default;
            NativeArray<int> writtenCount = default;
            NativeList<short> rlePairs = default;

            try
            {
                densities = new NativeArray<sbyte>(GridCellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                decompressed = new NativeArray<sbyte>(GridCellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                stats = new NativeArray<int>(StatsLength, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                writtenCount = new NativeArray<int>(CounterLength, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                rlePairs = new NativeList<short>(RlePairCapacity, Allocator.TempJob);

                new MockVoxelGridGeneratorJob
                {
                    Densities = densities,
                    Density = ShinobuDeltaCrusher.MockSolidDensity
                }.Run(densities.Length);

                RunRleValidation(densities, decompressed, stats, writtenCount, rlePairs);
                _lastRemovedMass = 0;
                _lastDebrisCount = 0;
            }
            finally
            {
                if (densities.IsCreated)
                    densities.Dispose();
                if (decompressed.IsCreated)
                    decompressed.Dispose();
                if (stats.IsCreated)
                    stats.Dispose();
                if (writtenCount.IsCreated)
                    writtenCount.Dispose();
                if (rlePairs.IsCreated)
                    rlePairs.Dispose();
            }
        }

        private void RunRleValidation(
            NativeArray<sbyte> densities,
            NativeArray<sbyte> decompressed,
            NativeArray<int> stats,
            NativeArray<int> writtenCount,
            NativeList<short> rlePairs)
        {
            new RleCompressSByteJob
            {
                Input = densities,
                OutputPairs = rlePairs,
                Stats = stats
            }.Run();

            new RleDecompressSByteJob
            {
                InputPairs = rlePairs,
                Output = decompressed,
                WrittenCount = writtenCount
            }.Run();

            bool valid = writtenCount[0] == densities.Length;
            for (int i = 0; valid && i < densities.Length; i++)
                valid = densities[i] == decompressed[i];

            _lastRawBytes = stats[0];
            _lastRleBytes = stats[1];
            _lastRleRatioPermille = stats[2];
            _lastRleFlags = stats.Length > 3 ? stats[3] : 0;
            _lastDebrisDtoBytes = UnsafeUtility.SizeOf<DebrisParticleDTO>();
            _lastStatus = valid ? "RLE valid" : "RLE mismatch";
        }

        private void SaveTuningToVault()
        {
            CarveDebrisTuningDTO tuning = BuildTuningFromFields();
            _lastStatus = TryWriteTuningToVault(in tuning)
                ? "Tuning saved to DataVault job state"
                : "Tuning write rejected";
        }

        private void ExportCurrentTuningCsv()
        {
            string path = ResolveProjectPath(TuningCsvRelativePath);
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            using (StreamWriter writer = new StreamWriter(path, false))
            {
                writer.WriteLine("gravity_y,bounce,max_debris,mass_units_per_particle");
                writer.Write(System.Convert.ToString(_gravityY, CultureInfo.InvariantCulture));
                writer.Write(',');
                writer.Write(System.Convert.ToString(_bounce, CultureInfo.InvariantCulture));
                writer.Write(',');
                writer.Write(System.Convert.ToString(_maxDebris, CultureInfo.InvariantCulture));
                writer.Write(',');
                writer.WriteLine(System.Convert.ToString(_massUnitsPerParticle, CultureInfo.InvariantCulture));
            }

            AssetDatabase.Refresh();
            _lastCsvRows = 1;
            _lastStatus = "CSV exported";
        }

        private void ImportCsvAndBakeBinary()
        {
            if (!TryReadTuningCsv(ResolveProjectPath(TuningCsvRelativePath), out CarveDebrisTuningDTO tuning, out int rowCount))
            {
                _lastStatus = "CSV import failed";
                return;
            }

            ApplyTuningToFields(in tuning);
            string binaryPath = ResolveProjectPath(TuningBinaryRelativePath);
            if (!TryWriteTuningBinary(binaryPath, in tuning))
            {
                _lastStatus = "CSV binary bake failed";
                return;
            }

            _lastCsvRows = rowCount;
            _lastBinaryBytes = ResolveFileLength(binaryPath);
            _lastBinaryHash = ComputeFnv1aFileHash(binaryPath);
            bool wroteVault = TryWriteTuningToVault(in tuning);
            AssetDatabase.Refresh();
            _lastStatus = wroteVault ? "CSV baked to binary and vault" : "CSV baked; vault unavailable";
        }

        private CarveDebrisTuningDTO BuildTuningFromFields()
        {
            return new CarveDebrisTuningDTO
            {
                Gravity = new float3(0f, _gravityY, 0f),
                Bounce = _bounce,
                MaxActiveDebris = _maxDebris,
                MassUnitsPerParticle = _massUnitsPerParticle,
                Flags = 0u,
                Version = TuningBinaryVersion
            };
        }

        private void ApplyTuningToFields(in CarveDebrisTuningDTO tuning)
        {
            _gravityY = math.isfinite(tuning.Gravity.y) ? tuning.Gravity.y : -5.25f;
            _bounce = math.saturate(math.isfinite(tuning.Bounce) ? tuning.Bounce : ShinobuDeltaCrusher.DefaultBounce);
            _maxDebris = math.clamp(tuning.MaxActiveDebris, ShinobuDeltaCrusher.MinimumQualityDebrisCap, ShinobuDeltaCrusher.MaximumQualityDebrisCap);
            _massUnitsPerParticle = math.max(1, tuning.MassUnitsPerParticle);
        }

        private bool TryWriteTuningToVault(in CarveDebrisTuningDTO tuning)
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null)
            {
                return false;
            }

            VaultGenerationHandle<int> jobStateHandle = vault.GetGenerationHandle<int>(
                BufferID.CarveDebrisJobState,
                ShinobuDeltaCrusher.CarveDebrisJobStateLength,
                SystemID.Vfx,
                NativeArrayOptions.ClearMemory);
            if (jobStateHandle.BufferID != unchecked((uint)(int)BufferID.CarveDebrisJobState))
            {
                return false;
            }

            bool locked = vault.TryAcquireWriteLock(in jobStateHandle, SystemID.CoreDiagnostics, out NativeArray<int> jobState);
            if (!locked)
                return false;

            if (!jobState.IsCreated || jobState.Length < ShinobuDeltaCrusher.CarveDebrisJobStateLength)
            {
                vault.ReleaseWriteLock(in jobStateHandle, SystemID.CoreDiagnostics);
                return false;
            }

            try
            {
                return ShinobuDeltaCrusher.TryWriteCarveDebrisTuning(jobState, in tuning);
            }
            finally
            {
                if (locked)
                    vault.ReleaseWriteLock(in jobStateHandle, SystemID.CoreDiagnostics);
            }
        }

        private static bool TryReadTuningCsv(string path, out CarveDebrisTuningDTO tuning, out int dataRowCount)
        {
            tuning = default;
            dataRowCount = 0;
            if (!File.Exists(path))
                return false;

            string[] lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (string.IsNullOrWhiteSpace(line) || line[0] == '#')
                    continue;

                ReadOnlySpan<char> row = line.AsSpan();
                int cursor = 0;
                ReadOnlySpan<char> gravityToken = ReadCsvToken(row, ref cursor);
                if (IsAsciiTokenIgnoreCase(gravityToken, "gravity_y".AsSpan()))
                    continue;

                dataRowCount++;
                if (!TryParseFloat(gravityToken, out float gravityY) ||
                    !TryParseFloat(ReadCsvToken(row, ref cursor), out float bounce) ||
                    !TryParseInt(ReadCsvToken(row, ref cursor), out int maxDebris) ||
                    !TryParseInt(ReadCsvToken(row, ref cursor), out int massUnits))
                {
                    return false;
                }

                tuning = new CarveDebrisTuningDTO
                {
                    Gravity = new float3(0f, gravityY, 0f),
                    Bounce = math.saturate(bounce),
                    MaxActiveDebris = math.clamp(maxDebris, ShinobuDeltaCrusher.MinimumQualityDebrisCap, ShinobuDeltaCrusher.MaximumQualityDebrisCap),
                    MassUnitsPerParticle = math.max(1, massUnits),
                    Flags = 0u,
                    Version = TuningBinaryVersion
                };
                return true;
            }

            return false;
        }

        private static ReadOnlySpan<char> ReadCsvToken(ReadOnlySpan<char> row, ref int cursor)
        {
            if ((uint)cursor >= (uint)row.Length)
                return ReadOnlySpan<char>.Empty;

            int start = cursor;
            while (cursor < row.Length && row[cursor] != ',')
                cursor++;

            ReadOnlySpan<char> token = row.Slice(start, cursor - start);
            if (cursor < row.Length && row[cursor] == ',')
                cursor++;

            return TrimCsvToken(token);
        }

        private static ReadOnlySpan<char> TrimCsvToken(ReadOnlySpan<char> token)
        {
            int start = 0;
            int end = token.Length - 1;
            while (start <= end && char.IsWhiteSpace(token[start]))
                start++;

            while (end >= start && char.IsWhiteSpace(token[end]))
                end--;

            return start <= end ? token.Slice(start, end - start + 1) : ReadOnlySpan<char>.Empty;
        }

        private static bool IsAsciiTokenIgnoreCase(ReadOnlySpan<char> token, ReadOnlySpan<char> expected)
        {
            if (token.Length != expected.Length)
                return false;

            for (int i = 0; i < token.Length; i++)
            {
                char left = token[i];
                char right = expected[i];
                if ((uint)(left - 'A') <= 25u)
                    left = (char)(left + 32);
                if ((uint)(right - 'A') <= 25u)
                    right = (char)(right + 32);
                if (left != right)
                    return false;
            }

            return true;
        }

        private static bool TryParseFloat(ReadOnlySpan<char> cell, out float value)
        {
            return float.TryParse(cell, NumberStyles.Float, CultureInfo.InvariantCulture, out value) &&
                   math.isfinite(value);
        }

        private static bool TryParseInt(ReadOnlySpan<char> cell, out int value)
        {
            return int.TryParse(cell, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryWriteTuningBinary(string path, in CarveDebrisTuningDTO tuning)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            string tempPath = path + ".tmp";
            try
            {
                TryDeleteTemp(tempPath);
                using (FileStream stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(TuningBinaryMagic);
                    writer.Write(TuningBinaryVersion);
                    writer.Write(tuning.Gravity.y);
                    writer.Write(tuning.Bounce);
                    writer.Write(tuning.MaxActiveDebris);
                    writer.Write(tuning.MassUnitsPerParticle);
                    writer.Write(tuning.Flags);
                    writer.Flush();
                    stream.Flush(true);
                }

                if (!TryValidateTuningBinary(tempPath, in tuning))
                {
                    TryDeleteTemp(tempPath);
                    return false;
                }

                if (File.Exists(path))
                    File.Replace(tempPath, path, null);
                else
                    File.Move(tempPath, path);

                return true;
            }
            catch (IOException)
            {
                TryDeleteTemp(tempPath);
                return false;
            }
            catch (System.UnauthorizedAccessException)
            {
                TryDeleteTemp(tempPath);
                return false;
            }
        }

        private static bool TryValidateTuningBinary(string path, in CarveDebrisTuningDTO tuning)
        {
            if (!File.Exists(path))
                return false;

            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                if (stream.Length != 28L)
                    return false;

                uint magic = reader.ReadUInt32();
                uint version = reader.ReadUInt32();
                float gravityY = reader.ReadSingle();
                float bounce = reader.ReadSingle();
                int maxDebris = reader.ReadInt32();
                int massUnits = reader.ReadInt32();
                uint flags = reader.ReadUInt32();
                return magic == TuningBinaryMagic &&
                       version == TuningBinaryVersion &&
                       math.abs(gravityY - tuning.Gravity.y) <= 0.0001f &&
                       math.abs(bounce - tuning.Bounce) <= 0.0001f &&
                       maxDebris == tuning.MaxActiveDebris &&
                       massUnits == tuning.MassUnitsPerParticle &&
                       flags == tuning.Flags;
            }
        }

        private static int ResolveFileLength(string path)
        {
            if (!File.Exists(path))
                return 0;

            long length = new FileInfo(path).Length;
            return length > int.MaxValue ? int.MaxValue : (int)length;
        }

        private static uint ComputeFnv1aFileHash(string path)
        {
            if (!File.Exists(path))
                return 0u;

            uint hash = 2166136261u;
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                int value;
                while ((value = stream.ReadByte()) >= 0)
                    hash = (hash ^ (byte)value) * 16777619u;
            }

            return hash;
        }

        private static void TryDeleteTemp(string tempPath)
        {
            try
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
            catch (IOException)
            {
            }
            catch (System.UnauthorizedAccessException)
            {
            }
        }

        private static string ResolveProjectPath(string projectRelativePath)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", projectRelativePath));
        }

        private static void AllocateScratch(
            out NativeArray<sbyte> densities,
            out NativeArray<sbyte> decompressed,
            out NativeArray<int> accumulator,
            out NativeArray<int> removedMass,
            out NativeArray<int> debrisCount,
            out NativeArray<int> stats,
            out NativeArray<int> writtenCount,
            out NativeArray<DebrisParticleDTO> particles,
            out NativeList<short> rlePairs)
        {
            // COLD EDITOR ALLOC: disposed before returning to the editor loop; runtime buffers live in GlobalDataVault.
            densities = new NativeArray<sbyte>(GridCellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            decompressed = new NativeArray<sbyte>(GridCellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            accumulator = new NativeArray<int>(GridCellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            removedMass = new NativeArray<int>(CounterLength, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            debrisCount = new NativeArray<int>(CounterLength, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            stats = new NativeArray<int>(StatsLength, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            writtenCount = new NativeArray<int>(CounterLength, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            particles = new NativeArray<DebrisParticleDTO>(ShinobuDeltaCrusher.MaximumQualityDebrisCap, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            rlePairs = new NativeList<short>(RlePairCapacity, Allocator.TempJob);
        }

        private static void DisposeScratch(
            ref NativeArray<sbyte> densities,
            ref NativeArray<sbyte> decompressed,
            ref NativeArray<int> accumulator,
            ref NativeArray<int> removedMass,
            ref NativeArray<int> debrisCount,
            ref NativeArray<int> stats,
            ref NativeArray<int> writtenCount,
            ref NativeArray<DebrisParticleDTO> particles,
            ref NativeList<short> rlePairs)
        {
            if (densities.IsCreated)
                densities.Dispose();
            if (decompressed.IsCreated)
                decompressed.Dispose();
            if (accumulator.IsCreated)
                accumulator.Dispose();
            if (removedMass.IsCreated)
                removedMass.Dispose();
            if (debrisCount.IsCreated)
                debrisCount.Dispose();
            if (stats.IsCreated)
                stats.Dispose();
            if (writtenCount.IsCreated)
                writtenCount.Dispose();
            if (particles.IsCreated)
                particles.Dispose();
            if (rlePairs.IsCreated)
                rlePairs.Dispose();
        }
    }
}
#endif
