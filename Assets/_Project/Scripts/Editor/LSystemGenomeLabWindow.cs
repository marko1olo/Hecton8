#if UNITY_EDITOR
using System;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Graphics.Materials;
using Hecton8.World;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    public sealed class LSystemGenomeLabWindow : EditorWindow
    {
        private NativeArray<byte> _previewExpandedSymbols;
        private NativeArray<byte> _previewScratchSymbols;
        private NativeArray<BranchMatrixDTO> _previewBranchMatrices;
        private NativeArray<HazardZoneDTO> _previewHazardZones;
        private NativeArray<TurtleStackFrameDTO> _previewTurtleStack;
        private int _selectedGenomeIndex;
        private int _previewMatrixCount;
        private bool _previewActive;
        private Vector3 _previewRoot = Vector3.zero;
        private string _axiomText = "X";
        private Color _color = Color.cyan;
        private Mesh _previewSegmentMesh;
        private Material _previewMaterial;

        [MenuItem("Hecton8/Tools/L-System Genome Lab")]
        public static void Open()
        {
            GetWindow<LSystemGenomeLabWindow>("L-System Genome Lab");
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui -= DrawPreviewSceneGui;
            SceneView.duringSceneGui += DrawPreviewSceneGui;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DrawPreviewSceneGui;
            DisposePreviewWorkspace();
            if (_previewSegmentMesh != null)
                DestroyImmediate(_previewSegmentMesh);
            if (_previewMaterial != null)
                DestroyImmediate(_previewMaterial);
        }

        private void OnGUI()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null ||
                !vault.TryGetGenerationHandle(BufferID.FloraGenomeDtos, out VaultGenerationHandle<FloraGenomeDTO> handle))
            {
                EditorGUILayout.HelpBox("Vault flora genomes are not bound. Mock preview is available.", MessageType.Info);
                DrawMockPreviewControls();
                return;
            }

            if (!vault.TryReadHandle(in handle, out NativeArray<FloraGenomeDTO> genomes))
                genomes = default;
            if (!genomes.IsCreated || genomes.Length == 0)
            {
                EditorGUILayout.HelpBox("FloraGenomeDTO buffer exists but is empty.", MessageType.Warning);
                DrawMockPreviewControls();
                return;
            }

            _selectedGenomeIndex = EditorGUILayout.IntSlider("Genome Index", _selectedGenomeIndex, 0, genomes.Length - 1);
            FloraGenomeDTO genome = genomes[_selectedGenomeIndex];
            if (genome.SpeciesHash == 0u)
                EditorGUILayout.HelpBox("Selected slot is empty.", MessageType.Warning);

            EditorGUI.BeginChangeCheck();
            uint speciesHash = (uint)EditorGUILayout.LongField("Species Hash", genome.SpeciesHash);
            float baseScale = EditorGUILayout.FloatField("Base Scale", genome.BaseScale);
            float angleDeg = EditorGUILayout.FloatField("Branch Angle", math.degrees(genome.BranchAngleRadians));
            float segmentLength = EditorGUILayout.FloatField("Segment Length", genome.SegmentLengthMeters);
            _axiomText = EditorGUILayout.TextField("Axiom", genome.Axiom.ToString());
            float biolum = EditorGUILayout.Slider("Biolum Threshold", genome.BiolumThreshold, 0f, 2f);
            _color = EditorGUILayout.ColorField("HDR Color", DecodeColor(genome.PackedColorHDR));
            int maxIterations = EditorGUILayout.IntSlider("Max Iterations", genome.MaxIterations, 1, FloraGenomeLSystemConstants.MaxRuntimeIterations);
            int ruleProfile = EditorGUILayout.IntSlider("Rule Profile", genome.RuleProfile, 0, 2);
            FloraHazardFlags hazardFlags = (FloraHazardFlags)EditorGUILayout.EnumFlagsField("Hazards", (FloraHazardFlags)genome.HazardFlags);
            if (EditorGUI.EndChangeCheck())
            {
                genome.SpeciesHash = speciesHash;
                genome.BaseScale = math.max(0.01f, baseScale);
                genome.BranchAngleRadians = math.radians(angleDeg);
                genome.SegmentLengthMeters = math.max(0.01f, segmentLength);
                genome.Axiom = BuildFixedAxiom(_axiomText);
                genome.BiolumThreshold = biolum;
                genome.PackedColorHDR = EncodeColor(_color);
                genome.MaxIterations = (byte)maxIterations;
                genome.RuleProfile = (byte)ruleProfile;
                genome.HazardFlags = (byte)hazardFlags;
                if (TryAcquireEditorWriteView(
                        vault,
                        BufferID.FloraGenomeDtos,
                        out VaultGenerationHandle<FloraGenomeDTO> writeHandle,
                        out NativeArray<FloraGenomeDTO> writableGenomes))
                {
                    try
                    {
                        if ((uint)_selectedGenomeIndex < (uint)writableGenomes.Length)
                            writableGenomes[_selectedGenomeIndex] = genome;
                    }
                    finally
                    {
                        vault.ReleaseWriteLock(in writeHandle, SystemID.CoreDiagnostics);
                    }
                }
            }

            _previewRoot = EditorGUILayout.Vector3Field("Preview Root", _previewRoot);
            if (GUILayout.Button("Preview in Scene"))
                BuildPreview(genomes, _selectedGenomeIndex);
        }

        private void DrawMockPreviewControls()
        {
            _previewRoot = EditorGUILayout.Vector3Field("Preview Root", _previewRoot);
            if (GUILayout.Button("Preview Mock Kelp"))
            {
                using NativeArray<FloraGenomeDTO> mockGenomes = new NativeArray<FloraGenomeDTO>(FloraGenomeLSystemConstants.MaxMockGenomeCount, Allocator.TempJob);
                MockGenomeGenerator.Populate(mockGenomes);
                BuildPreview(mockGenomes, 0);
            }
        }

        private static bool TryAcquireEditorWriteView<T>(
            IDataVault vault,
            BufferID bufferId,
            out VaultGenerationHandle<T> handle,
            out NativeArray<T> buffer)
            where T : struct
        {
            handle = default;
            buffer = default;
            if (vault == null ||
                !vault.TryGetGenerationHandle(bufferId, out handle) ||
                !vault.TryAcquireWriteLock(in handle, SystemID.CoreDiagnostics, out buffer))
            {
                return false;
            }

            if (buffer.IsCreated)
                return true;

            vault.ReleaseWriteLock(in handle, SystemID.CoreDiagnostics);
            return false;
        }

        private void BuildPreview(NativeArray<FloraGenomeDTO> genomes, int genomeIndex)
        {
            if (!genomes.IsCreated || (uint)genomeIndex >= (uint)genomes.Length)
                return;

            if (!IsPreviewWorkspaceCreated)
            {
                CreateEditorPreviewWorkspace(
                    FloraGenomeLSystemConstants.DefaultExpandedSymbolCapacity,
                    4096,
                    256,
                    FloraGenomeLSystemConstants.DefaultTurtleStackCapacity,
                    Allocator.Persistent);
            }

            FloraGenomeChunkWorkspace previewWorkspace = BuildPreviewWorkspaceView();

            NativeArray<FloraPlantSeedDTO> seed = new NativeArray<FloraPlantSeedDTO>(1, Allocator.TempJob);
            NativeArray<FloraGenomeJobStats> stats = new NativeArray<FloraGenomeJobStats>(1, Allocator.TempJob);
            NativeArray<FloraGenomeBlackBoxEntry> blackBox = new NativeArray<FloraGenomeBlackBoxEntry>(FloraGenomeLSystemConstants.BlackBoxFrameCount, Allocator.TempJob);
            NativeArray<int> cursor = new NativeArray<int>(1, Allocator.TempJob);

            try
            {
                FloraGenomeDTO genome = genomes[genomeIndex];
                seed[0] = new FloraPlantSeedDTO
                {
                    AupCell = new FloraAupCell { X = 0L, Y = 0L, Z = 0L },
                    LocalPosition = new float3(_previewRoot.x, _previewRoot.y, _previewRoot.z),
                    PlantHash = 0xED1708u,
                    SpeciesHash = genome.SpeciesHash,
                    WorldSeed = 0xB07A4E08u,
                    QualityWeightQ8 = 255,
                    RequestedIterations = genome.MaxIterations,
                    ChunkSlot = 0,
                    Reserved0 = 0u
                };

                new IterativeLSystemExpanderJob
                {
                    Genomes = genomes,
                    GenomeIndex = genomeIndex,
                    QualityWeight01 = 1f,
                    ExpandedSymbols = previewWorkspace.ExpandedSymbols,
                    ScratchSymbols = previewWorkspace.ScratchSymbols,
                    Stats = stats
                }.Run();

                new TurtleGraphicsJob
                {
                    Genomes = genomes,
                    PlantSeeds = seed,
                    Symbols = previewWorkspace.ExpandedSymbols,
                    GenomeIndex = genomeIndex,
                    PlantIndex = 0,
                    FrameIndex = (uint)Time.frameCount,
                    QualityWeight01 = 1f,
                    TurtleStack = previewWorkspace.TurtleStack,
                    BranchMatrices = previewWorkspace.BranchMatrices,
                    MatrixWriteOffset = 0,
                    MatrixWriteCapacity = previewWorkspace.BranchMatrices.Length,
                    HazardZones = previewWorkspace.HazardZones,
                    HazardWriteOffset = 0,
                    HazardWriteCapacity = previewWorkspace.HazardZones.Length,
                    BlackBox = blackBox,
                    BlackBoxCursor = cursor,
                    Stats = stats
                }.Run();

                _previewMatrixCount = math.min(math.max(0, stats[0].MatrixCount), previewWorkspace.BranchMatrices.Length);
                _previewActive = true;
                SceneView.RepaintAll();
            }
            finally
            {
                if (cursor.IsCreated)
                    cursor.Dispose();
                if (blackBox.IsCreated)
                    blackBox.Dispose();
                if (stats.IsCreated)
                    stats.Dispose();
                if (seed.IsCreated)
                    seed.Dispose();
            }
        }

        private void DrawPreviewSceneGui(SceneView sceneView)
        {
            if (!_previewActive || !IsPreviewWorkspaceCreated || !_previewBranchMatrices.IsCreated)
                return;
            if (Event.current.type != EventType.Repaint)
                return;
            if (!EnsurePreviewDrawResources())
                return;
            if (!_previewMaterial.SetPass(0))
                return;

            int count = math.min(_previewMatrixCount, 4096);
            for (int i = 0; i < count; i++)
            {
                BranchMatrixDTO dto = _previewBranchMatrices[i];
                UnityEngine.Graphics.DrawMeshNow(_previewSegmentMesh, ToMatrix4x4(dto.Matrix));
            }
        }

        private bool IsPreviewWorkspaceCreated =>
            _previewExpandedSymbols.IsCreated &&
            _previewScratchSymbols.IsCreated &&
            _previewBranchMatrices.IsCreated &&
            _previewHazardZones.IsCreated &&
            _previewTurtleStack.IsCreated;

        private FloraGenomeChunkWorkspace BuildPreviewWorkspaceView()
        {
            return FloraGenomeChunkWorkspace.FromVault(
                _previewExpandedSymbols,
                _previewScratchSymbols,
                _previewBranchMatrices,
                _previewHazardZones,
                _previewTurtleStack);
        }

        private void CreateEditorPreviewWorkspace(
            int symbolCapacity,
            int matrixCapacity,
            int hazardCapacity,
            int turtleStackCapacity,
            Allocator allocator)
        {
            DisposePreviewWorkspace();
            _previewExpandedSymbols = new NativeArray<byte>(math.max(1, symbolCapacity), allocator, NativeArrayOptions.UninitializedMemory);
            _previewScratchSymbols = new NativeArray<byte>(math.max(1, symbolCapacity), allocator, NativeArrayOptions.UninitializedMemory);
            _previewBranchMatrices = new NativeArray<BranchMatrixDTO>(math.max(1, matrixCapacity), allocator, NativeArrayOptions.UninitializedMemory);
            _previewHazardZones = new NativeArray<HazardZoneDTO>(math.max(1, hazardCapacity), allocator, NativeArrayOptions.UninitializedMemory);
            _previewTurtleStack = new NativeArray<TurtleStackFrameDTO>(math.max(1, turtleStackCapacity), allocator, NativeArrayOptions.UninitializedMemory);
        }

        private void DisposePreviewWorkspace()
        {
            if (_previewExpandedSymbols.IsCreated)
                _previewExpandedSymbols.Dispose();
            if (_previewScratchSymbols.IsCreated)
                _previewScratchSymbols.Dispose();
            if (_previewBranchMatrices.IsCreated)
                _previewBranchMatrices.Dispose();
            if (_previewHazardZones.IsCreated)
                _previewHazardZones.Dispose();
            if (_previewTurtleStack.IsCreated)
                _previewTurtleStack.Dispose();
        }

        private bool EnsurePreviewDrawResources()
        {
            if (_previewSegmentMesh == null)
            {
                _previewSegmentMesh = new Mesh
                {
                    name = "SHINOBU_LSystemPreviewSegment",
                    hideFlags = HideFlags.HideAndDontSave
                };
                _previewSegmentMesh.SetVertices(new[]
                {
                    new Vector3(-0.5f, -0.5f, -0.5f),
                    new Vector3(0.5f, -0.5f, -0.5f),
                    new Vector3(0.5f, 0.5f, -0.5f),
                    new Vector3(-0.5f, 0.5f, -0.5f),
                    new Vector3(-0.5f, -0.5f, 0.5f),
                    new Vector3(0.5f, -0.5f, 0.5f),
                    new Vector3(0.5f, 0.5f, 0.5f),
                    new Vector3(-0.5f, 0.5f, 0.5f)
                });
                _previewSegmentMesh.SetTriangles(new[]
                {
                    0, 2, 1, 0, 3, 2,
                    4, 5, 6, 4, 6, 7,
                    0, 1, 5, 0, 5, 4,
                    2, 3, 7, 2, 7, 6,
                    1, 2, 6, 1, 6, 5,
                    3, 0, 4, 3, 4, 7
                }, 0, false);
                _previewSegmentMesh.RecalculateBounds();
            }

            if (_previewMaterial == null)
            {
                Shader shader = Shader.Find("Hidden/Internal-Colored");
                if (shader == null)
                    return false;

                _previewMaterial = new Material(shader)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                _previewMaterial.SetInt(H8ShaderIDs.SrcBlend, (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                _previewMaterial.SetInt(H8ShaderIDs.DstBlend, (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                _previewMaterial.SetInt(H8ShaderIDs.Cull, (int)UnityEngine.Rendering.CullMode.Off);
                _previewMaterial.SetInt(H8ShaderIDs.ZWrite, 0);
                _previewMaterial.SetColor(H8ShaderIDs.Color, new Color(0.26f, 0.95f, 0.78f, 0.62f));
            }

            return _previewMaterial != null;
        }

        private static FixedString32Bytes BuildFixedAxiom(string text)
        {
            FixedString32Bytes axiom = default;
            if (string.IsNullOrEmpty(text))
            {
                axiom.Add((byte)'X');
                return axiom;
            }

            int limit = Mathf.Min(text.Length, FixedString32Bytes.UTF8MaxLengthInBytes);
            for (int i = 0; i < limit; i++)
            {
                char c = text[i];
                if (c <= 127 && c > 32)
                    axiom.Add((byte)c);
            }

            if (axiom.Length == 0)
                axiom.Add((byte)'X');
            return axiom;
        }

        private static Color DecodeColor(uint packed)
        {
            const float inv255 = 1f / 255f;
            return new Color(
                ((packed >> 24) & 0xFFu) * inv255,
                ((packed >> 16) & 0xFFu) * inv255,
                ((packed >> 8) & 0xFFu) * inv255,
                (packed & 0xFFu) * inv255);
        }

        private static uint EncodeColor(Color color)
        {
            uint r = (uint)Mathf.Clamp(Mathf.RoundToInt(color.r * 255f), 0, 255);
            uint g = (uint)Mathf.Clamp(Mathf.RoundToInt(color.g * 255f), 0, 255);
            uint b = (uint)Mathf.Clamp(Mathf.RoundToInt(color.b * 255f), 0, 255);
            uint a = (uint)Mathf.Clamp(Mathf.RoundToInt(color.a * 255f), 0, 255);
            return (r << 24) | (g << 16) | (b << 8) | a;
        }

        private static Vector4 ToVector4(float4 value)
        {
            return new Vector4(value.x, value.y, value.z, value.w);
        }

        private static Matrix4x4 ToMatrix4x4(float4x4 value)
        {
            return new Matrix4x4(
                ToVector4(value.c0),
                ToVector4(value.c1),
                ToVector4(value.c2),
                ToVector4(value.c3));
        }
    }
}
#endif
