// ============================================================================
// HECTON-8 â€” HectonBoidController.cs
// GPU-based Boid System Controller.
//
// ÐžÐ¢Ð’Ð•Ð¢Ð¡Ð¢Ð’Ð•ÐÐÐžÐ¡Ð¢Ð˜:
//   1. Ð˜Ð½Ð¸Ñ†Ð¸Ð°Ð»Ð¸Ð·Ð°Ñ†Ð¸Ñ GraphicsBuffer Ñ Ð½Ð°Ñ‡Ð°Ð»ÑŒÐ½Ñ‹Ð¼Ð¸ Ð¿Ð¾Ð·Ð¸Ñ†Ð¸ÑÐ¼Ð¸ Ñ€Ñ‹Ð±.
//   2. ÐšÐ°Ð¶Ð´Ñ‹Ð¹ ÐºÐ°Ð´Ñ€: Ð¿ÐµÑ€ÐµÐ´Ð°Ñ‡Ð° uniforms â†’ Dispatch â†’ Indirect Draw.
//   3. Frustum Culling: Ð¾Ñ‚ÐºÐ»ÑŽÑ‡ÐµÐ½Ð¸Ðµ Ñ€ÐµÐ½Ð´ÐµÑ€Ð° ÐµÑÐ»Ð¸ ÑÑ‚Ð°Ñ Ð½Ðµ Ð²Ð¸Ð´Ð½Ð°.
//   4. Lifecycle: ÐºÐ¾Ñ€Ñ€ÐµÐºÑ‚Ð½Ñ‹Ð¹ Release Ð±ÑƒÑ„ÐµÑ€Ð¾Ð² Ð¿Ñ€Ð¸ OnDestroy.
//
// ÐÐ Ð¥Ð˜Ð¢Ð•ÐšÐ¢Ð£Ð Ð:
//   â€¢ ITickable â€” Ð¸Ð½Ñ‚ÐµÐ³Ñ€Ð°Ñ†Ð¸Ñ Ñ GameTickManager. ÐÐµÑ‚ MonoBehaviour tick.
//   â€¢ Graphics.RenderMeshIndirect (Unity 6) â€” one GPU-visible draw call.
//   â€¢ Ping-Pong GraphicsBuffer â€” Ð´Ð²Ð° Ð±ÑƒÑ„ÐµÑ€Ð°, swap ÐºÐ°Ð¶Ð´Ñ‹Ð¹ ÐºÐ°Ð´Ñ€, zero race conditions.
//   â€¢ Owner-local runtime Material â€” zero GC per-frame render state.
//
// PING-PONG ARCHITECTURE:
//   ÐšÐ°Ð¶Ð´Ñ‹Ð¹ ÐºÐ°Ð´Ñ€ compute shader Ñ‡Ð¸Ñ‚Ð°ÐµÑ‚ Ð¸Ð· _BoidsBufferRead Ð¸ Ð¿Ð¸ÑˆÐµÑ‚ Ð² _BoidsBufferWrite.
//   ÐŸÐ¾ÑÐ»Ðµ dispatch Ð±ÑƒÑ„ÐµÑ€Ñ‹ Ð»Ð¾Ð³Ð¸Ñ‡ÐµÑÐºÐ¸ Ð¼ÐµÐ½ÑÑŽÑ‚ÑÑ Ð¼ÐµÑÑ‚Ð°Ð¼Ð¸ Ñ‡ÐµÑ€ÐµÐ· _frameIndex % 2.
//   Vertex shader Ð²ÑÐµÐ³Ð´Ð° Ñ‡Ð¸Ñ‚Ð°ÐµÑ‚ Ð¸Ð· Ð±ÑƒÑ„ÐµÑ€Ð°, Ð² ÐºÐ¾Ñ‚Ð¾Ñ€Ñ‹Ð¹ Ñ‚Ð¾Ð»ÑŒÐºÐ¾ Ñ‡Ñ‚Ð¾ Ð·Ð°Ð¿Ð¸ÑÐ°Ð»Ð¸ (writeBuffer).
//   ÐÐ¸ÐºÐ°ÐºÐ¸Ñ… Ð°Ð»Ð»Ð¾ÐºÐ°Ñ†Ð¸Ð¹ â€” Ñ‚Ð¾Ð»ÑŒÐºÐ¾ Ð¿ÐµÑ€ÐµÐ¿Ñ€Ð¸ÑÐ²Ð¾ÐµÐ½Ð¸Ðµ ÑÑÑ‹Ð»Ð¾Ðº Ð½Ð° ÑÑƒÑ‰ÐµÑÑ‚Ð²ÑƒÑŽÑ‰Ð¸Ðµ Ð±ÑƒÑ„ÐµÑ€Ñ‹.
//
// RENDERING:
//   Instanced rendering Ñ‡ÐµÑ€ÐµÐ· StructuredBuffer Ð² vertex shader.
//   ÐšÐ°Ð¶Ð´Ñ‹Ð¹ instance Ñ‡Ð¸Ñ‚Ð°ÐµÑ‚ ÑÐ²Ð¾ÑŽ BoidData Ð¸Ð· Ð±ÑƒÑ„ÐµÑ€Ð° Ð¿Ð¾ SV_InstanceID.
//   Vertex shader: position + LookRotation(velocity) + scale.
//
// 3D DEPTH TRACKING (v2.1):
//   UpdateTarget() ÑÐ»ÐµÐ´ÑƒÐµÑ‚ Ð·Ð° Ð¸Ð³Ñ€Ð¾ÐºÐ¾Ð¼ Ð¿Ð¾ Ð²ÑÐµÐ¼ Ñ‚Ñ€Ñ‘Ð¼ Ð¾ÑÑÐ¼ (X, Y, Z).
//   ÐžÑÑŒ Y Ð¾Ð³Ñ€Ð°Ð½Ð¸Ñ‡ÐµÐ½Ð°: Ð²ÐµÑ€Ñ…Ð½ÑÑ Ð³Ñ€Ð°Ð½Ð¸Ñ†Ð° Ð±Ð¾ÐºÑÐ° (center.y + boundsSize.y)
//   Ð½Ðµ Ð¼Ð¾Ð¶ÐµÑ‚ Ð¿Ñ€ÐµÐ²Ñ‹ÑˆÐ°Ñ‚ÑŒ waterSurfaceY. Ð­Ñ‚Ð¾ Ð³Ð°Ñ€Ð°Ð½Ñ‚Ð¸Ñ€ÑƒÐµÑ‚, Ñ‡Ñ‚Ð¾ ÑÑ‚Ð°Ñ
//   Ð¿Ð¾Ð³Ñ€ÑƒÐ¶Ð°ÐµÑ‚ÑÑ Ð²Ð¼ÐµÑÑ‚Ðµ Ñ Ð¸Ð³Ñ€Ð¾ÐºÐ¾Ð¼, Ð½Ð¾ Ð½Ð¸ÐºÐ¾Ð³Ð´Ð° Ð½Ðµ Ð¿Ñ€Ð¾Ð±Ð¸Ð²Ð°ÐµÑ‚ Ð¿Ð¾Ð²ÐµÑ€Ñ…Ð½Ð¾ÑÑ‚ÑŒ.
//
// GPU MEMORY SAFETY (v2.2):
//   â€¢ InitializeBuffers() Ð²Ñ‹Ð·Ñ‹Ð²Ð°ÐµÑ‚ Release() Ð½Ð° ÑÑ‚Ð°Ñ€Ñ‹Ðµ Ð±ÑƒÑ„ÐµÑ€Ñ‹ Ð¿ÐµÑ€ÐµÐ´
//     ÑÐ¾Ð·Ð´Ð°Ð½Ð¸ÐµÐ¼ Ð½Ð¾Ð²Ñ‹Ñ…. ÐŸÑ€ÐµÐ´Ð¾Ñ‚Ð²Ñ€Ð°Ñ‰Ð°ÐµÑ‚ ÑƒÑ‚ÐµÑ‡ÐºÑƒ VRAM Ð¿Ñ€Ð¸ Ð¿Ð¾Ð²Ñ‚Ð¾Ñ€Ð½Ð¾Ð¼ Ð²Ñ‹Ð·Ð¾Ð²Ðµ.
//   â€¢ _fallbackHeightMap ÑÐ¾Ð·Ð´Ð°Ñ‘Ñ‚ÑÑ Ð¢ÐžÐ›Ð¬ÐšÐž ÐµÑÐ»Ð¸ == null. ÐŸÐµÑ€ÐµÐ¸ÑÐ¿Ð¾Ð»ÑŒÐ·ÑƒÐµÑ‚ÑÑ
//     Ð¿Ñ€Ð¸ Ð¿Ð¾Ð²Ñ‚Ð¾Ñ€Ð½Ñ‹Ñ… Ð²Ñ‹Ð·Ð¾Ð²Ð°Ñ…. Ð£Ð½Ð¸Ñ‡Ñ‚Ð¾Ð¶Ð°ÐµÑ‚ÑÑ Ñ‚Ð¾Ð»ÑŒÐºÐ¾ Ð² ReleaseBuffers().
//   â€¢ Awake() Ð·Ð°Ñ‰Ð¸Ñ‰Ñ‘Ð½ Ð¾Ñ‚ double-init: ÐµÑÐ»Ð¸ _initialized â€” ÑÐ½Ð°Ñ‡Ð°Ð»Ð° Release.
//   â€¢ NativeArray<byte> Ð´Ð»Ñ Ð·Ð°Ð¿Ð¾Ð»Ð½ÐµÐ½Ð¸Ñ fallback Ñ‚ÐµÐºÑÑ‚ÑƒÑ€Ñ‹ (zero managed alloc).
//
// PERFORMANCE Ð½Ð° MX350 (Ñ†ÐµÐ»ÐµÐ²Ð¾Ðµ Ð¶ÐµÐ»ÐµÐ·Ð¾):
//   5000 boids: Compute ~0.5ms, Draw ~0.3ms = ~0.8ms total.
//   Instanced draw: 1 draw call (vs 5000 GameObjects = 5000 calls).
//   CPU: ~0.01ms (uniform upload + dispatch + draw).
//
// HEIGHTMAP INTEGRATION:
//   Terrain heightmap Ð¿ÐµÑ€ÐµÐ´Ð°Ñ‘Ñ‚ÑÑ ÐºÐ°Ðº Texture2D.
//   ÐœÐ¾Ð¶Ð½Ð¾ Ð·Ð°Ñ…Ð²Ð°Ñ‚Ð¸Ñ‚ÑŒ Ñ‡ÐµÑ€ÐµÐ· Terrain.terrainData.heightmapTexture
//   Ð¸Ð»Ð¸ Ð¾Ñ‚Ñ€Ð¸ÑÐ¾Ð²Ð°Ñ‚ÑŒ Ñ‡ÐµÑ€ÐµÐ· Camera.RenderTexture (Ð´Ð»Ñ MapMagic multi-tile).
//
// ZERO GC:
//   â€¢ Ð’ÑÐµ Ð±ÑƒÑ„ÐµÑ€Ñ‹ Ð°Ð»Ð»Ð¾Ñ†Ð¸Ñ€Ð¾Ð²Ð°Ð½Ñ‹ Ð² Awake, Ð¾ÑÐ²Ð¾Ð±Ð¾Ð¶Ð´ÐµÐ½Ñ‹ Ð² OnDestroy.
//   â€¢ BoidData â€” struct (blittable, no GC pressure).
//   â€¢ Owner-local material SetBuffer/SetFloat â€” zero GC after cold material copy.
//   â€¢ ComputeShader.SetFloat/SetVector/SetInt â€” zero GC.
//   â€¢ Graphics.RenderMeshIndirect â€” zero GC.
//   â€¢ GeometryUtility.TestPlanesAABB â€” zero GC (struct arrays).
//   â€¢ Ping-Pong swap â€” integer increment, zero allocation.
// ============================================================================

using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Gameplay;
using Hecton8.Optimization;
using Hecton8.World;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.AI.GPU
{
    [DisallowMultipleComponent]
    public sealed class HectonBoidController : MonoBehaviour, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        // â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• 
        //  BOID DATA â€” must match compute shader struct exactly
        // â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• 

        /// <summary>Stride of BoidData in bytes. Must match GPU struct.</summary>
        private const int BoidStride = 32; // 8 Ã— sizeof(float)
        private const int BoidPhysicsStride = 24; // 6 Ã— sizeof(float) â€” position + velocity only (SoA hot path)
        private const int SpatialGridMaxAxisResolution = 32;
        private const int SpatialGridMaxCellCount = SpatialGridMaxAxisResolution * SpatialGridMaxAxisResolution * SpatialGridMaxAxisResolution;
        private const int SpatialGridMaxBoidsPerCell = 32;
        private const int SpatialGridCounterStride = 4;
        private const int SpatialGridCellEntryStride = 4;
        private const uint ThreadGroupPortableMaxSize = 256u;
        private const int MaxPredatorRuntimePositions = 16;
        private const int MaxAcousticPingSignalsPerFrame = 16;
        private const float AcousticPingDecayMetersPerSecond = 34f;
        private const float AcousticPingMinLifetimeSeconds = 0.15f;
        private const float AcousticPingMaxLifetimeSeconds = 3.5f;
        private const float DefaultBoidCullingRadiusScale = 2.25f;
        private const float BoidClockMaxSeconds = 16777215f;
        private const int BoidBlackBoxFrameCount = 300;
        private const int BoidBlackBoxStride = 128;
        private const uint BoidBlackBoxDumpMagic = 0x424F4944u;
        private const uint BoidBlackBoxDumpVersion = 1u;
        private const BufferID BoidBlackBoxBufferId = BufferID.HectonBoidController_BoidBlackBoxBufferId;
        private const string BoidBlackBoxDumpRelativePath = "Docs/AgentLogs/Dump_1301_Boids.bin";
        private const float RuntimeVectorComponentLimitMeters = 100000f;
        private const float MaxBoidWeight = 64f;
        private const float MaxBoidRadiusMeters = 4096f;
        private const float MaxBoidSpeedMetersPerSecond = 512f;
        private const float MaxHeightScaleMeters = 10000f;

        private const uint BoidBlackBoxFlagInitialized = 1u << 0;
        private const uint BoidBlackBoxFlagSimulated = 1u << 1;
        private const uint BoidBlackBoxFlagBuffersReady = 1u << 2;
        private const uint BoidBlackBoxFlagVisibleIndices = 1u << 3;
        private const uint BoidBlackBoxFaultInvalidDeltaTime = 1u << 16;
        private const uint BoidBlackBoxFaultInvalidTarget = 1u << 17;
        private const uint BoidBlackBoxFaultInvalidBounds = 1u << 18;
        private const uint BoidBlackBoxFaultInvalidGrid = 1u << 19;
        private const uint BoidBlackBoxFaultInvalidClock = 1u << 20;
        private const uint BoidBlackBoxFaultInvalidPopulation = 1u << 21;
        private const uint BoidBlackBoxFaultInvalidAcoustic = 1u << 22;
        private const uint BoidBlackBoxFaultMissingBuffers = 1u << 23;
        private const uint BoidBlackBoxFaultMask = 0xFFFF0000u;

        /// <summary>
        /// GPU-compatible boid data structure.
        /// 32 bytes total (8 floats Ã— 4 bytes).
        /// Matches HLSL struct BoidData layout exactly.
        /// Blittable â€” no GC, direct GPU upload.
        /// </summary>
        [StructLayout(LayoutKind.Explicit, Size = BoidStride)]
        private struct BoidData
        {
            [FieldOffset(0)] public Vector3 position;  // 12 bytes
            [FieldOffset(12)] public Vector3 velocity;  // 12 bytes
            [FieldOffset(24)] public float   panic;     // 4 bytes
            [FieldOffset(28)] public uint    stateFlags;// 4 bytes
            // TOTAL: 32 bytes
        }

        /// <summary>
        /// SoA hot-path physics struct (24 bytes). Matches first 24 bytes of BoidData.
        /// Used by AccumulateSpatialNeighbor, BuildSpatialGrid, CullVisibleBoids â€” kernels
        /// that only need position+velocity and would waste 8 bytes per fetch from BoidData.
        /// Saves ~3 MB/frame in VRAM reads at N=5000 on MX350 (80 GB/s).
        /// </summary>
        [StructLayout(LayoutKind.Explicit, Size = BoidPhysicsStride)]
        private struct BoidPhysicsData
        {
            [FieldOffset(0)]  public Vector3 position; // 12 bytes
            [FieldOffset(12)] public Vector3 velocity; // 12 bytes
            // TOTAL: 24 bytes
        }

        [StructLayout(LayoutKind.Explicit, Size = BoidBlackBoxStride)]
        private struct BoidBlackBoxEntry
        {
            [FieldOffset(0)] public uint Frame;
            [FieldOffset(4)] public uint StateHash;
            [FieldOffset(8)] public uint Flags;
            [FieldOffset(12)] public int BoidCount;
            [FieldOffset(16)] public Vector3 TargetPosition;
            [FieldOffset(28)] public float DeltaTime;
            [FieldOffset(32)] public Vector3 BoundsCenter;
            [FieldOffset(44)] public float BoidClockSeconds;
            [FieldOffset(48)] public Vector3 SpatialGridOrigin;
            [FieldOffset(60)] public float SpatialGridCellSize;
            [FieldOffset(64)] public int SpatialGridResolutionX;
            [FieldOffset(68)] public int SpatialGridResolutionY;
            [FieldOffset(72)] public int SpatialGridResolutionZ;
            [FieldOffset(76)] public int DispatchGroupCount;
            [FieldOffset(80)] public int PredatorCount;
            [FieldOffset(84)] public int FoveatedTier;
            [FieldOffset(88)] public float GlobalQualityWeight;
            [FieldOffset(92)] public float SocialLodWeight;
            [FieldOffset(96)] public Vector4 AcousticPingParams;
            [FieldOffset(112)] public Vector4 AcousticPingRuntimeRadius;
        }

        // â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• 
        //  INSPECTOR â€” CORE
        // â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• 

        [Header("â”€â”€ Core References â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("Compute Shader Ð´Ð»Ñ  Ñ Ð¸Ð¼ÑƒÐ»Ñ Ñ†Ð¸Ð¸ Ð±Ð¾Ð¹Ð´Ð¾Ð².")]
        [SerializeField] private ComputeShader boidShader;

        [Tooltip("Mesh Ð¾Ð´Ð½Ð¾Ð¹ Ñ€Ñ‹Ð±Ñ‹ (low-poly, ~100-300 tris).")]
        [SerializeField] private Mesh fishMesh;

        [Tooltip("Material Ð´Ð»Ñ  instanced Ñ€ÐµÐ½Ð´ÐµÑ€Ð°. Ð”Ð¾Ð»Ð¶ÐµÐ½ Ð¿Ð¾Ð´Ð´ÐµÑ€Ð¶Ð¸Ð²Ð°Ñ‚ÑŒ " +
                 "StructuredBuffer<BoidData> Ð² vertex shader.")]
        [SerializeField] private Material fishMaterial;

        [Header("â”€â”€ Population â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("ÐšÐ¾Ð»Ð¸Ñ‡ÐµÑ Ñ‚Ð²Ð¾ Ñ€Ñ‹Ð± Ð² Ñ Ñ‚Ð°Ðµ. Max recommended: 5000.")]
        [Range(64, 8192)]
        [SerializeField] private int boidCount = 2000;
        private bool _registeredToTickManager;

        // â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• 
        //  INSPECTOR â€” BOID RULES
        // â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• 

        [Header("â”€â”€ Boid Weights â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [SerializeField] private float separationWeight = 2.5f;
        [SerializeField] private float alignmentWeight  = 1.0f;
        [SerializeField] private float cohesionWeight   = 1.0f;
        [SerializeField] private float targetWeight     = 0.5f;
        [SerializeField] private float obstacleWeight   = 3.0f;
        [SerializeField] private float boundsWeight     = 1.5f;

        [Header("â”€â”€ Boid Radii â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("Ð Ð°Ð´Ð¸ÑƒÑ  Ð²Ð¾Ñ Ð¿Ñ€Ð¸Ñ Ñ‚Ð¸Ñ  (alignment + cohesion).")]
        [SerializeField] private float perceptionRadius    = 5f;
        [Tooltip("Ð Ð°Ð´Ð¸ÑƒÑ  Ñ€Ð°Ð·Ð´ÐµÐ»ÐµÐ½Ð¸Ñ  (separation). Ð”Ð¾Ð»Ð¶ÐµÐ½ Ð±Ñ‹Ñ‚ÑŒ < perception.")]
        [SerializeField] private float separationRadius    = 2f;
        [Tooltip("Ð’Ñ‹Ñ Ð¾Ñ‚Ð° Ð½Ð°Ð´ Ð´Ð½Ð¾Ð¼, Ñ  ÐºÐ¾Ñ‚Ð¾Ñ€Ð¾Ð¹ Ð½Ð°Ñ‡Ð¸Ð½Ð°ÐµÑ‚Ñ Ñ  ÑƒÐºÐ»Ð¾Ð½ÐµÐ½Ð¸Ðµ.")]
        [SerializeField] private float obstacleAvoidRadius = 5f;

        [Header("â”€â”€ Speed â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [SerializeField] private float minSpeed = 2f;
        [SerializeField] private float maxSpeed = 6f;

        // â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• 
        //  INSPECTOR â€” SPAWN ZONE
        // â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• 

        [Header("â”€â”€ Simulation Zone â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("Ð¦ÐµÐ½Ñ‚Ñ€ Ð·Ð¾Ð½Ñ‹ Ñ Ð¸Ð¼ÑƒÐ»Ñ Ñ†Ð¸Ð¸ (Ð¼Ð¸Ñ€Ð¾Ð²Ñ‹Ðµ ÐºÐ¾Ð¾Ñ€Ð´Ð¸Ð½Ð°Ñ‚Ñ‹).")]
        [SerializeField] private Vector3 boundsCenter = Vector3.zero;
        [Tooltip("ÐŸÐ¾Ð»ÑƒÑ€Ð°Ð·Ð¼ÐµÑ€Ñ‹ Ð·Ð¾Ð½Ñ‹ Ñ Ð¸Ð¼ÑƒÐ»Ñ Ñ†Ð¸Ð¸.")]
        [SerializeField] private Vector3 boundsSize   = new Vector3(100f, 30f, 100f);

        [Tooltip("Ð Ð°Ð´Ð¸ÑƒÑ  Ð½Ð°Ñ‡Ð°Ð»ÑŒÐ½Ð¾Ð³Ð¾ Ñ Ð¿Ð°Ð²Ð½Ð° Ð²Ð¾ÐºÑ€ÑƒÐ³ Ñ†ÐµÐ½Ñ‚Ñ€Ð°.")]
        [SerializeField] private float spawnRadius = 30f;

        // â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• 
        //  INSPECTOR â€” HEIGHTMAP
        // â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• 

        [Header("â”€â”€ Heightmap (Terrain) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("Ð¢ÐµÐºÑ Ñ‚ÑƒÑ€Ð° Ð²Ñ‹Ñ Ð¾Ñ‚ Ð¸Ð· MapMagic/Terrain. " +
                 "R-ÐºÐ°Ð½Ð°Ð» = Ð½Ð¾Ñ€Ð¼Ð°Ð»Ð¸Ð·Ð¾Ð²Ð°Ð½Ð½Ð°Ñ  Ð²Ñ‹Ñ Ð¾Ñ‚Ð° [0..1]. " +
                 "Ð•Ñ Ð»Ð¸ null â€” obstacle avoidance Ð¸Ñ Ð¿Ð¾Ð»ÑŒÐ·ÑƒÐµÑ‚ flat plane.")]
        [SerializeField] private Texture2D heightMap;

        [Tooltip("Authored flat R8 height texture used when no terrain heightmap is available. Runtime texture synthesis is forbidden.")]
        [SerializeField] private Texture2D neutralHeightMap;

        [Tooltip("ÐœÐ¸Ñ€Ð¾Ð²Ð°Ñ  Ð¿Ð¾Ð·Ð¸Ñ†Ð¸Ñ  Ð½Ð°Ñ‡Ð°Ð»Ð° Ñ‚ÐµÑ€Ñ€ÐµÐ¹Ð½Ð° (XZ).")]
        [SerializeField] private Vector2 worldOffset = Vector2.zero;

        [Tooltip("ÐœÐ¸Ñ€Ð¾Ð²Ð¾Ð¹ Ñ€Ð°Ð·Ð¼ÐµÑ€ Ñ‚ÐµÑ€Ñ€ÐµÐ¹Ð½Ð° (XZ).")]
        [SerializeField] private Vector2 worldSize = new Vector2(1024f, 1024f);

        [Tooltip("ÐœÐ°Ñ ÑˆÑ‚Ð°Ð± Ð²Ñ‹Ñ Ð¾Ñ‚Ñ‹ Ñ‚ÐµÑ€Ñ€ÐµÐ¹Ð½Ð° (Ð¼Ð°ÐºÑ Ð¸Ð¼Ð°Ð»ÑŒÐ½Ð°Ñ  Y).")]
        [SerializeField] private float heightScale = 100f;

        [Tooltip("Ð£Ñ€Ð¾Ð²ÐµÐ½ÑŒ Ð¿Ð¾Ð²ÐµÑ€Ñ…Ð½Ð¾Ñ Ñ‚Ð¸ Ð²Ð¾Ð´Ñ‹ (Ð¼Ð¸Ñ€Ð¾Ð²Ð°Ñ  Y).")]
        [SerializeField] private float waterSurfaceY = 0f;

        [Header("GPU Ecosystem Inputs")]
        [SerializeField] private bool enableVoxelSdfAvoidance = true;
        [SerializeField] private HectonCaveVoxelLightingVolume caveSdfOverride;
        [SerializeField] private float voxelSdfWeight = 1.35f;
        [Tooltip("Authored white SDF Texture3D used when cave SDF is unavailable. Runtime Texture3D fallback generation is forbidden.")]
        [SerializeField] private Texture3D neutralCaveSdfTexture;
        [SerializeField] private bool enableAbyssalFlowAdvection = true;
        [SerializeField] private float abyssalFlowWeight = 0.35f;
        [Tooltip("Authored zero-flow Texture3D used when abyssal flow is unavailable. Runtime Texture3D fallback generation is forbidden.")]
        [SerializeField] private Texture3D neutralAbyssalFlowTexture;
        [SerializeField] private float predatorPanicRadius = 18f;
        [SerializeField] private float predatorEvasionWeight = 1f;
        [SerializeField] private float acousticPingShockwaveWeight = 1f;
        [SerializeField] private float panicDecayPerSecond = 2.5f;
        [SerializeField] private float panicAccelerationThreshold = 14f;

        // â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• 
        //  INSPECTOR â€” RENDERING
        // â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• 

        [Header("â”€â”€ Rendering â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("ÐœÐ°Ñ ÑˆÑ‚Ð°Ð± Ð¼Ð¾Ð´ÐµÐ»Ð¸ Ñ€Ñ‹Ð±Ñ‹ (uniform).")]
        [SerializeField] private float fishScale = 0.3f;

        [Tooltip("Rendering layer mask.")]
        [SerializeField] private int renderingLayerMask = 1;

        [Tooltip("Shadow casting mode for instanced fish.")]
        [SerializeField] private ShadowCastingMode shadowMode = ShadowCastingMode.Off;

        // â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• 
        //  INSPECTOR â€” DIAGNOSTICS
        // â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• 

        [Header("â”€â”€ Diagnostics â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [SerializeField] private bool  _debugIsVisible;
        [SerializeField] private float _debugComputeMs;
        [SerializeField] private int   _debugDispatchGroups;

        // â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• 
        //  COMPUTE SHADER PROPERTY IDs â€” cached, zero GC
        // â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• â• 

        private static class ShaderProps
        {
            // â”€â”€ Buffers (Compute Shader â€” Ping-Pong) â”€â”€
            public static readonly int BoidsBufferRead   = Shader.PropertyToID("_BoidsBufferRead");
            public static readonly int BoidsBufferWrite  = Shader.PropertyToID("_BoidsBufferWrite");
            // SoA physics-only buffers (24B, position+velocity only, for hot-path kernels)
            public static readonly int BoidsPhysicsRead  = Shader.PropertyToID("_BoidsPhysicsRead");
            public static readonly int BoidsPhysicsWrite = Shader.PropertyToID("_BoidsPhysicsWrite");
            public static readonly int SpatialGridCounts = Shader.PropertyToID("_SpatialGridCounts");
            public static readonly int BoidCellKeyValues  = Shader.PropertyToID("_BoidCellKeyValues");
            public static readonly int SpatialGridOffsets = Shader.PropertyToID("_SpatialGridOffsets");
            public static readonly int BitonicBlock       = Shader.PropertyToID("_BitonicBlock");
            public static readonly int BitonicStep        = Shader.PropertyToID("_BitonicStep");

            // â”€â”€ Buffer (Material / Vertex Shader) â”€â”€
            public static readonly int BoidsBuffer = Shader.PropertyToID("_BoidsBuffer");
            public static readonly int VisibleBoidIndices = Shader.PropertyToID("_VisibleBoidIndices");
            public static readonly int BoidUseVisibleIndices = Shader.PropertyToID("_BoidUseVisibleIndices");

            // â”€â”€ Simulation â”€â”€
            public static readonly int BoidCount = Shader.PropertyToID("_BoidCount");
            public static readonly int DeltaTime = Shader.PropertyToID("_DeltaTime");
            public static readonly int SpatialGridOrigin = Shader.PropertyToID("_SpatialGridOrigin");
            public static readonly int SpatialGridResolution = Shader.PropertyToID("_SpatialGridResolution");
            public static readonly int SpatialGridCellSize = Shader.PropertyToID("_SpatialGridCellSize");
            public static readonly int SpatialGridMaxBoidsPerCell = Shader.PropertyToID("_SpatialGridMaxBoidsPerCell");
            public static readonly int BoidMathLodMode = Shader.PropertyToID("_BoidMathLodMode");
            public static readonly int VisibleIndirectArgs = Shader.PropertyToID("_VisibleIndirectArgs");
            public static readonly int CameraFrustumPlanes = Shader.PropertyToID("_CameraFrustumPlanes");
            public static readonly int BoidCullingRadius = Shader.PropertyToID("_BoidCullingRadius");
            public static readonly int GpuFrustumCullingActive = Shader.PropertyToID("_GpuFrustumCullingActive");

            // â”€â”€ Weights â”€â”€
            public static readonly int SeparationWeight = Shader.PropertyToID("_SeparationWeight");
            public static readonly int AlignmentWeight  = Shader.PropertyToID("_AlignmentWeight");
            public static readonly int CohesionWeight   = Shader.PropertyToID("_CohesionWeight");
            public static readonly int TargetWeight     = Shader.PropertyToID("_TargetWeight");
            public static readonly int ObstacleWeight   = Shader.PropertyToID("_ObstacleWeight");
            public static readonly int BoundsWeight     = Shader.PropertyToID("_BoundsWeight");

            // â”€â”€ Radii â”€â”€
            public static readonly int PerceptionRadius    = Shader.PropertyToID("_PerceptionRadius");
            public static readonly int SeparationRadius    = Shader.PropertyToID("_SeparationRadius");
            public static readonly int ObstacleAvoidRadius = Shader.PropertyToID("_ObstacleAvoidRadius");

            // â”€â”€ Speed â”€â”€
            public static readonly int MinSpeed = Shader.PropertyToID("_MinSpeed");
            public static readonly int MaxSpeed = Shader.PropertyToID("_MaxSpeed");

            // â”€â”€ Target â”€â”€
            public static readonly int TargetPosition = Shader.PropertyToID("_TargetPosition");

            // â”€â”€ Bounds â”€â”€
            public static readonly int BoundsCenter = Shader.PropertyToID("_BoundsCenter");
            public static readonly int BoundsSize   = Shader.PropertyToID("_BoundsSize");

            // â”€â”€ Heightmap â”€â”€
            public static readonly int HeightMap       = Shader.PropertyToID("_HeightMap");
            public static readonly int WorldOffset     = Shader.PropertyToID("_WorldOffset");
            public static readonly int WorldSize       = Shader.PropertyToID("_WorldSize");
            public static readonly int HeightScaleProp = Shader.PropertyToID("_HeightScale");
            public static readonly int WaterSurfaceY   = Shader.PropertyToID("_WaterSurfaceY");

            public static readonly int CaveVoxelSdfTex = Shader.PropertyToID("_HectonCaveVoxelSdfTex");
            public static readonly int CaveVoxelWorldToLocal = Shader.PropertyToID("_HectonCaveVoxelWorldToLocal");
            public static readonly int CaveVoxelHalfExtents = Shader.PropertyToID("_HectonCaveVoxelHalfExtents");
            public static readonly int CaveVoxelInvDoubleHalfExtents = Shader.PropertyToID("_HectonCaveVoxelInvDoubleHalfExtents");
            public static readonly int CaveVoxelActive = Shader.PropertyToID("_HectonCaveVoxelActive");
            public static readonly int CaveVoxelWeight = Shader.PropertyToID("_HectonCaveVoxelWeight");

            public static readonly int AbyssalFlowFieldResult = Shader.PropertyToID("_AbyssalFlowFieldResult");
            public static readonly int AbyssalFlowFieldTexture = Shader.PropertyToID("_AbyssalFlowFieldTexture");
            public static readonly int AbyssalGridResolution = Shader.PropertyToID("_AbyssalGridResolution");
            public static readonly int AbyssalFlowCenter = Shader.PropertyToID("_AbyssalFlowCenter");
            public static readonly int AbyssalFlowSpacing = Shader.PropertyToID("_AbyssalFlowSpacing");
            public static readonly int AbyssalFlowActive = Shader.PropertyToID("_AbyssalFlowActive");
            public static readonly int AbyssalFlowWeight = Shader.PropertyToID("_AbyssalFlowWeight");

            public static readonly int PredatorRuntimePositions = Shader.PropertyToID("_PredatorRuntimePositions");
            public static readonly int PredatorCount = Shader.PropertyToID("_PredatorCount");
            public static readonly int PredatorPanicRadiusSq = Shader.PropertyToID("_PredatorPanicRadiusSq");
            public static readonly int PredatorWeight = Shader.PropertyToID("_PredatorWeight");
            public static readonly int AcousticPingRuntimeRadius = Shader.PropertyToID("_AcousticPingRuntimeRadius");
            public static readonly int AcousticPingParams = Shader.PropertyToID("_AcousticPingParams");
            public static readonly int PanicDecay = Shader.PropertyToID("_PanicDecay");
            public static readonly int PanicAccelerationThresholdSq = Shader.PropertyToID("_PanicAccelerationThresholdSq");
            public static readonly int FoveatedVatTimeScale = Shader.PropertyToID("_H8FoveatedVatTimeScale");
            public static readonly int FishScale = Shader.PropertyToID("_FishScale");
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  GPU BUFFERS â€” PING-PONG
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>
        /// Ping-Pong buffer A. On even frames: Read. On odd frames: Write.
        /// Created in InitializeBuffers, released in ReleaseBuffers.
        /// </summary>
        private GraphicsBuffer _boidsBufferA;

        /// <summary>
        /// Ping-Pong buffer B. On even frames: Write. On odd frames: Read.
        /// Created in InitializeBuffers, released in ReleaseBuffers.
        /// </summary>
        private GraphicsBuffer _boidsBufferB;
        /// <summary>SoA physics ping-A: 24B position+velocity. Even frames: Read. Odd frames: Write.</summary>
        private GraphicsBuffer _boidsPhysicsBufferA;
        /// <summary>SoA physics ping-B: 24B position+velocity. Even frames: Write. Odd frames: Read.</summary>
        private GraphicsBuffer _boidsPhysicsBufferB;
        private GraphicsBuffer _spatialGridCountBuffer;
        private GraphicsBuffer _boidCellKeyValuesBuffer;
        private GraphicsBuffer _spatialGridOffsetsBuffer;
        private GraphicsBuffer _fallbackFlowFieldBuffer;
        private GraphicsBuffer _visibleBoidIndexBuffer;
        private GraphicsBuffer _visibleIndirectArgsBuffer;
        private GraphicsBuffer _boidUploadStagingBuffer;
        private GraphicsBuffer _visibleIndirectArgsUploadBuffer;
        private readonly GraphicsBuffer.IndirectDrawIndexedArgs[] _visibleIndirectArgsUpload = new GraphicsBuffer.IndirectDrawIndexedArgs[1]; // COLD ALLOC: IndirectDrawIndexedArgs[1] - boid indirect draw static args staging - owner: HectonBoidController
        private BoidData[] _spawnUploadBuffer;
        private BoidPhysicsData[] _spawnPhysicsUploadBuffer;
        private Texture3D _fallbackVoxelSdfTexture;
        private Texture3D _fallbackAbyssalFlowTexture;
        private readonly Vector4[] _cameraFrustumPlaneUpload = new Vector4[6];
        private Mesh _indirectArgsMesh;
        private readonly Vector4[] _predatorRuntimePositions = new Vector4[MaxPredatorRuntimePositions];
        private int _predatorRuntimePositionCount;
        private Vector4 _activeAcousticPingRuntimeRadius;
        private Vector4 _activeAcousticPingParams;

        /// <summary>
        /// Frame counter for Ping-Pong buffer swap.
        /// Incremented each Tick. Used as: _frameIndex % 2.
        /// Zero allocation swap â€” only integer arithmetic.
        /// </summary>
        private int _frameIndex;
        private float _boidClockSeconds;
        private Vector3 _spatialGridOrigin;
        private Vector3Int _spatialGridResolution = Vector3Int.one;
        private float _spatialGridCellSize = 1f;

        /// <summary>
        /// GPU draw state.
        /// Direct instance count is issued from CPU-side render params.
        /// Ð¡Ð¾Ð·Ð´Ð°Ñ‘Ñ‚ÑÑ Ð¾Ð´Ð¸Ð½ Ñ€Ð°Ð·. ÐÐ¸ÐºÐ¾Ð³Ð´Ð° Ð½Ðµ Ð¼ÐµÐ½ÑÐµÑ‚ÑÑ (ÐºÑ€Ð¾Ð¼Ðµ OnValidate).
        /// </summary>

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  CACHED STATE
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>Kernel index for CSMain.</summary>
        private int _kernelCSMain;
        private int _kernelClearSpatialGrid;
        private int _kernelBuildSpatialGrid;
        private int _kernelBitonicSort;
        private int _kernelComputeCellOffsets;
        private int _kernelClearVisibleIndirectArgs;
        private int _kernelCullVisibleBoids;

        /// <summary>Thread group size X (read from shader).</summary>
        private int _threadGroupSizeX;
        private int _clearSpatialGridThreadGroupSizeX;
        private int _buildSpatialGridThreadGroupSizeX;
        private int _computeCellOffsetsThreadGroupSizeX;
        private int _clearVisibleIndirectArgsThreadGroupSizeX;
        private int _cullVisibleBoidsThreadGroupSizeX;

        /// <summary>Number of dispatch groups = ceil(boidCount / threadGroupSize).</summary>
        private int _dispatchGroupCount;
        private int _clearSpatialGridGroupCount;
        private int _buildSpatialGridGroupCount;
        private int _computeCellOffsetsGroupCount;
        private int _clearVisibleIndirectArgsGroupCount;
        private int _cullVisibleBoidsGroupCount;

        /// <summary>ÐšÑÑˆÐ¸Ñ€Ð¾Ð²Ð°Ð½Ð½Ñ‹Ð¹ Transform Ð¸Ð³Ñ€Ð¾ÐºÐ°.</summary>
        private Transform _playerTransform;
        private IPlayerRuntimeContext _playerRuntimeContext;

        /// <summary>Target position (follows player).</summary>
        private Vector3 _targetPosition;

        /// <summary>
        /// Pre-allocated Plane[6] for frustum culling.
        /// GeometryUtility.CalculateFrustumPlanes fills this array.
        /// Reused every frame â€” zero GC.
        /// </summary>
        private readonly Plane[] _frustumPlanes = new Plane[6];

        /// <summary>
        /// AABB of the simulation zone for frustum culling.
        /// Computed once from boundsCenter + boundsSize.
        /// </summary>
        private Bounds _simulationBounds;

        /// <summary>Owner-local indirect draw properties. Bound through RenderParams.matProps; authored fishMaterial is never cloned or mutated.</summary>
        private MaterialPropertyBlock _renderMaterialProperties; // COLD ALLOC: MaterialPropertyBlock[1] - boid indirect draw payload - owner: HectonBoidController

        /// <summary>ÐšÑÑˆÐ¸Ñ€Ð¾Ð²Ð°Ð½Ð½Ð°Ñ ÐºÐ°Ð¼ÐµÑ€Ð°.</summary>
        private Camera _mainCamera;
        private IFoveatedSimulationDirector _foveatedSimulationDirector;
        private IAbyssalFlowGpuReadModel _fluidRuntime;
        private IDataVault _dataVault;
        private VaultGenerationHandle<BoidBlackBoxEntry> _boidBlackBoxHandle;
        private FoveatedSimulationTier _foveatedSimulationTier = FoveatedSimulationTier.Active;
        private bool _hotSwapListenerRegistered;
        private int _boidBlackBoxCursor;
        private int _boidBlackBoxWritten;
        private bool _boidBlackBoxDumped;

        /// <summary>Is system initialized and ready.</summary>
        private bool _initialized;

        /// <summary>
        /// RenderParams for Graphics.RenderMeshIndirect (Unity 6).
        /// Ð¡Ð¾Ð·Ð´Ð°Ñ‘Ñ‚ÑÑ Ð¾Ð´Ð¸Ð½ Ñ€Ð°Ð·.
        /// </summary>
        private RenderParams _renderParams;

        /// <summary>
        /// Fallback heightmap (black = height 0, flat plane) if none assigned.
        /// Created once, reused across InitializeBuffers() calls.
        /// Destroyed only in ReleaseBuffers().
        /// </summary>
        private Texture2D _fallbackHeightMap;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  LIFECYCLE
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>
        /// Initialization entry point.
        ///
        /// v2.2 Patch: Ð”Ð¾Ð±Ð°Ð²Ð»ÐµÐ½Ð° Ð·Ð°Ñ‰Ð¸Ñ‚Ð° Ð¾Ñ‚ Ð¿Ð¾Ð²Ñ‚Ð¾Ñ€Ð½Ð¾Ð³Ð¾ Ð²Ñ‹Ð·Ð¾Ð²Ð° Ñ‡ÐµÑ€ÐµÐ· _initialized.
        /// Ð•ÑÐ»Ð¸ Awake Ð²Ñ‹Ð·Ñ‹Ð²Ð°ÐµÑ‚ÑÑ Ð¿Ð¾Ð²Ñ‚Ð¾Ñ€Ð½Ð¾ (edge case: ÑÐºÑ€Ð¸Ð¿Ñ‚ Ð¿ÐµÑ€ÐµÑÐ¾Ð·Ð´Ð°Ð½
        /// Ñ‡ÐµÑ€ÐµÐ· Reset Ð² Inspector, Ð¸Ð»Ð¸ Ð¾ÑˆÐ¸Ð±ÐºÐ° Ð² Ð½Ð°ÑÐ»ÐµÐ´Ð½Ð¸ÐºÐµ), ÑÑ‚Ð°Ñ€Ñ‹Ðµ
        /// GPU-Ñ€ÐµÑÑƒÑ€ÑÑ‹ ÐºÐ¾Ñ€Ñ€ÐµÐºÑ‚Ð½Ð¾ Ð¾ÑÐ²Ð¾Ð±Ð¾Ð¶Ð´Ð°ÑŽÑ‚ÑÑ Ð¿ÐµÑ€ÐµÐ´ ÑÐ¾Ð·Ð´Ð°Ð½Ð¸ÐµÐ¼ Ð½Ð¾Ð²Ñ‹Ñ….
        ///
        /// Ð‘Ð•Ð— Ð—ÐÐ©Ð˜Ð¢Ð«: ÐºÐ°Ð¶Ð´Ñ‹Ð¹ Ð¿Ð¾Ð²Ñ‚Ð¾Ñ€Ð½Ñ‹Ð¹ Ð²Ñ‹Ð·Ð¾Ð² ÑÐ¾Ð·Ð´Ð°Ñ‘Ñ‚ Ð½Ð¾Ð²Ñ‹Ðµ GraphicsBuffer
        /// Ð¸ Texture2D Ð±ÐµÐ· Release/Destroy ÑÑ‚Ð°Ñ€Ñ‹Ñ…. Unity ÐÐ• ÑÐ¾Ð±Ð¸Ñ€Ð°ÐµÑ‚
        /// GPU-Ñ€ÐµÑÑƒÑ€ÑÑ‹ Ñ‡ÐµÑ€ÐµÐ· GC â€” Ð¾Ð½Ð¸ ÑƒÑ‚ÐµÐºÐ°ÑŽÑ‚ Ð½Ð°Ð²ÑÐµÐ³Ð´Ð° Ð´Ð¾ Ð¿ÐµÑ€ÐµÐ·Ð°Ð¿ÑƒÑÐºÐ°.
        /// ÐÐ° MX350 (2GB VRAM): 5000 boids Ã— 32 bytes Ã— 2 buffers = 320KB
        /// Ð·Ð° ÐºÐ°Ð¶Ð´Ñ‹Ð¹ Ð²Ñ‹Ð·Ð¾Ð². 10 Ð²Ñ‹Ð·Ð¾Ð²Ð¾Ð² = 3.2MB.
        /// </summary>
        private void Awake()
        {
            boidCount = VRAMEnforcer.ApplyBoidPopulationBudget(boidCount, 64, 8192);

            // â”€â”€ Ð—Ð°Ñ‰Ð¸Ñ‚Ð° Ð¾Ñ‚ Ð¿Ð¾Ð²Ñ‚Ð¾Ñ€Ð½Ð¾Ð¹ Ð¸Ð½Ð¸Ñ†Ð¸Ð°Ð»Ð¸Ð·Ð°Ñ†Ð¸Ð¸ (v2.2) â”€â”€
            // Ð•ÑÐ»Ð¸ ÑƒÐ¶Ðµ Ð¸Ð½Ð¸Ñ†Ð¸Ð°Ð»Ð¸Ð·Ð¸Ñ€Ð¾Ð²Ð°Ð½ â€” ÑÐ½Ð°Ñ‡Ð°Ð»Ð° Ð¾ÑÐ²Ð¾Ð±Ð¾Ð¶Ð´Ð°ÐµÐ¼ ÑÑ‚Ð°Ñ€Ñ‹Ðµ Ñ€ÐµÑÑƒÑ€ÑÑ‹.
            // ÐŸÐ¾ÐºÑ€Ñ‹Ð²Ð°ÐµÑ‚ edge cases:
            //   â€¢ Reset ÐºÐ¾Ð¼Ð¿Ð¾Ð½ÐµÐ½Ñ‚Ð° Ð² Inspector Ð²Ð¾ Ð²Ñ€ÐµÐ¼Ñ Play Mode
            //   â€¢ ÐžÑˆÐ¸Ð±Ð¾Ñ‡Ð½Ñ‹Ð¹ Ð²Ñ‹Ð·Ð¾Ð² Ð¸Ð· Ð½Ð°ÑÐ»ÐµÐ´Ð½Ð¸ÐºÐ°
            //   â€¢ Unity internal re-Awake (ÐºÑ€Ð°Ð¹Ð½Ðµ Ñ€ÐµÐ´ÐºÐ¾, Ð½Ð¾ Ð²Ð¾Ð·Ð¼Ð¾Ð¶Ð½Ð¾
            //     Ð¿Ñ€Ð¸ AddComponent Ð½Ð° ÑƒÐ¶Ðµ ÑÑƒÑ‰ÐµÑÑ‚Ð²ÑƒÑŽÑ‰Ð¸Ð¹ GO)
            if (_initialized)
            {
                Hecton8.Core.H8Debug.LogWarning(
                    "[HectonBoidController] Awake() called while already initialized. Releasing old GPU resources before re-init.",
                    this);
                ReleaseBuffers();
                _initialized = false;
            }

            if (boidShader == null || fishMesh == null || fishMaterial == null)
            {
                Hecton8.Core.H8Debug.LogError("[HectonBoidController] Missing required references!");
                enabled = false;
                return;
            }

            if (!HasAuthoredNeutralTextures())
            {
                Hecton8.Core.H8Debug.LogError("[HectonBoidController] Missing authored neutral height/SDF/flow textures. Runtime texture fallback generation is forbidden.", this);
                enabled = false;
                return;
            }

            if (!InitializeCompute())
            {
                enabled = false;
                return;
            }

            InitializeBuffers();
            InitializeRendering();

            _simulationBounds = new Bounds(boundsCenter, boundsSize * 2f);
            _initialized      = true;
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            CacheRegistryServicesCold(forceRefresh: true);
            TryRegisterHotSwapListener();

            if (_registeredToTickManager)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            EnsureBoidBlackBoxCold();

            _registeredToTickManager = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);

            if (_playerTransform == null)
                FindPlayer();
        }

        private void OnDisable()
        {
            if (_registeredToTickManager)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredToTickManager = false;
            }

            TryUnregisterHotSwapListener();
            ReleaseBoidBlackBoxHandle(_dataVault);
            _foveatedSimulationDirector = null;
            _fluidRuntime = null;
            _dataVault = null;
            _playerRuntimeContext = null;
            _playerTransform = null;
            _mainCamera = null;
            _foveatedSimulationTier = FoveatedSimulationTier.Active;
            _boidBlackBoxCursor = 0;
            _boidBlackBoxWritten = 0;
            _boidBlackBoxDumped = false;
        }

        private void OnDestroy()
        {
            TryUnregisterHotSwapListener();
            ReleaseBoidBlackBoxHandle(_dataVault);
            ReleaseBuffers();
        }

        private bool HasAuthoredNeutralTextures()
        {
            return (heightMap != null || neutralHeightMap != null) &&
                   neutralCaveSdfTexture != null &&
                   neutralAbyssalFlowTexture != null;
        }

        /// <inheritdoc />
        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Player:
                    _playerRuntimeContext = currentService as IPlayerRuntimeContext;
                    _playerTransform = _playerRuntimeContext != null ? _playerRuntimeContext.PlayerTransform : null;
                    _mainCamera = null;
                    break;
                case GlobalRegistryServiceSlot.FluidRuntime:
                    _fluidRuntime = currentService as IAbyssalFlowGpuReadModel;
                    break;
                case GlobalRegistryServiceSlot.FoveatedSimulationDirector:
                    _foveatedSimulationDirector = currentService as IFoveatedSimulationDirector;
                    _foveatedSimulationTier = FoveatedSimulationTier.Active;
                    break;
                case GlobalRegistryServiceSlot.DataVault:
                    ReleaseBoidBlackBoxHandle(previousService as IDataVault ?? _dataVault);
                    _dataVault = currentService as IDataVault;
                    _boidBlackBoxCursor = 0;
                    _boidBlackBoxWritten = 0;
                    _boidBlackBoxDumped = false;
                    EnsureBoidBlackBoxCold();
                    break;
            }
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  ITickable â€” MAIN LOOP
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>
        /// Called during dispatcher late-frame visual sync.
        ///
        /// Order:
        ///   1. Update target position (from player).
        ///   2. Set compute shader uniforms (includes Ping-Pong buffer binding).
        ///   3. Dispatch compute shader (GPU simulation).
        ///   4. Increment frame index (swap buffers for next frame).
        ///   5. Frustum culling check.
        ///   6. Instanced draw (if visible) â€” reads from writeBuffer.
        ///
        /// CPU cost: ~0.01ms (uniform upload + dispatch command + draw command).
        /// Actual computation happens on GPU asynchronously.
        /// </summary>
        public void LateFrameTick()
        {
            RunBoidVisualSync(SystemDispatcher.CurrentFrameDeltaTime);
        }

        private void RunBoidVisualSync(float deltaTime)
        {
            using (ProfilerRegistry.AiTick.Auto())
            {
            uint frameInputFaultFlags = math.isfinite(deltaTime) && deltaTime >= 0f ? 0u : BoidBlackBoxFaultInvalidDeltaTime;
            float safeDeltaTime = ClampMinFinite(deltaTime, 0f, 0f);
            if (!_initialized)
            {
                WriteBoidBlackBoxFrame(safeDeltaTime, false, frameInputFaultFlags);
                return;
            }

            if (!HasRuntimeBuffersReady())
            {
                WriteBoidBlackBoxFrame(safeDeltaTime, false, frameInputFaultFlags | BoidBlackBoxFaultMissingBuffers);
                return;
            }

            AdvanceBoidClock(safeDeltaTime);

            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
            //  1. UPDATE TARGET
            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

            UpdateTarget();
            UpdateSpatialGridLayout();
            if (_foveatedSimulationDirector != null)
                _foveatedSimulationTier = _foveatedSimulationDirector.ResolveTierForPosition(boundsCenter);
            bool simulateBoids = _foveatedSimulationTier != FoveatedSimulationTier.Frozen;

            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
            //  2. SET UNIFORMS (includes Ping-Pong buffer binding)
            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

            if (simulateBoids)
            {
                ConsumeAcousticPingSignals();
                SetComputeUniforms(safeDeltaTime);
            }

            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
            //  3. DISPATCH COMPUTE
            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

#if UNITY_EDITOR
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
#endif

            if (simulateBoids)
            {
                if (_dispatchGroupCount > 0 && _clearSpatialGridGroupCount > 0 && _buildSpatialGridGroupCount > 0)
                {
                    DispatchSpatialGridBuild();
                    boidShader.Dispatch(_kernelCSMain, _dispatchGroupCount, 1, 1);
                }
            }

#if UNITY_EDITOR
            _debugComputeMs = simulateBoids
                ? (float)((System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency)
                : 0f;
#endif

            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
            //  4. INCREMENT FRAME INDEX (swap for next frame)
            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

            if (simulateBoids)
                _frameIndex++;

            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
            //  5. FRUSTUM CULLING
            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

            GraphicsBuffer currentDataBuffer = (_frameIndex % 2 == 0) ? _boidsBufferA : _boidsBufferB;
            DispatchComputeFrustumCulling(currentDataBuffer);
            bool isVisible = true;

#if UNITY_EDITOR
            _debugIsVisible = isVisible;
#endif

            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
            //  6. RENDER (if visible)
            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

            RenderBoids();
            WriteBoidBlackBoxFrame(safeDeltaTime, simulateBoids, frameInputFaultFlags);
            }
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  INITIALIZATION â€” COMPUTE
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>
        /// Finds kernel, reads thread group size, computes dispatch count.
        /// </summary>
        private bool InitializeCompute()
        {
            if (boidShader == null || !SystemInfo.supportsComputeShaders)
                return false;

            if (!TryResolveKernel("CSMain", out _kernelCSMain) ||
                !TryResolveKernel("ClearSpatialGrid", out _kernelClearSpatialGrid) ||
                !TryResolveKernel("BuildSpatialGrid", out _kernelBuildSpatialGrid) ||
                !TryResolveKernel("BitonicSort", out _kernelBitonicSort) ||
                !TryResolveKernel("ComputeCellOffsets", out _kernelComputeCellOffsets) ||
                !TryResolveKernel("ClearVisibleIndirectArgs", out _kernelClearVisibleIndirectArgs) ||
                !TryResolveKernel("CullVisibleBoids", out _kernelCullVisibleBoids))
            {
                return false;
            }

            if (!TryResolveThreadGroupSizeX(_kernelCSMain, out _threadGroupSizeX) ||
                !TryResolveThreadGroupSizeX(_kernelClearSpatialGrid, out _clearSpatialGridThreadGroupSizeX) ||
                !TryResolveThreadGroupSizeX(_kernelBuildSpatialGrid, out _buildSpatialGridThreadGroupSizeX) ||
                !TryResolveThreadGroupSizeX(_kernelComputeCellOffsets, out _computeCellOffsetsThreadGroupSizeX) ||
                !TryResolveThreadGroupSizeX(_kernelClearVisibleIndirectArgs, out _clearVisibleIndirectArgsThreadGroupSizeX) ||
                !TryResolveThreadGroupSizeX(_kernelCullVisibleBoids, out _cullVisibleBoidsThreadGroupSizeX))
            {
                return false;
            }

            RefreshDispatchGroupCounts();

#if UNITY_EDITOR
            _debugDispatchGroups = _dispatchGroupCount;
#endif
            return true;
        }

        private bool HasRuntimeBuffersReady()
        {
            return fishMaterial != null &&
                   _boidsBufferA != null &&
                   _boidsBufferB != null &&
                   _boidsPhysicsBufferA != null &&
                   _boidsPhysicsBufferB != null &&
                   _spatialGridCountBuffer != null &&
                   _boidCellKeyValuesBuffer != null &&
                   _spatialGridOffsetsBuffer != null &&
                   _visibleBoidIndexBuffer != null &&
                   _visibleIndirectArgsBuffer != null &&
                   _boidsBufferA.count == boidCount &&
                   _boidsBufferB.count == boidCount &&
                   _spatialGridCountBuffer.count == SpatialGridMaxCellCount &&
                   _boidCellKeyValuesBuffer.count == Mathf.NextPowerOfTwo(boidCount) &&
                   _spatialGridOffsetsBuffer.count == SpatialGridMaxCellCount &&
                   _visibleBoidIndexBuffer.count == boidCount;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  INITIALIZATION â€” BUFFERS (v2.2 â€” GPU Memory Leak Fix)
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>
        /// Creates both Ping-Pong GraphicsBuffers, fills with identical initial positions,
        /// uploads to GPU. Creates args buffer for indirect draw.
        ///
        /// v2.2 Patch: ÐŸÐµÑ€ÐµÐ´ ÑÐ¾Ð·Ð´Ð°Ð½Ð¸ÐµÐ¼ ÐºÐ°Ð¶Ð´Ð¾Ð³Ð¾ GPU-Ñ€ÐµÑÑƒÑ€ÑÐ° Ð²Ñ‹Ð·Ñ‹Ð²Ð°ÐµÑ‚ÑÑ Release/Destroy
        /// Ð´Ð»Ñ ÑÑ‚Ð°Ñ€Ð¾Ð³Ð¾, ÐµÑÐ»Ð¸ Ð¾Ð½ Ð½Ðµ null. Ð­Ñ‚Ð¾ Ð¿Ñ€ÐµÐ´Ð¾Ñ‚Ð²Ñ€Ð°Ñ‰Ð°ÐµÑ‚ ÑƒÑ‚ÐµÑ‡ÐºÑƒ VRAM Ð¿Ñ€Ð¸:
        ///   â€¢ ÐŸÐ¾Ð²Ñ‚Ð¾Ñ€Ð½Ð¾Ð¼ Ð²Ñ‹Ð·Ð¾Ð²Ðµ InitializeBuffers() (hot reload, redesign).
        ///   â€¢ ÐŸÐµÑ€ÐµÑÐ¾Ð·Ð´Ð°Ð½Ð¸Ð¸ ÑÐ¸ÑÑ‚ÐµÐ¼Ñ‹ Ñ‡ÐµÑ€ÐµÐ· public API (SetBoidCount Ð² Ð±ÑƒÐ´ÑƒÑ‰ÐµÐ¼).
        ///   â€¢ Edge case Ñ Awake (ÑÐ¼. ÐºÐ¾Ð¼Ð¼ÐµÐ½Ñ‚Ð°Ñ€Ð¸Ð¹ Ð² Awake).
        ///
        /// ÐŸÐžÐ Ð¯Ð”ÐžÐš: Release old â†’ Create new â†’ LockBufferForWrite upload.
        /// Ð•ÑÐ»Ð¸ Release Ð²Ñ‹Ð·Ð²Ð°Ð½ Ð½Ð° ÑƒÐ¶Ðµ released Ð±ÑƒÑ„ÐµÑ€ â€” Unity Ð¿Ñ€Ð¾ÑÑ‚Ð¾ Ð¸Ð³Ð½Ð¾Ñ€Ð¸Ñ€ÑƒÐµÑ‚.
        /// Null-check Ð¾Ð±ÑÐ·Ð°Ñ‚ÐµÐ»ÐµÐ½, Ñ‚.Ðº. Release() Ð½Ð° null = NullReferenceException.
        ///
        /// ALLOCATION: One-time. BoidData[] on managed heap (released by GC after upload).
        /// Both GraphicsBuffers live on GPU until Release().
        /// Both buffers get identical data so first-frame Read is never garbage.
        ///
        /// SPAWN Y RANGE:
        ///   ÐÐ¸Ð¶Ð½ÑÑ Ð³Ñ€Ð°Ð½Ð¸Ñ†Ð°: boundsCenter.y - boundsSize.y (Ð¿Ð¾Ð»Ð½Ð°Ñ Ð²Ñ‹ÑÐ¾Ñ‚Ð° Ð±Ð¾ÐºÑÐ° Ð²Ð½Ð¸Ð·).
        ///   Ð’ÐµÑ€Ñ…Ð½ÑÑ Ð³Ñ€Ð°Ð½Ð¸Ñ†Ð°: waterSurfaceY - 2f (2 Ð¼ÐµÑ‚Ñ€Ð° Ð½Ð¸Ð¶Ðµ Ð¿Ð¾Ð²ÐµÑ€Ñ…Ð½Ð¾ÑÑ‚Ð¸ Ð²Ð¾Ð´Ñ‹).
        ///   Ð Ñ‹Ð±Ñ‹ Ñ€Ð°ÑÐ¿Ñ€ÐµÐ´ÐµÐ»ÑÑŽÑ‚ÑÑ Ñ€Ð°Ð²Ð½Ð¾Ð¼ÐµÑ€Ð½Ð¾ Ð¿Ð¾ Ð²ÑÐµÐ¼Ñƒ Ð²ÐµÑ€Ñ‚Ð¸ÐºÐ°Ð»ÑŒÐ½Ð¾Ð¼Ñƒ Ð´Ð¸Ð°Ð¿Ð°Ð·Ð¾Ð½Ñƒ Ð±Ð¾ÐºÑÐ°.
        /// </summary>
        private void InitializeBuffers()
        {
            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
            //  STEP 1: Release existing GPU resources (if any)
            //
            //  GraphicsBuffer Ð¸ GraphicsBuffer â€” unmanaged GPU memory.
            //  Unity GC Ð¸Ñ… ÐÐ• Ð¾ÑÐ²Ð¾Ð±Ð¾Ð¶Ð´Ð°ÐµÑ‚. Ð‘ÐµÐ· Release() â€” Ð¿Ñ€ÑÐ¼Ð°Ñ
            //  ÑƒÑ‚ÐµÑ‡ÐºÐ° VRAM. ÐÐ° MX350 Ñ 2GB ÑÑ‚Ð¾ ÐºÑ€Ð¸Ñ‚Ð¸Ñ‡Ð½Ð¾.
            //
            //  Texture2D â€” managed, Ð½Ð¾ GPU-ÑÑ‚Ð¾Ñ€Ð¾Ð½Ð° (native texture)
            //  Ð¾ÑÐ²Ð¾Ð±Ð¾Ð¶Ð´Ð°ÐµÑ‚ÑÑ Ñ‚Ð¾Ð»ÑŒÐºÐ¾ Ñ‡ÐµÑ€ÐµÐ· Destroy(). Ð‘ÐµÐ· Destroy() â€”
            //  native texture ÑƒÑ‚ÐµÐºÐ°ÐµÑ‚ Ð´Ð¾ Ð²Ñ‹Ñ…Ð¾Ð´Ð° Ð¸Ð· Play Mode.
            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

            // â”€â”€ Release old Ping-Pong buffers â”€â”€
            if (_boidsBufferA != null) { _boidsBufferA.Release(); _boidsBufferA = null; }
            if (_boidsBufferB != null) { _boidsBufferB.Release(); _boidsBufferB = null; }
            // SoA physics buffers
            if (_boidsPhysicsBufferA != null) { _boidsPhysicsBufferA.Release(); _boidsPhysicsBufferA = null; }
            if (_boidsPhysicsBufferB != null) { _boidsPhysicsBufferB.Release(); _boidsPhysicsBufferB = null; }

            if (_spatialGridCountBuffer != null)
            {
                _spatialGridCountBuffer.Release();
                _spatialGridCountBuffer = null;
            }

            if (_boidCellKeyValuesBuffer != null)
            {
                _boidCellKeyValuesBuffer.Release();
                _boidCellKeyValuesBuffer = null;
            }

            if (_spatialGridOffsetsBuffer != null)
            {
                _spatialGridOffsetsBuffer.Release();
                _spatialGridOffsetsBuffer = null;
            }

            if (_fallbackFlowFieldBuffer != null)
            {
                _fallbackFlowFieldBuffer.Release();
                _fallbackFlowFieldBuffer = null;
            }

            if (_visibleBoidIndexBuffer != null)
            {
                _visibleBoidIndexBuffer.Release();
                _visibleBoidIndexBuffer = null;
            }

            if (_visibleIndirectArgsBuffer != null)
            {
                _visibleIndirectArgsBuffer.Release();
                _visibleIndirectArgsBuffer = null;
            }

            if (_boidUploadStagingBuffer != null)
            {
                _boidUploadStagingBuffer.Release();
                _boidUploadStagingBuffer = null;
            }

            if (_visibleIndirectArgsUploadBuffer != null)
            {
                _visibleIndirectArgsUploadBuffer.Release();
                _visibleIndirectArgsUploadBuffer = null;
            }

            _indirectArgsMesh = null;

            if (_fallbackVoxelSdfTexture != null)
            {
                _fallbackVoxelSdfTexture = null;
            }

            if (_fallbackAbyssalFlowTexture != null)
            {
                _fallbackAbyssalFlowTexture = null;
            }

            // â”€â”€ Release old args buffer â”€â”€
            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
            //  STEP 2: Create Ping-Pong boids buffers
            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

            _boidsBufferA = GraphicsBufferUploadUtility.CreateStructuredCopyDestinationBuffer<BoidData>(boidCount); // COLD ALLOC: 32B boid ping-A
            _boidsBufferB = GraphicsBufferUploadUtility.CreateStructuredCopyDestinationBuffer<BoidData>(boidCount); // COLD ALLOC: 32B boid ping-B
            // SoA physics buffers: 24B stride (position+velocity only), ping-ponged in sync
            _boidsPhysicsBufferA = new GraphicsBuffer(GraphicsBuffer.Target.Structured, boidCount, BoidPhysicsStride); // COLD ALLOC: 24B physics ping-A
            _boidsPhysicsBufferB = new GraphicsBuffer(GraphicsBuffer.Target.Structured, boidCount, BoidPhysicsStride); // COLD ALLOC: 24B physics ping-B
            _boidUploadStagingBuffer = GraphicsBufferUploadUtility.CreateStructuredUploadStagingBuffer<BoidData>(boidCount); // COLD ALLOC: GraphicsBuffer[boidCount] - CPU-visible boid reset staging, GPU copy source only - owner: HectonBoidController
            _spatialGridCountBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Raw,
                SpatialGridMaxCellCount,
                SpatialGridCounterStride); // COLD ALLOC: GraphicsBuffer[SpatialGridMaxCellCount] - GPU-written spatial cell counters - owner: HectonBoidController
            _boidCellKeyValuesBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Mathf.NextPowerOfTwo(boidCount), 8);
            _spatialGridOffsetsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, SpatialGridMaxCellCount, 4); // COLD ALLOC: GraphicsBuffer[spatial cells] - GPU-written spatial cell entries - owner: HectonBoidController
            _fallbackFlowFieldBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<Vector4>(1);
            UploadFallbackFlowField();
            _visibleBoidIndexBuffer = GraphicsBufferUploadUtility.CreateStructuredBuffer<uint>(boidCount); // COLD ALLOC: GraphicsBuffer[boidCount] - GPU-written visible boid indices - owner: HectonBoidController
            _visibleIndirectArgsBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.CopyDestination,
                1,
                GraphicsBuffer.IndirectDrawIndexedArgs.size); // COLD ALLOC: GraphicsBuffer[IndirectDrawIndexedArgs] - GPU-written visible boid draw args - owner: HectonBoidController
            _visibleIndirectArgsUploadBuffer = GraphicsBufferUploadUtility.CreateRawIndirectUploadStagingBuffer(
                1,
                GraphicsBuffer.IndirectDrawIndexedArgs.size); // COLD ALLOC: GraphicsBuffer[IndirectDrawIndexedArgs] - CPU-visible visible-boid args staging, GPU copy source only - owner: HectonBoidController
            UploadIndirectArgsStaticMeshData();

            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
            //  STEP 3: Fill initial data
            //  One array, uploaded to BOTH buffers.
            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

            UploadSpawnSetToBoidBuffers(boundsCenter, 0xB01D5EEDu, 0xB01D7101u, false, allowResize: true);

            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
            //  STEP 4: Initialize frame index
            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

            _frameIndex = 0;

            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
            //  STEP 5: Fallback heightmap (v2.2 â€” reuse if exists)
            //
            //  Ð¢ÐµÐºÑÑ‚ÑƒÑ€Ð° ÑÐ¾Ð·Ð´Ð°Ñ‘Ñ‚ÑÑ Ð¢ÐžÐ›Ð¬ÐšÐž ÐµÑÐ»Ð¸ ÐµÑ‰Ñ‘ Ð½Ðµ ÑÑƒÑ‰ÐµÑÑ‚Ð²ÑƒÐµÑ‚.
            //  ÐŸÑ€Ð¸ Ð¿Ð¾Ð²Ñ‚Ð¾Ñ€Ð½Ð¾Ð¼ Ð²Ñ‹Ð·Ð¾Ð²Ðµ InitializeBuffers() ÑÑ‚Ð°Ñ€Ð°Ñ Ñ‚ÐµÐºÑÑ‚ÑƒÑ€Ð°
            //  Ð¿ÐµÑ€ÐµÐ¸ÑÐ¿Ð¾Ð»ÑŒÐ·ÑƒÐµÑ‚ÑÑ â€” zero VRAM leak.
            //
            //  Ð•ÑÐ»Ð¸ heightMap Ð½Ð°Ð·Ð½Ð°Ñ‡ÐµÐ½Ð° Ð² Inspector â€” fallback Ð½Ðµ Ð½ÑƒÐ¶ÐµÐ½,
            //  Ð½Ð¾ Ð¼Ñ‹ ÐµÐ³Ð¾ ÐÐ• ÑƒÐ½Ð¸Ñ‡Ñ‚Ð¾Ð¶Ð°ÐµÐ¼ (Ð¾Ð½ Ð¼Ð¾Ð¶ÐµÑ‚ Ð¿Ð¾Ð½Ð°Ð´Ð¾Ð±Ð¸Ñ‚ÑŒÑÑ ÐµÑÐ»Ð¸
            //  heightMap Ð±ÑƒÐ´ÐµÑ‚ ÑÐ½ÑÑ‚ Ð² Ñ€Ð°Ð½Ñ‚Ð°Ð¹Ð¼Ðµ Ñ‡ÐµÑ€ÐµÐ· SetHeightMap(null, ...)).
            //
            //  Ð£Ð½Ð¸Ñ‡Ñ‚Ð¾Ð¶ÐµÐ½Ð¸Ðµ _fallbackHeightMap â€” Ð¢ÐžÐ›Ð¬ÐšÐž Ð² ReleaseBuffers()
            //  (Ð²Ñ‹Ð·Ñ‹Ð²Ð°ÐµÑ‚ÑÑ Ð¸Ð· OnDestroy).
            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

            _fallbackHeightMap = heightMap == null ? neutralHeightMap : null;

            if (heightMap == null && _fallbackHeightMap == null)
            {
                // Ð¡Ð¾Ð·Ð´Ð°Ñ‘Ð¼ Ð¼Ð¸Ð½Ð¸Ð¼Ð°Ð»ÑŒÐ½ÑƒÑŽ Ñ‚ÐµÐºÑÑ‚ÑƒÑ€Ñƒ 4Ã—4 (R8 = 16 Ð±Ð°Ð¹Ñ‚ Ð½Ð° GPU).
                // Ð§Ñ‘Ñ€Ð½Ð°Ñ = Ð²Ñ‹ÑÐ¾Ñ‚Ð° 0 = Ð¿Ð»Ð¾ÑÐºÐ¾Ðµ Ð´Ð½Ð¾.
                // hideFlags Ð¿Ñ€ÐµÐ´Ð¾Ñ‚Ð²Ñ€Ð°Ñ‰Ð°ÐµÑ‚ Ð¿Ð¾ÑÐ²Ð»ÐµÐ½Ð¸Ðµ Ð² Project/Hierarchy.
                _fallbackHeightMap = neutralHeightMap;
                /*
                Runtime texture synthesis removed; neutralHeightMap is assigned above.
                {
                    name       = "[HectonBoid] FallbackHeightMap",
                    hideFlags  = HideFlags.HideAndDontSave,
                    wrapMode   = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };

                // NativeArray path: zero managed Color[] allocation.
                // GetRawTextureData returns existing native buffer â€” zero GC.
                Runtime raw texture mutation removed.
                for (int i = 0; i < rawData.Length; i++)
                {
                    rawData[i] = 0; // Black = height 0
                }

                Runtime texture upload removed.
                */
                // makeNoLongerReadable=false: ÑÐ¾Ñ…Ñ€Ð°Ð½ÑÐµÐ¼ CPU-ÐºÐ¾Ð¿Ð¸ÑŽ
                // Ð´Ð»Ñ Ð²Ð¾Ð·Ð¼Ð¾Ð¶Ð½Ð¾Ð³Ð¾ Ð¿ÐµÑ€ÐµÑ‡Ð¸Ñ‚Ñ‹Ð²Ð°Ð½Ð¸Ñ Ð¿Ñ€Ð¸ hot reload.
            }

            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
            //  STEP 6: Args buffer for RenderMeshIndirect
            //  (old buffer already released in STEP 1)
            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

            EnsureFallbackVoxelSdfTexture();
            EnsureFallbackAbyssalFlowTexture();

            // GraphicsBuffer.IndirectDrawIndexedArgs: 5 uints = 20 bytes
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  INITIALIZATION â€” RENDERING
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void EnsureFallbackVoxelSdfTexture()
        {
            if (_fallbackVoxelSdfTexture != null)
                return;

            _fallbackVoxelSdfTexture = neutralCaveSdfTexture;
        }

        private void EnsureFallbackAbyssalFlowTexture()
        {
            if (_fallbackAbyssalFlowTexture != null)
                return;

            _fallbackAbyssalFlowTexture = neutralAbyssalFlowTexture;
        }

        private void UploadFallbackFlowField()
        {
            if (_fallbackFlowFieldBuffer == null)
                return;

            var mapped = _fallbackFlowFieldBuffer.LockBufferForWrite<Vector4>(0, 1);
            try
            {
                mapped[0] = Vector4.zero;
            }
            finally
            {
                _fallbackFlowFieldBuffer.UnlockBufferAfterWrite<Vector4>(1);
            }
        }

        private void UploadIndirectArgsStaticMeshData()
        {
            if (_visibleIndirectArgsBuffer == null || fishMesh == null || ReferenceEquals(_indirectArgsMesh, fishMesh))
                return;

            _visibleIndirectArgsUpload[0] = new GraphicsBuffer.IndirectDrawIndexedArgs
            {
                indexCountPerInstance = fishMesh.GetIndexCount(0),
                instanceCount = 0u,
                startIndex = fishMesh.GetIndexStart(0),
                baseVertexIndex = (uint)Mathf.Max(0, fishMesh.GetBaseVertex(0)),
                startInstance = 0u
            };
            GraphicsBufferUploadUtility.UploadArrayAndCopyWholeBuffer(
                _visibleIndirectArgsUploadBuffer,
                _visibleIndirectArgsBuffer,
                _visibleIndirectArgsUpload,
                1);
            _indirectArgsMesh = fishMesh;
        }

        private void UploadSpawnSetToBoidBuffers(Vector3 center, uint positionSeed, uint velocitySeed, bool useMinimumVelocity, bool allowResize)
        {
            if (_boidsBufferA == null || _boidsBufferB == null)
                return;

            int safeCount = math.min(boidCount, math.min(_boidsBufferA.count, _boidsBufferB.count));
            if (safeCount <= 0 || !EnsureSpawnUploadBufferCapacity(safeCount, allowResize))
                return;

            float spawnSpeed = useMinimumVelocity ? minSpeed : (minSpeed + maxSpeed) * 0.5f;
            for (int i = 0; i < safeCount; i++)
            {
                Vector3 position = center + ResolveDeterministicScatterVector(i, center, positionSeed) * spawnRadius;
                position.y = Mathf.Clamp(position.y, center.y - boundsSize.y, waterSurfaceY - 2f);

                Vector3 velocity = ResolveDeterministicScatterVector(i, center, velocitySeed) * spawnSpeed;
                float minimumSpeedSq = minSpeed * minSpeed;
                if (velocity.sqrMagnitude < minimumSpeedSq)
                    velocity = ResolveDeterministicCardinalAxis(i, velocitySeed) * minSpeed;

                _spawnUploadBuffer[i] = new BoidData
                {
                    position = position,
                    velocity = velocity,
                    panic = 0f,
                    stateFlags = 0u
                };

                _spawnPhysicsUploadBuffer[i] = new BoidPhysicsData
                {
                    position = position,
                    velocity = velocity
                };
            }

            GraphicsBufferUploadUtility.UploadArrayAndCopyWholeBuffer(_boidUploadStagingBuffer, _boidsBufferA, _spawnUploadBuffer, safeCount);
            GraphicsBufferUploadUtility.UploadArrayAndCopyWholeBuffer(_boidUploadStagingBuffer, _boidsBufferB, _spawnUploadBuffer, safeCount);

            // Pre-populate physics buffers to avoid massive origin-pop on frame 0
            _boidsPhysicsBufferA.SetData(_spawnPhysicsUploadBuffer, 0, 0, safeCount);
            _boidsPhysicsBufferB.SetData(_spawnPhysicsUploadBuffer, 0, 0, safeCount);
        }

        private bool EnsureSpawnUploadBufferCapacity(int safeCount, bool allowResize)
        {
            if (safeCount <= 0)
                return false;

            if (_spawnUploadBuffer == null || _spawnUploadBuffer.Length < safeCount ||
                _spawnPhysicsUploadBuffer == null || _spawnPhysicsUploadBuffer.Length < safeCount)
            {
                if (!allowResize)
                    return false;

                _spawnUploadBuffer = new BoidData[safeCount]; // COLD ALLOC: BoidData[safeCount] - reusable boid spawn/reset upload staging - owner: HectonBoidController
                _spawnPhysicsUploadBuffer = new BoidPhysicsData[safeCount]; // COLD ALLOC: BoidPhysicsData[safeCount] - reusable boid physics upload staging - owner: HectonBoidController
            }

            return true;
        }

        /// <summary>
        /// Sets up owner-local draw-property bindings and RenderParams.
        /// Uses authored fishMaterial directly; mutable buffers/scalars live in matProps.
        /// Draw buffers bind through a draw-local property block before each indirect draw.
        /// </summary>
        private void InitializeRendering()
        {
            if (fishMaterial == null)
                return;

            EnsureRenderMaterialPropertiesCold();

            // Frame 0: Read=A, Write=B â†’ after dispatch, fresh data is in B
            _renderParams = new RenderParams(fishMaterial)
            {
                worldBounds          = _simulationBounds,
                shadowCastingMode    = shadowMode,
                receiveShadows       = false,
                renderingLayerMask   = HectonLayerMasks.ToRenderingLayerMask(renderingLayerMask),
                matProps             = _renderMaterialProperties
            };
        }

        private void EnsureRenderMaterialPropertiesCold()
        {
            if (_renderMaterialProperties != null)
                return;

            _renderMaterialProperties = new MaterialPropertyBlock();
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  BUFFER RELEASE (v2.2 â€” Safe for repeated calls)
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>
        /// Releases all GPU resources. Called in OnDestroy and
        /// as safety cleanup before re-initialization in Awake.
        ///
        /// CRITICAL: GraphicsBuffer Ð¸ GraphicsBuffer MUST be released manually.
        /// Unity does NOT garbage collect GPU buffers.
        /// Texture2D native side must be destroyed via Object.Destroy().
        ///
        /// ÐŸÐ°Ñ‚Ñ‚ÐµÑ€Ð½: null-check â†’ Release/Destroy â†’ null assignment.
        /// Null assignment Ð¿Ñ€ÐµÐ´Ð¾Ñ‚Ð²Ñ€Ð°Ñ‰Ð°ÐµÑ‚ double-Release Ð¿Ñ€Ð¸ Ð¿Ð¾Ð²Ñ‚Ð¾Ñ€Ð½Ñ‹Ñ… Ð²Ñ‹Ð·Ð¾Ð²Ð°Ñ….
        ///
        /// Ð‘ÐµÐ·Ð¾Ð¿Ð°ÑÐ½Ð¾ Ð²Ñ‹Ð·Ñ‹Ð²Ð°Ñ‚ÑŒ Ð¼Ð½Ð¾Ð³Ð¾ÐºÑ€Ð°Ñ‚Ð½Ð¾ â€” Ð²ÑÐµ Ð²ÐµÑ‚ÐºÐ¸ Ð¿Ñ€Ð¾Ð²ÐµÑ€ÑÑŽÑ‚ null.
        /// ÐŸÐ¾Ñ€ÑÐ´Ð¾Ðº Ð½Ðµ ÐºÑ€Ð¸Ñ‚Ð¸Ñ‡ÐµÐ½ â€” Ð±ÑƒÑ„ÐµÑ€Ñ‹ Ð½ÐµÐ·Ð°Ð²Ð¸ÑÐ¸Ð¼Ñ‹ Ð´Ñ€ÑƒÐ³ Ð¾Ñ‚ Ð´Ñ€ÑƒÐ³Ð°.
        /// </summary>
        private void ReleaseBuffers()
        {
            if (_boidsBufferA != null)
            {
                _boidsBufferA.Release();
                _boidsBufferA = null;
            }

            if (_boidsBufferB != null)
            {
                _boidsBufferB.Release();
                _boidsBufferB = null;
            }

            if (_boidsPhysicsBufferA != null)
            {
                _boidsPhysicsBufferA.Release();
                _boidsPhysicsBufferA = null;
            }

            if (_boidsPhysicsBufferB != null)
            {
                _boidsPhysicsBufferB.Release();
                _boidsPhysicsBufferB = null;
            }

            if (_spatialGridCountBuffer != null)
            {
                _spatialGridCountBuffer.Release();
                _spatialGridCountBuffer = null;
            }



            if (_fallbackFlowFieldBuffer != null)
            {
                _fallbackFlowFieldBuffer.Release();
                _fallbackFlowFieldBuffer = null;
            }

            if (_visibleBoidIndexBuffer != null)
            {
                _visibleBoidIndexBuffer.Release();
                _visibleBoidIndexBuffer = null;
            }

            if (_visibleIndirectArgsBuffer != null)
            {
                _visibleIndirectArgsBuffer.Release();
                _visibleIndirectArgsBuffer = null;
            }

            if (_boidUploadStagingBuffer != null)
            {
                _boidUploadStagingBuffer.Release();
                _boidUploadStagingBuffer = null;
            }

            if (_visibleIndirectArgsUploadBuffer != null)
            {
                _visibleIndirectArgsUploadBuffer.Release();
                _visibleIndirectArgsUploadBuffer = null;
            }

            _indirectArgsMesh = null;

            if (_fallbackHeightMap != null)
            {
                _fallbackHeightMap = null;
            }

            if (_fallbackVoxelSdfTexture != null)
            {
                _fallbackVoxelSdfTexture = null;
            }

            if (_fallbackAbyssalFlowTexture != null)
            {
                _fallbackAbyssalFlowTexture = null;
            }

            if (_renderMaterialProperties != null)
                _renderMaterialProperties.Clear();
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  COMPUTE â€” UNIFORM UPLOAD + PING-PONG BINDING
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>
        /// Sets all compute shader uniforms and binds Ping-Pong buffers.
        ///
        /// Ping-Pong logic:
        ///   Even _frameIndex â†’ Read from A, Write to B.
        ///   Odd  _frameIndex â†’ Read from B, Write to A.
        ///
        /// All SetFloat/SetInt/SetVector/SetTexture/SetBuffer â€” zero GC.
        /// Called once per frame, BEFORE Dispatch.
        /// </summary>
        private void SetComputeUniforms(float dt)
        {
            ComputeShader cs = boidShader;
            int kernel = _kernelCSMain;
            float safeDeltaTime = ClampMinFinite(dt, 0f, 0f);
            Vector3 safeGridOrigin = ClampFiniteVector3(_spatialGridOrigin, -RuntimeVectorComponentLimitMeters, RuntimeVectorComponentLimitMeters, Vector3.zero);
            float safeGridCellSize = ClampFinite(_spatialGridCellSize, 0.001f, MaxBoidRadiusMeters, 5f);
            Vector3 safeTargetPosition = ClampFiniteVector3(_targetPosition, -RuntimeVectorComponentLimitMeters, RuntimeVectorComponentLimitMeters, boundsCenter);
            Vector3 safeBoundsCenter = ClampFiniteVector3(boundsCenter, -RuntimeVectorComponentLimitMeters, RuntimeVectorComponentLimitMeters, Vector3.zero);
            Vector3 safeBoundsSize = ResolveSafeBoundsSize();
            Vector2 safeWorldOffset = ClampFiniteVector2(worldOffset, -RuntimeVectorComponentLimitMeters, RuntimeVectorComponentLimitMeters, Vector2.zero);
            Vector2 safeWorldSize = ClampFiniteVector2(worldSize, 0.001f, RuntimeVectorComponentLimitMeters, new Vector2(1024f, 1024f));
            float safeHeightScale = ClampFinite(heightScale, 0f, MaxHeightScaleMeters, 100f);
            float safeWaterSurfaceY = ClampFinite(waterSurfaceY, -RuntimeVectorComponentLimitMeters, RuntimeVectorComponentLimitMeters, 0f);
            float safeMinSpeed = ClampFinite(minSpeed, 0f, MaxBoidSpeedMetersPerSecond, 2f);
            float safeMaxSpeed = ClampFinite(maxSpeed, safeMinSpeed, MaxBoidSpeedMetersPerSecond, math.max(safeMinSpeed, 6f));

            // â”€â”€ Ping-Pong Buffer Binding â”€â”€
            // Determine which buffer is Read and which is Write this frame.
            // No allocation â€” just pointer swap via ternary on existing references.
            GraphicsBuffer readBuffer  = (_frameIndex % 2 == 0) ? _boidsBufferA : _boidsBufferB;
            GraphicsBuffer writeBuffer = (_frameIndex % 2 == 0) ? _boidsBufferB : _boidsBufferA;

            GraphicsBuffer physicsReadBuffer  = (_frameIndex % 2 == 0) ? _boidsPhysicsBufferA : _boidsPhysicsBufferB;
            GraphicsBuffer physicsWriteBuffer = (_frameIndex % 2 == 0) ? _boidsPhysicsBufferB : _boidsPhysicsBufferA;

            cs.SetBuffer(kernel, ShaderProps.BoidsBufferRead, readBuffer);
            cs.SetBuffer(kernel, ShaderProps.BoidsBufferWrite, writeBuffer);
            // SoA physics buffers for hot-path kernels (24B reads in AccumulateSpatialNeighbor)
            cs.SetBuffer(kernel, ShaderProps.BoidsPhysicsRead, physicsReadBuffer);
            cs.SetBuffer(kernel, ShaderProps.BoidsPhysicsWrite, physicsWriteBuffer);
            cs.SetBuffer(kernel, ShaderProps.SpatialGridCounts, _spatialGridCountBuffer);
            // BuildSpatialGrid: uses _BoidsPhysicsRead (position only for cell assignment)
            cs.SetBuffer(_kernelBuildSpatialGrid, ShaderProps.BoidsPhysicsRead, physicsReadBuffer);
            cs.SetBuffer(_kernelBuildSpatialGrid, ShaderProps.SpatialGridCounts, _spatialGridCountBuffer);
            cs.SetBuffer(_kernelClearSpatialGrid, ShaderProps.SpatialGridCounts, _spatialGridCountBuffer);

            // â”€â”€ Simulation â”€â”€
            cs.SetInt(ShaderProps.BoidCount, boidCount);
            cs.SetFloat(ShaderProps.DeltaTime, safeDeltaTime);
            cs.SetVector(ShaderProps.SpatialGridOrigin,
                new Vector4(safeGridOrigin.x, safeGridOrigin.y, safeGridOrigin.z, 0f));
            cs.SetVector(ShaderProps.SpatialGridResolution,
                new Vector4(_spatialGridResolution.x, _spatialGridResolution.y, _spatialGridResolution.z, 0f));
            cs.SetFloat(ShaderProps.SpatialGridCellSize, safeGridCellSize);
            cs.SetInt(ShaderProps.SpatialGridMaxBoidsPerCell, SpatialGridMaxBoidsPerCell);
            cs.SetFloat(ShaderProps.BoidMathLodMode, ResolveBoidSocialLodWeight01());

            // â”€â”€ Weights â”€â”€
            cs.SetFloat(ShaderProps.SeparationWeight, ClampFinite(separationWeight, 0f, MaxBoidWeight, 1.5f));
            cs.SetFloat(ShaderProps.AlignmentWeight, ClampFinite(alignmentWeight, 0f, MaxBoidWeight, 1f));
            cs.SetFloat(ShaderProps.CohesionWeight, ClampFinite(cohesionWeight, 0f, MaxBoidWeight, 1f));
            cs.SetFloat(ShaderProps.TargetWeight, ClampFinite(targetWeight, 0f, MaxBoidWeight, 2f));
            cs.SetFloat(ShaderProps.ObstacleWeight, ClampFinite(obstacleWeight, 0f, MaxBoidWeight, 2f));
            cs.SetFloat(ShaderProps.BoundsWeight, ClampFinite(boundsWeight, 0f, MaxBoidWeight, 1f));

            // â”€â”€ Radii â”€â”€
            cs.SetFloat(ShaderProps.PerceptionRadius, ClampFinite(perceptionRadius, 0.001f, MaxBoidRadiusMeters, 5f));
            cs.SetFloat(ShaderProps.SeparationRadius, ClampFinite(separationRadius, 0.001f, MaxBoidRadiusMeters, 2f));
            cs.SetFloat(ShaderProps.ObstacleAvoidRadius, ClampFinite(obstacleAvoidRadius, 0.001f, MaxBoidRadiusMeters, 6f));

            // â”€â”€ Speed â”€â”€
            cs.SetFloat(ShaderProps.MinSpeed, safeMinSpeed);
            cs.SetFloat(ShaderProps.MaxSpeed, safeMaxSpeed);

            // â”€â”€ Target â”€â”€
            cs.SetVector(ShaderProps.TargetPosition,
                new Vector4(safeTargetPosition.x, safeTargetPosition.y, safeTargetPosition.z, 0f));

            // â”€â”€ Bounds â”€â”€
            cs.SetVector(ShaderProps.BoundsCenter,
                new Vector4(safeBoundsCenter.x, safeBoundsCenter.y, safeBoundsCenter.z, 0f));
            cs.SetVector(ShaderProps.BoundsSize,
                new Vector4(safeBoundsSize.x, safeBoundsSize.y, safeBoundsSize.z, 0f));

            // â”€â”€ Heightmap â”€â”€
            Texture2D hmap = heightMap != null ? heightMap : _fallbackHeightMap != null ? _fallbackHeightMap : neutralHeightMap;
            cs.SetTexture(kernel, ShaderProps.HeightMap, hmap);
            cs.SetVector(ShaderProps.WorldOffset,
                new Vector4(safeWorldOffset.x, safeWorldOffset.y, 0f, 0f));
            cs.SetVector(ShaderProps.WorldSize,
                new Vector4(safeWorldSize.x, safeWorldSize.y, 0f, 0f));
            cs.SetFloat(ShaderProps.HeightScaleProp, safeHeightScale);
            cs.SetFloat(ShaderProps.WaterSurfaceY, safeWaterSurfaceY);

            BindCaveSdfPayload(cs, kernel);
            BindAbyssalFlowPayload(cs, kernel);
            BindPanicPayload(cs);
        }

        private void BindCaveSdfPayload(ComputeShader cs, int kernel)
        {
            Texture3D sdfTexture = _fallbackVoxelSdfTexture;
            Matrix4x4 worldToLocal = Matrix4x4.identity;
            Vector4 halfExtentsAndRange = new Vector4(1f, 1f, 1f, 1f);
            Vector4 invDoubleHalfExtents = Vector4.zero;
            float active = 0f;

            HectonCaveVoxelLightingVolume caveVolume = caveSdfOverride;

            if (enableVoxelSdfAvoidance &&
                caveVolume != null &&
                caveVolume.TryGetPublishedGpuSdfPayload(
                    out Texture3D publishedTexture,
                    out Matrix4x4 publishedWorldToLocal,
                    out Vector4 publishedHalfExtentsAndRange,
                    out Vector4 publishedInvDoubleHalfExtents) &&
                publishedTexture != null &&
                IsFiniteMatrix4x4(publishedWorldToLocal) &&
                IsFiniteVector4(publishedHalfExtentsAndRange) &&
                IsFiniteVector4(publishedInvDoubleHalfExtents))
            {
                sdfTexture = publishedTexture;
                worldToLocal = publishedWorldToLocal;
                halfExtentsAndRange = publishedHalfExtentsAndRange;
                invDoubleHalfExtents = publishedInvDoubleHalfExtents;
                active = 1f;
            }

            cs.SetTexture(kernel, ShaderProps.CaveVoxelSdfTex, sdfTexture);
            cs.SetMatrix(ShaderProps.CaveVoxelWorldToLocal, worldToLocal);
            cs.SetVector(ShaderProps.CaveVoxelHalfExtents, halfExtentsAndRange);
            cs.SetVector(ShaderProps.CaveVoxelInvDoubleHalfExtents, invDoubleHalfExtents);
            cs.SetFloat(ShaderProps.CaveVoxelActive, active);
            cs.SetFloat(ShaderProps.CaveVoxelWeight, ClampFinite(voxelSdfWeight, 0f, MaxBoidWeight, 1.35f));
        }

        private void BindAbyssalFlowPayload(ComputeShader cs, int kernel)
        {
            GraphicsBuffer flowBuffer = _fallbackFlowFieldBuffer;
            Texture flowTexture = _fallbackAbyssalFlowTexture;
            Vector4 gridResolution = Vector4.zero;
            Vector4 flowCenter = Vector4.zero;
            Vector4 flowSpacing = Vector4.zero;
            float active = 0f;

            IAbyssalFlowGpuReadModel fluid = _fluidRuntime;
            if (enableAbyssalFlowAdvection && fluid != null)
            {
                if (fluid.TryGetGpuAbyssalFlowFieldTexture(
                    out Texture publishedFlowTexture,
                    out Vector4 publishedTextureGridResolution,
                    out Vector4 publishedTextureFlowCenter,
                    out Vector4 publishedTextureFlowSpacing) &&
                    publishedFlowTexture != null &&
                    IsFiniteVector4(publishedTextureGridResolution) &&
                    IsFiniteVector4(publishedTextureFlowCenter) &&
                    IsFiniteVector4(publishedTextureFlowSpacing))
                {
                    flowTexture = publishedFlowTexture;
                    gridResolution = publishedTextureGridResolution;
                    flowCenter = publishedTextureFlowCenter;
                    flowSpacing = publishedTextureFlowSpacing;
                    active = 1f;
                }

                if (fluid.TryGetGpuAbyssalFlowFieldBuffer(
                    out GraphicsBuffer publishedFlowBuffer,
                    out Vector4 publishedGridResolution,
                    out Vector4 publishedFlowCenter,
                    out Vector4 publishedFlowSpacing) &&
                    publishedFlowBuffer != null &&
                    IsFiniteVector4(publishedGridResolution) &&
                    IsFiniteVector4(publishedFlowCenter) &&
                    IsFiniteVector4(publishedFlowSpacing))
                {
                    flowBuffer = publishedFlowBuffer;
                    if (active <= 0f)
                    {
                        gridResolution = publishedGridResolution;
                        flowCenter = publishedFlowCenter;
                        flowSpacing = publishedFlowSpacing;
                        flowSpacing.w = 0f;
                        active = 1f;
                    }
                }
            }

            cs.SetBuffer(kernel, ShaderProps.AbyssalFlowFieldResult, flowBuffer);
            cs.SetTexture(kernel, ShaderProps.AbyssalFlowFieldTexture, flowTexture);
            cs.SetVector(ShaderProps.AbyssalGridResolution, gridResolution);
            cs.SetVector(ShaderProps.AbyssalFlowCenter, flowCenter);
            cs.SetVector(ShaderProps.AbyssalFlowSpacing, flowSpacing);
            cs.SetFloat(ShaderProps.AbyssalFlowActive, active);
            cs.SetFloat(ShaderProps.AbyssalFlowWeight, ClampFinite(abyssalFlowWeight, 0f, MaxBoidWeight, 0.35f));
        }

        private void BindPanicPayload(ComputeShader cs)
        {
            float predatorRadius = ClampFinite(predatorPanicRadius, 0.001f, MaxBoidRadiusMeters, 18f);
            cs.SetVectorArray(ShaderProps.PredatorRuntimePositions, _predatorRuntimePositions);
            cs.SetInt(ShaderProps.PredatorCount, Mathf.Clamp(_predatorRuntimePositionCount, 0, MaxPredatorRuntimePositions));
            cs.SetFloat(ShaderProps.PredatorPanicRadiusSq, predatorRadius * predatorRadius);
            cs.SetFloat(ShaderProps.PredatorWeight, ClampFinite(predatorEvasionWeight, 0f, MaxBoidWeight, 1f));

            Vector4 acousticRuntimeRadius = IsFiniteVector4(_activeAcousticPingRuntimeRadius) ? _activeAcousticPingRuntimeRadius : Vector4.zero;
            Vector4 acousticParams = IsFiniteVector4(_activeAcousticPingParams) ? _activeAcousticPingParams : Vector4.zero;
            float clock = ClampMinFinite(ResolveBoidClockSeconds(), 0f, 0f);
            float pingActive = acousticParams.x > 0.0001f && clock <= acousticParams.z ? 1f : 0f;
            acousticParams.w = pingActive;
            if (pingActive <= 0f)
            {
                acousticRuntimeRadius = Vector4.zero;
                acousticParams = Vector4.zero;
            }

            _activeAcousticPingRuntimeRadius = acousticRuntimeRadius;
            _activeAcousticPingParams = acousticParams;
            cs.SetVector(ShaderProps.AcousticPingRuntimeRadius, acousticRuntimeRadius);
            cs.SetVector(ShaderProps.AcousticPingParams, acousticParams);
            cs.SetFloat(ShaderProps.PanicDecay, ClampFinite(panicDecayPerSecond, 0f, MaxBoidWeight, 2.5f));
            float accelerationThreshold = ClampFinite(panicAccelerationThreshold, 0.001f, MaxBoidSpeedMetersPerSecond, 14f);
            cs.SetFloat(ShaderProps.PanicAccelerationThresholdSq, accelerationThreshold * accelerationThreshold);
        }

        private void DispatchSpatialGridBuild()
        {
            if (_clearSpatialGridGroupCount <= 0 || _buildSpatialGridGroupCount <= 0)
                return;

            boidShader.Dispatch(_kernelClearSpatialGrid, _clearSpatialGridGroupCount, 1, 1);
            boidShader.Dispatch(_kernelBuildSpatialGrid, _buildSpatialGridGroupCount, 1, 1);
            uint nextPowerOfTwo = (uint)Mathf.NextPowerOfTwo(boidCount);
            uint numStages = (uint)Mathf.Log(nextPowerOfTwo, 2);
            for (uint stage = 0; stage < numStages; stage++)
            {
                for (uint passOfStage = 0; passOfStage <= stage; passOfStage++)
                {
                    uint stepIndex = (stage - passOfStage);
                    uint step = 1u << (int)stepIndex;
                    uint block = 2u * step;
                    boidShader.SetInt(ShaderProps.BitonicBlock, (int)block);
                    boidShader.SetInt(ShaderProps.BitonicStep, (int)step);
                    int sortGroups = Mathf.CeilToInt(nextPowerOfTwo / 512f);
                    boidShader.Dispatch(_kernelBitonicSort, sortGroups, 1, 1);
                }
            }
            if (_computeCellOffsetsGroupCount > 0)
            {
                boidShader.Dispatch(_kernelComputeCellOffsets, _computeCellOffsetsGroupCount, 1, 1);
            }
        }

        private bool PopulateGpuFrustumPlanes()
        {
            if (!TryResolveViewCamera())
                return false;

            GeometryUtility.CalculateFrustumPlanes(_mainCamera, _frustumPlanes);
            for (int i = 0; i < 6; i++)
            {
                Plane plane = _frustumPlanes[i];
                Vector3 normal = plane.normal;
                _cameraFrustumPlaneUpload[i] = new Vector4(normal.x, normal.y, normal.z, plane.distance);
            }

            return true;
        }

        private void DispatchComputeFrustumCulling(GraphicsBuffer currentDataBuffer)
        {
            if (currentDataBuffer == null || _visibleBoidIndexBuffer == null || _visibleIndirectArgsBuffer == null)
                return;

            UploadIndirectArgsStaticMeshData();
            bool hasCameraFrustum = PopulateGpuFrustumPlanes();
            if (_clearVisibleIndirectArgsGroupCount <= 0)
                return;

            if (_cullVisibleBoidsGroupCount <= 0)
            {
                boidShader.SetBuffer(_kernelClearVisibleIndirectArgs, ShaderProps.VisibleIndirectArgs, _visibleIndirectArgsBuffer);
                boidShader.Dispatch(_kernelClearVisibleIndirectArgs, _clearVisibleIndirectArgsGroupCount, 1, 1);
                return;
            }

            boidShader.SetBuffer(_kernelClearVisibleIndirectArgs, ShaderProps.VisibleIndirectArgs, _visibleIndirectArgsBuffer);
            // CullVisibleBoids: uses _BoidsPhysicsRead (position only for frustum test)
            boidShader.SetBuffer(_kernelCullVisibleBoids, ShaderProps.BoidsPhysicsRead, (_frameIndex % 2 == 0) ? _boidsPhysicsBufferA : _boidsPhysicsBufferB);
            boidShader.SetBuffer(_kernelCullVisibleBoids, ShaderProps.VisibleBoidIndices, _visibleBoidIndexBuffer);
            boidShader.SetBuffer(_kernelCullVisibleBoids, ShaderProps.VisibleIndirectArgs, _visibleIndirectArgsBuffer);
            boidShader.SetInt(ShaderProps.BoidCount, boidCount);
            boidShader.SetVectorArray(ShaderProps.CameraFrustumPlanes, _cameraFrustumPlaneUpload);
            float safeFishScale = ClampFinite(fishScale, 0.001f, MaxBoidRadiusMeters, 0.4f);
            boidShader.SetFloat(ShaderProps.BoidCullingRadius, math.max(0.01f, safeFishScale * DefaultBoidCullingRadiusScale));
            boidShader.SetInt(ShaderProps.GpuFrustumCullingActive, hasCameraFrustum ? 1 : 0);
            boidShader.Dispatch(_kernelClearVisibleIndirectArgs, _clearVisibleIndirectArgsGroupCount, 1, 1);
            boidShader.Dispatch(_kernelCullVisibleBoids, _cullVisibleBoidsGroupCount, 1, 1);
        }

        private void UpdateSpatialGridLayout()
        {
            Vector3 safeBoundsCenter = ClampFiniteVector3(boundsCenter, -RuntimeVectorComponentLimitMeters, RuntimeVectorComponentLimitMeters, Vector3.zero);
            Vector3 safeBoundsSize = ResolveSafeBoundsSize();
            float safePerceptionRadius = ClampFinite(perceptionRadius, 0.001f, MaxBoidRadiusMeters, 5f);
            float safeSeparationRadius = ClampFinite(separationRadius, 0.001f, MaxBoidRadiusMeters, 2f);
            float baseCellSize = math.max(math.max(safePerceptionRadius, safeSeparationRadius), 0.001f);
            Vector3 doubledExtents = safeBoundsSize * 2f;
            Vector3 fieldSize = new Vector3(
                math.max(doubledExtents.x, baseCellSize),
                math.max(doubledExtents.y, baseCellSize),
                math.max(doubledExtents.z, baseCellSize));
            float axisClampCellSize = math.max(
                fieldSize.x / SpatialGridMaxAxisResolution,
                math.max(fieldSize.y / SpatialGridMaxAxisResolution, fieldSize.z / SpatialGridMaxAxisResolution));
            _spatialGridCellSize = math.max(baseCellSize, axisClampCellSize);

            Vector3 fieldMin = safeBoundsCenter - safeBoundsSize;
            Vector3 fieldMax = safeBoundsCenter + safeBoundsSize;
            _spatialGridOrigin = new Vector3(
                FloorToMultiple(fieldMin.x, _spatialGridCellSize),
                FloorToMultiple(fieldMin.y, _spatialGridCellSize),
                FloorToMultiple(fieldMin.z, _spatialGridCellSize));

            int resolutionX = Mathf.Clamp(CeilToIntPositive((fieldMax.x - _spatialGridOrigin.x) / _spatialGridCellSize), 1, SpatialGridMaxAxisResolution);
            int resolutionY = Mathf.Clamp(CeilToIntPositive((fieldMax.y - _spatialGridOrigin.y) / _spatialGridCellSize), 1, SpatialGridMaxAxisResolution);
            int resolutionZ = Mathf.Clamp(CeilToIntPositive((fieldMax.z - _spatialGridOrigin.z) / _spatialGridCellSize), 1, SpatialGridMaxAxisResolution);
            _spatialGridResolution = new Vector3Int(resolutionX, resolutionY, resolutionZ);

            int cellCount = resolutionX * resolutionY * resolutionZ;
            _clearSpatialGridGroupCount = CeilDivPositive(cellCount, _clearSpatialGridThreadGroupSizeX);
        }

        private bool TryResolveKernel(string kernelName, out int kernelIndex)
        {
            kernelIndex = -1;
            if (boidShader == null || !SystemInfo.supportsComputeShaders)
                return false;

            try
            {
                if (!boidShader.HasKernel(kernelName))
                    return false;

                kernelIndex = boidShader.FindKernel(kernelName);
                return kernelIndex >= 0;
            }
            catch (ObjectDisposedException)
            {
                kernelIndex = -1;
                return false;
            }
            catch (InvalidOperationException)
            {
                kernelIndex = -1;
                return false;
            }
            catch (ArgumentException)
            {
                kernelIndex = -1;
                return false;
            }
            catch (MissingReferenceException)
            {
                kernelIndex = -1;
                return false;
            }
            catch (UnityException)
            {
                kernelIndex = -1;
                return false;
            }
        }

        private void ResetDispatchGroupSizes()
        {
            _threadGroupSizeX = 0;
            _clearSpatialGridThreadGroupSizeX = 0;
            _buildSpatialGridThreadGroupSizeX = 0;
            _clearVisibleIndirectArgsThreadGroupSizeX = 0;
            _cullVisibleBoidsThreadGroupSizeX = 0;
            RefreshDispatchGroupCounts();
        }

        private bool TryResolveThreadGroupSizeX(int kernelIndex, out int groupSizeX)
        {
            groupSizeX = 0;
            if (boidShader == null ||
                kernelIndex < 0 ||
                !SystemInfo.supportsComputeShaders)
                return false;

            uint x;
            uint y;
            uint z;
            try
            {
                if (!boidShader.IsSupported(kernelIndex))
                    return false;

                boidShader.GetKernelThreadGroupSizes(kernelIndex, out x, out y, out z);
            }
            catch (ObjectDisposedException)
            {
                ResetDispatchGroupSizes();
                return false;
            }
            catch (InvalidOperationException)
            {
                ResetDispatchGroupSizes();
                return false;
            }
            catch (ArgumentException)
            {
                ResetDispatchGroupSizes();
                return false;
            }
            catch (MissingReferenceException)
            {
                ResetDispatchGroupSizes();
                return false;
            }
            catch (UnityException)
            {
                ResetDispatchGroupSizes();
                return false;
            }

            ulong totalThreads = (ulong)x * y * z;
            if (x == 0u || y != 1u || z != 1u || totalThreads > ThreadGroupPortableMaxSize || x > 2147483647u)
            {
                ResetDispatchGroupSizes();
                return false;
            }

            groupSizeX = (int)x;
            return true;
        }

        private void RefreshDispatchGroupCounts()
        {
            _dispatchGroupCount = CeilDivPositive(boidCount, _threadGroupSizeX);
            _buildSpatialGridGroupCount = CeilDivPositive(Mathf.NextPowerOfTwo(boidCount), _buildSpatialGridThreadGroupSizeX);
            _computeCellOffsetsGroupCount = CeilDivPositive(boidCount, _computeCellOffsetsThreadGroupSizeX);
            _cullVisibleBoidsGroupCount = CeilDivPositive(boidCount, _cullVisibleBoidsThreadGroupSizeX);
            _clearSpatialGridGroupCount = CeilDivPositive(SpatialGridMaxCellCount, _clearSpatialGridThreadGroupSizeX);
            _clearVisibleIndirectArgsGroupCount = CeilDivPositive(1, _clearVisibleIndirectArgsThreadGroupSizeX);
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  RENDERING
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>
        /// Issues indirect instanced draw call.
        /// ONE draw call for ALL boids.
        ///
        /// PING-PONG RENDER BINDING:
        ///   After Dispatch + _frameIndex++, we need to render from the buffer
        ///   that was WRITTEN TO during this frame's dispatch.
        ///
        ///   Before increment: writeBuffer = (_frameIndex % 2 == 0) ? B : A
        ///   After  increment: _frameIndex is now +1, so:
        ///     currentData = (_frameIndex % 2 == 0) ? B : A
        ///   This correctly points to the buffer that was just written.
        ///
        /// Graphics.RenderMeshIndirect (Unity 6):
        ///   - Reads visible instance count from GPU-written args buffer.
        ///   - Vertex shader maps SV_InstanceID through _VisibleBoidIndices.
        ///   - Zero CPU overhead for transforms.
        ///
        /// The fish material's vertex shader must:
        ///   1. Declare StructuredBuffer&lt;BoidData&gt; _BoidsBuffer.
        ///   2. In vert(): read _BoidsBuffer[unity_InstanceID].
        ///   3. Construct rotation from velocity (LookRotation).
        ///   4. Apply position + rotation + scale to vertex position.
        /// </summary>
        private void RenderBoids()
        {
            if (fishMesh == null || fishMaterial == null || _visibleIndirectArgsBuffer == null || _visibleBoidIndexBuffer == null)
                return;

            GraphicsBuffer currentDataBuffer = (_frameIndex % 2 == 0) ? _boidsBufferA : _boidsBufferB;

            BindRenderMaterialState(currentDataBuffer);

            // Update world bounds in case center moved
            _renderParams.worldBounds = _simulationBounds;

            UnityEngine.Graphics.RenderMeshIndirect(_renderParams, fishMesh, _visibleIndirectArgsBuffer, 1, 0);
        }

        private void BindRenderMaterialState(GraphicsBuffer currentDataBuffer)
        {
            _renderMaterialProperties.SetBuffer(ShaderProps.BoidsBuffer, currentDataBuffer);
            _renderMaterialProperties.SetBuffer(ShaderProps.VisibleBoidIndices, _visibleBoidIndexBuffer);
            _renderMaterialProperties.SetFloat(ShaderProps.BoidUseVisibleIndices, 1f);
            _renderMaterialProperties.SetFloat(ShaderProps.FishScale, ClampFinite(fishScale, 0.001f, MaxBoidRadiusMeters, 0.4f));
            _renderMaterialProperties.SetFloat(ShaderProps.FoveatedVatTimeScale, ResolveFoveatedVatTimeScale());
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  FRUSTUM CULLING
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>
        /// Tests simulation AABB against camera frustum.
        ///
        /// Uses pre-allocated Plane[6] array â€” zero GC.
        /// GeometryUtility.CalculateFrustumPlanes fills array in-place.
        /// GeometryUtility.TestPlanesAABB â€” struct math, zero GC.
        ///
        /// If camera is not found â€” assumes visible (safety).
        /// </summary>


        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  TARGET TRACKING
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>
        /// Updates target position to follow player in full 3D.
        /// Falls back to boundsCenter if player not found.
        ///
        /// DEPTH TRACKING:
        ///   boundsCenter ÑÐ»ÐµÐ´ÑƒÐµÑ‚ Ð·Ð° Ð¸Ð³Ñ€Ð¾ÐºÐ¾Ð¼ Ð¿Ð¾ Ð²ÑÐµÐ¼ Ñ‚Ñ€Ñ‘Ð¼ Ð¾ÑÑÐ¼ (X, Y, Z).
        ///   ÐžÐ³Ñ€Ð°Ð½Ð¸Ñ‡ÐµÐ½Ð¸Ðµ Ð¿Ð¾ Y: Ð²ÐµÑ€Ñ…Ð½ÑÑ Ð³Ñ€Ð°Ð½Ð¸Ñ†Ð° Ð±Ð¾ÐºÑÐ° (center.y + boundsSize.y)
        ///   Ð½Ðµ Ð¼Ð¾Ð¶ÐµÑ‚ Ð¿Ñ€ÐµÐ²Ñ‹ÑˆÐ°Ñ‚ÑŒ waterSurfaceY.
        ///   maxCenterY = waterSurfaceY - boundsSize.y
        ///   targetY = min(playerY, maxCenterY)
        ///
        ///   Ð­Ñ‚Ð¾ Ð³Ð°Ñ€Ð°Ð½Ñ‚Ð¸Ñ€ÑƒÐµÑ‚:
        ///     â€¢ Ð¡Ñ‚Ð°Ñ Ð¿Ð¾Ð³Ñ€ÑƒÐ¶Ð°ÐµÑ‚ÑÑ Ð²Ð¼ÐµÑÑ‚Ðµ Ñ Ð¸Ð³Ñ€Ð¾ÐºÐ¾Ð¼.
        ///     â€¢ Ð Ñ‹Ð±Ñ‹ Ð½Ð¸ÐºÐ¾Ð³Ð´Ð° Ð½Ðµ Ð¿Ñ€Ð¾Ð±Ð¸Ð²Ð°ÑŽÑ‚ Ð¿Ð¾Ð²ÐµÑ€Ñ…Ð½Ð¾ÑÑ‚ÑŒ Ð²Ð¾Ð´Ñ‹.
        ///     â€¢ ÐŸÑ€Ð¸ Ð¿Ð»Ð°Ð²Ð°Ð½Ð¸Ð¸ Ñƒ Ð¿Ð¾Ð²ÐµÑ€Ñ…Ð½Ð¾ÑÑ‚Ð¸ â€” Ð±Ð¾ÐºÑ Ð¿Ñ€Ð¸Ð¶Ð°Ñ‚ Ðº Ð²Ð¾Ð´Ðµ ÑÐ²ÐµÑ€Ñ…Ñƒ.
        /// </summary>
        private void UpdateTarget()
        {
            if (TryResolvePlayerTargetPosition(out Vector3 playerPosition))
            {
                _targetPosition = ClampFiniteVector3(playerPosition, -RuntimeVectorComponentLimitMeters, RuntimeVectorComponentLimitMeters, Vector3.zero);

                // Ð”Ð¸Ð½Ð°Ð¼Ð¸Ñ‡ÐµÑÐºÐ¸Ðµ Ð³Ñ€Ð°Ð½Ð¸Ñ†Ñ‹: Ñ†ÐµÐ½Ñ‚Ñ€ ÑÐ»ÐµÐ´ÑƒÐµÑ‚ Ð·Ð° Ð¸Ð³Ñ€Ð¾ÐºÐ¾Ð¼ Ð¿Ð¾ X, Y Ð¸ Z.
                // ÐžÐ³Ñ€Ð°Ð½Ð¸Ñ‡Ð¸Ð²Ð°ÐµÐ¼ Y, Ñ‡Ñ‚Ð¾Ð±Ñ‹ Ð²ÐµÑ€Ñ…Ð½ÑÑ Ð³Ñ€Ð°Ð½Ð¸Ñ†Ð° Ð±Ð¾ÐºÑÐ° (center.y + boundsSize.y)
                // Ð½Ðµ Ð¿Ñ€Ð¾Ð±Ð¸Ð²Ð°Ð»Ð° Ð¿Ð¾Ð²ÐµÑ€Ñ…Ð½Ð¾ÑÑ‚ÑŒ Ð²Ð¾Ð´Ñ‹ (waterSurfaceY).
                Vector3 safeBoundsSize = ResolveSafeBoundsSize();
                float safeWaterSurfaceY = ClampFinite(waterSurfaceY, -RuntimeVectorComponentLimitMeters, RuntimeVectorComponentLimitMeters, 0f);
                float maxCenterY = safeWaterSurfaceY - safeBoundsSize.y;
                float targetY    = math.min(_targetPosition.y, maxCenterY);

                boundsCenter = new Vector3(
                    _targetPosition.x,
                    targetY,
                    _targetPosition.z);

                _simulationBounds.center = boundsCenter;
            }
            else
            {
                _targetPosition = ClampFiniteVector3(boundsCenter, -RuntimeVectorComponentLimitMeters, RuntimeVectorComponentLimitMeters, Vector3.zero);
            }
        }

        /// <summary>
        /// Resolves the player transform through the shared runtime path.
        /// </summary>
        private void FindPlayer()
        {
            _playerRuntimeContext = ResolvePlayerContext();
            _playerTransform = _playerRuntimeContext != null ? _playerRuntimeContext.PlayerTransform : null;
        }

        private IPlayerRuntimeContext ResolvePlayerContext()
        {
            return _playerRuntimeContext;
        }

        private void CacheRegistryServicesCold(bool forceRefresh)
        {
            if (forceRefresh || _playerRuntimeContext == null)
            {
                _playerRuntimeContext = Hecton8.Core.GlobalRegistry.Player;
                _playerTransform = _playerRuntimeContext != null ? _playerRuntimeContext.PlayerTransform : _playerTransform;
                _mainCamera = null;
            }

            if (forceRefresh || _fluidRuntime == null)
                _fluidRuntime = GlobalRegistry.AbyssalFlowGpu;

            if (forceRefresh || _foveatedSimulationDirector == null)
                _foveatedSimulationDirector = GlobalRegistry.FoveatedSimulationDirector;

            if (forceRefresh || _dataVault == null)
                _dataVault = GlobalRegistry.DataVault;
        }

        private bool EnsureBoidBlackBoxCold()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            if (IsVaultHandleCreated(in _boidBlackBoxHandle) &&
                vault.TryResolveHandle(in _boidBlackBoxHandle, out NativeArray<BoidBlackBoxEntry> existing) &&
                existing.IsCreated &&
                existing.Length >= BoidBlackBoxFrameCount)
            {
                return true;
            }

            _boidBlackBoxHandle = vault.EnsureGenerationHandle<BoidBlackBoxEntry>(
                BoidBlackBoxBufferId,
                BoidBlackBoxFrameCount,
                SystemID.AIEcology,
                NativeArrayOptions.ClearMemory);

            return IsVaultHandleCreated(in _boidBlackBoxHandle) &&
                   vault.TryResolveHandle(in _boidBlackBoxHandle, out NativeArray<BoidBlackBoxEntry> blackBox) &&
                   blackBox.IsCreated &&
                   blackBox.Length >= BoidBlackBoxFrameCount;
        }

        private void ReleaseBoidBlackBoxHandle(IDataVault vault)
        {
            if (vault == null || !IsVaultHandleCreated(in _boidBlackBoxHandle))
                return;

            vault.ReleaseBuffer(in _boidBlackBoxHandle);
            _boidBlackBoxHandle = default;
        }

        private static bool IsVaultHandleCreated<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            return handle.BufferID != 0u && handle.Generation != 0u;
        }

        private void WriteBoidBlackBoxFrame(float deltaTime, bool simulateBoids, uint forcedFlags)
        {
            IDataVault vault = _dataVault;
            if (vault == null || !IsVaultHandleCreated(in _boidBlackBoxHandle))
                return;

            uint flags = forcedFlags | ResolveBoidBlackBoxFaultFlags(deltaTime);
            if (_initialized)
                flags |= BoidBlackBoxFlagInitialized;
            if (simulateBoids)
                flags |= BoidBlackBoxFlagSimulated;
            if (HasRuntimeBuffersReady())
                flags |= BoidBlackBoxFlagBuffersReady;
            if (_visibleBoidIndexBuffer != null)
                flags |= BoidBlackBoxFlagVisibleIndices;

            if (!vault.TryAcquireWriteLock(in _boidBlackBoxHandle, SystemID.AIEcology, out NativeArray<BoidBlackBoxEntry> blackBox))
                return;

            NativeArray<byte> dumpPayload = default;
            int dumpByteCount = 0;
            try
            {
                if (!blackBox.IsCreated || blackBox.Length < BoidBlackBoxFrameCount)
                    return;

                int index = _boidBlackBoxCursor;
                blackBox[index] = BuildBoidBlackBoxEntry(deltaTime, flags);
                _boidBlackBoxCursor = (index + 1) % blackBox.Length;
                if (_boidBlackBoxWritten < blackBox.Length)
                    _boidBlackBoxWritten++;

                if ((flags & BoidBlackBoxFaultMask) != 0u && !_boidBlackBoxDumped)
                {
                    TryStageBoidBlackBoxDump(blackBox, _boidBlackBoxCursor, _boidBlackBoxWritten, out dumpPayload, out dumpByteCount);
                }
            }
            finally
            {
                vault.ReleaseWriteLock(in _boidBlackBoxHandle, SystemID.AIEcology);
            }

            if (!dumpPayload.IsCreated)
                return;

            try
            {
                _boidBlackBoxDumped = NativeFaultDumpWriter.TryWriteAll(
                    ResolveBoidBlackBoxDumpPath(),
                    dumpPayload,
                    dumpByteCount);
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref dumpPayload,
                    nameof(HectonBoidController),
                    "boidBlackBoxDumpPayload");
            }
        }

        private BoidBlackBoxEntry BuildBoidBlackBoxEntry(float deltaTime, uint flags)
        {
            float globalQualityWeight = ResolveGlobalQualityWeight01();
            float socialLodWeight = ResolveBoidSocialLodWeight01();
            Vector4 acousticParams = IsFiniteVector4(_activeAcousticPingParams) ? _activeAcousticPingParams : Vector4.zero;
            Vector4 acousticRuntimeRadius = IsFiniteVector4(_activeAcousticPingRuntimeRadius) ? _activeAcousticPingRuntimeRadius : Vector4.zero;
            BoidBlackBoxEntry entry = default;
            entry.Frame = (uint)SystemDispatcher.CurrentFrameIndex;
            entry.Flags = flags;
            entry.BoidCount = boidCount;
            entry.TargetPosition = ClampFiniteVector3(_targetPosition, -RuntimeVectorComponentLimitMeters, RuntimeVectorComponentLimitMeters, Vector3.zero);
            entry.DeltaTime = ClampMinFinite(deltaTime, 0f, 0f);
            entry.BoundsCenter = ClampFiniteVector3(boundsCenter, -RuntimeVectorComponentLimitMeters, RuntimeVectorComponentLimitMeters, Vector3.zero);
            entry.BoidClockSeconds = ClampMinFinite(_boidClockSeconds, 0f, 0f);
            entry.SpatialGridOrigin = ClampFiniteVector3(_spatialGridOrigin, -RuntimeVectorComponentLimitMeters, RuntimeVectorComponentLimitMeters, Vector3.zero);
            entry.SpatialGridCellSize = ClampMinFinite(_spatialGridCellSize, 0f, 0f);
            entry.SpatialGridResolutionX = _spatialGridResolution.x;
            entry.SpatialGridResolutionY = _spatialGridResolution.y;
            entry.SpatialGridResolutionZ = _spatialGridResolution.z;
            entry.DispatchGroupCount = _dispatchGroupCount;
            entry.PredatorCount = _predatorRuntimePositionCount;
            entry.FoveatedTier = (int)_foveatedSimulationTier;
            entry.GlobalQualityWeight = globalQualityWeight;
            entry.SocialLodWeight = socialLodWeight;
            entry.AcousticPingParams = acousticParams;
            entry.AcousticPingRuntimeRadius = acousticRuntimeRadius;
            entry.StateHash = HashBoidBlackBoxState(in entry);
            return entry;
        }

        private uint ResolveBoidBlackBoxFaultFlags(float deltaTime)
        {
            uint flags = 0u;
            if (!math.isfinite(deltaTime) || deltaTime < 0f)
                flags |= BoidBlackBoxFaultInvalidDeltaTime;
            if (!TryToFiniteVector3(_targetPosition, out _))
                flags |= BoidBlackBoxFaultInvalidTarget;
            if (!TryToFiniteVector3(boundsCenter, out _) || !TryToFiniteVector3(boundsSize, out _))
                flags |= BoidBlackBoxFaultInvalidBounds;
            if (!TryToFiniteVector3(_spatialGridOrigin, out _) ||
                !math.isfinite(_spatialGridCellSize) ||
                _spatialGridCellSize <= 0f ||
                _spatialGridResolution.x <= 0 ||
                _spatialGridResolution.y <= 0 ||
                _spatialGridResolution.z <= 0 ||
                _spatialGridResolution.x > SpatialGridMaxAxisResolution ||
                _spatialGridResolution.y > SpatialGridMaxAxisResolution ||
                _spatialGridResolution.z > SpatialGridMaxAxisResolution ||
                _dispatchGroupCount < 0)
            {
                flags |= BoidBlackBoxFaultInvalidGrid;
            }

            if (!math.isfinite(_boidClockSeconds) || _boidClockSeconds < 0f)
                flags |= BoidBlackBoxFaultInvalidClock;
            if (boidCount <= 0 || boidCount > 8192)
                flags |= BoidBlackBoxFaultInvalidPopulation;
            if (!IsFiniteVector4(_activeAcousticPingRuntimeRadius) || !IsFiniteVector4(_activeAcousticPingParams))
                flags |= BoidBlackBoxFaultInvalidAcoustic;
            return flags;
        }

        private static bool IsFiniteVector4(Vector4 value)
        {
            return math.isfinite(value.x) &&
                   math.isfinite(value.y) &&
                   math.isfinite(value.z) &&
                   math.isfinite(value.w);
        }

        private static uint HashBoidBlackBoxState(in BoidBlackBoxEntry entry)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = MixHash(hash, entry.Frame);
                hash = MixHash(hash, entry.Flags);
                hash = MixHash(hash, (uint)entry.BoidCount);
                hash = MixHash(hash, QuantizeFloatForHash(entry.TargetPosition.x));
                hash = MixHash(hash, QuantizeFloatForHash(entry.TargetPosition.y));
                hash = MixHash(hash, QuantizeFloatForHash(entry.TargetPosition.z));
                hash = MixHash(hash, QuantizeFloatForHash(entry.BoundsCenter.x));
                hash = MixHash(hash, QuantizeFloatForHash(entry.BoundsCenter.y));
                hash = MixHash(hash, QuantizeFloatForHash(entry.BoundsCenter.z));
                hash = MixHash(hash, QuantizeFloatForHash(entry.SpatialGridOrigin.x));
                hash = MixHash(hash, QuantizeFloatForHash(entry.SpatialGridOrigin.y));
                hash = MixHash(hash, QuantizeFloatForHash(entry.SpatialGridOrigin.z));
                hash = MixHash(hash, (uint)entry.SpatialGridResolutionX);
                hash = MixHash(hash, (uint)entry.SpatialGridResolutionY);
                hash = MixHash(hash, (uint)entry.SpatialGridResolutionZ);
                hash = MixHash(hash, (uint)entry.DispatchGroupCount);
                hash = MixHash(hash, (uint)entry.PredatorCount);
                hash = MixHash(hash, (uint)entry.FoveatedTier);
                hash = MixHash(hash, QuantizeFloatForHash(entry.GlobalQualityWeight));
                hash = MixHash(hash, QuantizeFloatForHash(entry.SocialLodWeight));
                hash = MixHash(hash, QuantizeFloatForHash(entry.AcousticPingParams.x));
                hash = MixHash(hash, QuantizeFloatForHash(entry.AcousticPingParams.y));
                hash = MixHash(hash, QuantizeFloatForHash(entry.AcousticPingParams.z));
                hash = MixHash(hash, QuantizeFloatForHash(entry.AcousticPingParams.w));
                return hash != 0u ? hash : 1u;
            }
        }

        private static uint MixHash(uint hash, uint value)
        {
            unchecked
            {
                return (hash ^ value) * 16777619u;
            }
        }

        private static uint QuantizeFloatForHash(float value)
        {
            if (!math.isfinite(value))
                return 0xFFFFFFFFu;

            float scaled = value * 1000f;
            if (!math.isfinite(scaled))
                return 0xFFFFFFFEu;

            scaled = math.clamp(scaled, int.MinValue + 1f, int.MaxValue - 1f);
            return unchecked((uint)Mathf.RoundToInt(scaled));
        }

        private static unsafe bool TryStageBoidBlackBoxDump(
            NativeArray<BoidBlackBoxEntry> blackBox,
            int cursor,
            int written,
            out NativeArray<byte> payload,
            out int byteCount)
        {
            payload = default;
            byteCount = 0;

            if (!blackBox.IsCreated)
                return false;

            int capacity = blackBox.Length;
            if (capacity <= 0)
                return false;

            int dumpCount = Math.Min(capacity, Math.Max(0, written));
            int start = written < capacity ? 0 : PositiveMod(cursor, capacity);
            byteCount = 28 + dumpCount * BoidBlackBoxStride;

            try
            {
                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    byteCount,
                    nameof(HectonBoidController),
                    "boidBlackBoxDumpPayload");
                byte* target = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(payload);
                byte* source = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(blackBox);

                WriteUInt32LittleEndian(target, 0, BoidBlackBoxDumpMagic);
                WriteUInt32LittleEndian(target, 4, BoidBlackBoxDumpVersion);
                WriteInt32LittleEndian(target, 8, BoidBlackBoxStride);
                WriteInt32LittleEndian(target, 12, capacity);
                WriteInt32LittleEndian(target, 16, dumpCount);
                WriteInt32LittleEndian(target, 20, cursor);
                WriteInt32LittleEndian(target, 24, start);

                int payloadOffset = 28;
                for (int i = 0; i < dumpCount; i++)
                {
                    int index = (start + i) % capacity;
                    UnsafeUtility.MemCpy(
                        target + payloadOffset + i * BoidBlackBoxStride,
                        source + index * BoidBlackBoxStride,
                        BoidBlackBoxStride);
                }

                return true;
            }
            catch (Exception)
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(HectonBoidController),
                    "boidBlackBoxDumpPayload");

                payload = default;
                byteCount = 0;
                return false;
            }
        }

        private static int PositiveMod(int value, int divisor)
        {
            int result = value % divisor;
            return result < 0 ? result + divisor : result;
        }

        private static unsafe void WriteInt32LittleEndian(byte* target, int offset, int value)
        {
            WriteUInt32LittleEndian(target, offset, unchecked((uint)value));
        }

        private static unsafe void WriteUInt32LittleEndian(byte* target, int offset, uint value)
        {
            target[offset] = (byte)value;
            target[offset + 1] = (byte)(value >> 8);
            target[offset + 2] = (byte)(value >> 16);
            target[offset + 3] = (byte)(value >> 24);
        }

        private static string ResolveBoidBlackBoxDumpPath()
        {
            return BoidBlackBoxDumpRelativePath;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapListenerRegistered)
                return;

            _hotSwapListenerRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapListenerRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapListenerRegistered = false;
        }

        private static float ResolveBoidSocialLodWeight01()
        {
            float qualityWeight = ResolveGlobalQualityWeight01();
            return math.saturate(math.smoothstep(0.2f, 0.85f, qualityWeight));
        }

        private static float ResolveGlobalQualityWeight01()
        {
            float qualityWeight = MathLodRuntimeConfig.TryReadLatestConfig(out MathLodConfigDTO config)
                ? config.GlobalQualityWeight
                : HomeostasisBrain.GlobalQualityWeight;
            return MathLodApproximation.SaturateFinite(qualityWeight, 1f);
        }

        private float ResolveFoveatedVatTimeScale()
        {
            float qualityWeight = ResolveGlobalQualityWeight01();
            switch (_foveatedSimulationTier)
            {
                case FoveatedSimulationTier.Frozen:
                    return math.lerp(0.08f, 0.22f, qualityWeight);
                case FoveatedSimulationTier.Peripheral:
                    return math.lerp(0.38f, 0.72f, qualityWeight);
                default:
                    return math.lerp(0.82f, 1.08f, qualityWeight);
            }
        }

        private bool TryResolvePlayerTargetPosition(out Vector3 playerPosition)
        {
            IPlayerRuntimeContext playerContext = ResolvePlayerContext();
            if (playerContext != null &&
                playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState))
            {
                if ((movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u)
                {
                    if (TryToFiniteVector3(movementState.PredictedWorldPosition, out playerPosition) ||
                        TryToFiniteVector3(movementState.WorldPosition, out playerPosition))
                    {
                        return true;
                    }
                }
            }

            if (playerContext != null &&
                playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot pose) &&
                (pose.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u &&
                TryToFiniteVector3(pose.RuntimePosition, out playerPosition))
            {
                return true;
            }

            playerPosition = default;
            return false;
        }

        private static bool TryToFiniteVector3(float3 value, out Vector3 result)
        {
            if (math.all(math.isfinite(value)))
            {
                result = new Vector3(value.x, value.y, value.z);
                return true;
            }

            result = default;
            return false;
        }

        private static bool TryResolveRuntimePosition(in AbsoluteUniversePosition positionAup, out Vector3 runtimePosition)
        {
            runtimePosition = default;
            if (!AbsoluteUniversePosition.IsFinite(in positionAup))
                return false;

            return TryToFiniteVector3(positionAup.ToRuntimeFloat3(), out runtimePosition);
        }

        private static bool TryToFiniteVector3(Vector3 value, out Vector3 result)
        {
            if (float.IsFinite(value.x) &&
                float.IsFinite(value.y) &&
                float.IsFinite(value.z))
            {
                result = value;
                return true;
            }

            result = default;
            return false;
        }

        private static float ClampFinite(float value, float min, float max, float fallback)
        {
            float safe = math.isfinite(value) ? value : fallback;
            if (safe < min)
                return min;
            if (safe > max)
                return max;
            return safe;
        }

        private static float ClampMinFinite(float value, float min, float fallback)
        {
            float safe = math.isfinite(value) ? value : fallback;
            return safe < min ? min : safe;
        }

        private static Vector2 ClampFiniteVector2(Vector2 value, float min, float max, Vector2 fallback)
        {
            Vector2 result;
            result.x = ClampFinite(value.x, min, max, fallback.x);
            result.y = ClampFinite(value.y, min, max, fallback.y);
            return result;
        }

        private static Vector3 ClampFiniteVector3(Vector3 value, float min, float max, Vector3 fallback)
        {
            Vector3 result;
            result.x = ClampFinite(value.x, min, max, fallback.x);
            result.y = ClampFinite(value.y, min, max, fallback.y);
            result.z = ClampFinite(value.z, min, max, fallback.z);
            return result;
        }

        private Vector3 ResolveSafeBoundsSize()
        {
            Vector3 result;
            result.x = ClampFinite(math.abs(boundsSize.x), 1f, RuntimeVectorComponentLimitMeters, 100f);
            result.y = ClampFinite(math.abs(boundsSize.y), 1f, RuntimeVectorComponentLimitMeters, 30f);
            result.z = ClampFinite(math.abs(boundsSize.z), 1f, RuntimeVectorComponentLimitMeters, 100f);
            return result;
        }

        private static bool IsFiniteMatrix4x4(Matrix4x4 value)
        {
            return math.isfinite(value.m00) &&
                   math.isfinite(value.m01) &&
                   math.isfinite(value.m02) &&
                   math.isfinite(value.m03) &&
                   math.isfinite(value.m10) &&
                   math.isfinite(value.m11) &&
                   math.isfinite(value.m12) &&
                   math.isfinite(value.m13) &&
                   math.isfinite(value.m20) &&
                   math.isfinite(value.m21) &&
                   math.isfinite(value.m22) &&
                   math.isfinite(value.m23) &&
                   math.isfinite(value.m30) &&
                   math.isfinite(value.m31) &&
                   math.isfinite(value.m32) &&
                   math.isfinite(value.m33);
        }

        /// <summary>
        /// Resolves the gameplay view camera from the current player hierarchy.
        /// </summary>
        private bool TryResolveViewCamera()
        {
            if (_mainCamera != null)
                return true;

            if (_playerTransform == null)
                FindPlayer();

            IPlayerRuntimeContext playerContext = ResolvePlayerContext();
            _mainCamera = playerContext != null ? playerContext.PlayerCamera : null;
            return _mainCamera != null;
        }

        private static int CeilDivPositive(int numerator, int denominator)
        {
            const int MaxDispatchGroupsPerDimension = 65535;
            if (numerator <= 0 || denominator <= 0)
                return 0;

            long groups = ((long)numerator + denominator - 1L) / denominator;
            return groups <= MaxDispatchGroupsPerDimension ? (int)groups : 0;
        }

        private static int CeilToIntPositive(float value)
        {
            return value > 0f ? Mathf.CeilToInt(value) : 0;
        }

        private static float FloorToMultiple(float value, float multiple)
        {
            return multiple > 0f ? Mathf.Floor(value / multiple) * multiple : value;
        }

        private static Vector3 ResolveDeterministicScatterVector(int index, Vector3 center, uint salt)
        {
            uint state = BuildDeterministicBoidSeed(index, center, salt);
            float x = NextSignedUnit(ref state);
            float y = NextSignedUnit(ref state);
            float z = NextSignedUnit(ref state);
            Vector3 value = new Vector3(x, y, z) * 0.57735026f;
            return value.sqrMagnitude > 0.015625f ? value : ResolveDeterministicCardinalAxis(index, salt);
        }

        private static Vector3 ResolveDeterministicCardinalAxis(int index, uint salt)
        {
            switch ((index + (int)(salt & 7u)) & 7)
            {
                case 0: return Vector3.right;
                case 1: return Vector3.left;
                case 2: return Vector3.up;
                case 3: return Vector3.down;
                case 4: return Vector3.forward;
                case 5: return Vector3.back;
                case 6: return new Vector3(0.70710677f, 0f, 0.70710677f);
                default: return new Vector3(-0.70710677f, 0f, 0.70710677f);
            }
        }

        private static uint BuildDeterministicBoidSeed(int index, Vector3 center, uint salt)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = (hash ^ salt) * 16777619u;
                hash = (hash ^ (uint)index) * 16777619u;
                hash = (hash ^ QuantizeSeedComponent(center.x)) * 16777619u;
                hash = (hash ^ QuantizeSeedComponent(center.y)) * 16777619u;
                hash = (hash ^ QuantizeSeedComponent(center.z)) * 16777619u;
                return hash != 0u ? hash : 1u;
            }
        }

        private static uint QuantizeSeedComponent(float value)
        {
            return unchecked((uint)Mathf.RoundToInt(value * 100f));
        }

        private static float NextSignedUnit(ref uint state)
        {
            state = unchecked((state * 1664525u) + 1013904223u);
            return ((state >> 8) * (1f / 8388607.5f)) - 1f;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  PUBLIC API
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>ÐšÐ¾Ð»Ð¸Ñ‡ÐµÑÑ‚Ð²Ð¾ Ð±Ð¾Ð¹Ð´Ð¾Ð².</summary>
        public int BoidCount => boidCount;

        /// <summary>
        /// Pure logic redirect for boid alignment force.
        /// Extracts calculation safely for tests.
        /// </summary>
        public static UnityEngine.Vector3 CalculateSteerForce(UnityEngine.Vector3 boidVelocity, UnityEngine.Vector3 averageNeighborVelocity, float maxSteerForce)
        {
            var systemBoidVel = new System.Numerics.Vector3(boidVelocity.x, boidVelocity.y, boidVelocity.z);
            var systemAvgVel = new System.Numerics.Vector3(averageNeighborVelocity.x, averageNeighborVelocity.y, averageNeighborVelocity.z);
            var result = Hecton8.PureLogic.Ecosystem.FlockingBoidAlignmentVector.Calculate(systemBoidVel, systemAvgVel, maxSteerForce);
            return new UnityEngine.Vector3(result.X, result.Y, result.Z);
        }


        /// <summary>Ð¡ÐµÐ¹Ñ‡Ð°Ñ Ñ€ÐµÐ½Ð´ÐµÑ€Ð¸Ñ‚ÑÑ.</summary>
        public bool IsVisible => _debugIsVisible;

        /// <summary>
        /// Ð£ÑÑ‚Ð°Ð½Ð°Ð²Ð»Ð¸Ð²Ð°ÐµÑ‚ heightmap Ð² runtime (Ð½Ð°Ð¿Ñ€Ð¸Ð¼ÐµÑ€, Ð¿Ñ€Ð¸ ÑÐ¼ÐµÐ½Ðµ Ñ‚Ð°Ð¹Ð»Ð° MapMagic).
        /// </summary>
        /// <param name="texture">Heightmap texture (R channel = height [0..1]).</param>
        /// <param name="offset">Terrain world position XZ.</param>
        /// <param name="size">Terrain world size XZ.</param>
        /// <param name="maxHeight">Terrain max height Y.</param>
        public void SetHeightMap(Texture2D texture, Vector2 offset, Vector2 size, float maxHeight)
        {
            heightMap   = texture;
            _fallbackHeightMap = texture == null ? neutralHeightMap : null;
            worldOffset = ClampFiniteVector2(offset, -RuntimeVectorComponentLimitMeters, RuntimeVectorComponentLimitMeters, Vector2.zero);
            worldSize   = ClampFiniteVector2(size, 0.001f, RuntimeVectorComponentLimitMeters, new Vector2(1024f, 1024f));
            heightScale = ClampFinite(maxHeight, 0f, MaxHeightScaleMeters, 100f);
        }

        public void ClearPredatorRuntimePositions()
        {
            _predatorRuntimePositionCount = 0;
        }

        public void ClearPredatorAupPositions()
        {
            ClearPredatorRuntimePositions();
        }

        public bool SetPredatorRuntimePosition(int index, Vector3 runtimePosition)
        {
            if ((uint)index >= MaxPredatorRuntimePositions ||
                !TryToFiniteVector3(runtimePosition, out Vector3 finitePosition))
                return false;

            _predatorRuntimePositions[index] = new Vector4(finitePosition.x, finitePosition.y, finitePosition.z, 1f);
            if (index + 1 > _predatorRuntimePositionCount)
                _predatorRuntimePositionCount = index + 1;

            return true;
        }

        public bool SetPredatorAupPosition(int index, in AbsoluteUniversePosition predatorAup)
        {
            if (!TryResolveRuntimePosition(in predatorAup, out Vector3 runtimePosition))
                return false;

            return SetPredatorRuntimePosition(index, runtimePosition);
        }

        [Obsolete("Vector3 predator input is runtime-origin-local. Use SetPredatorRuntimePosition(Vector3) or SetPredatorAupPosition(AbsoluteUniversePosition).")]
        public bool SetPredatorAupPosition(int index, Vector3 runtimePosition)
        {
            return SetPredatorRuntimePosition(index, runtimePosition);
        }

        public void SetPredatorRuntimePositionCount(int count)
        {
            _predatorRuntimePositionCount = Mathf.Clamp(count, 0, MaxPredatorRuntimePositions);
        }

        public void SetPredatorAupCount(int count)
        {
            SetPredatorRuntimePositionCount(count);
        }

        private void ConsumeAcousticPingSignals()
        {
            ReadOnlySpan<AcousticPingSignal> signals = SignalBus<AcousticPingSignal>.GetFrameSnapshot();
            int start = Mathf.Max(0, signals.Length - MaxAcousticPingSignalsPerFrame);
            float currentTime = ResolveBoidClockSeconds();
            for (int i = start; i < signals.Length; i++)
            {
                ref readonly AcousticPingSignal signal = ref signals[i];
                if (!math.isfinite(signal.RadiusMeters) ||
                    !math.isfinite(signal.Intensity01))
                {
                    continue;
                }

                float lifetimeSeconds = math.clamp(
                    signal.RadiusMeters / AcousticPingDecayMetersPerSecond,
                    AcousticPingMinLifetimeSeconds,
                    AcousticPingMaxLifetimeSeconds);
                RegisterAcousticPing(in signal.PositionAup, signal.RadiusMeters, signal.Intensity01, lifetimeSeconds, currentTime);
            }
        }

        private void AdvanceBoidClock(float deltaTime)
        {
            if (!math.isfinite(deltaTime) || deltaTime <= 0f)
                return;

            _boidClockSeconds = math.min(BoidClockMaxSeconds, _boidClockSeconds + deltaTime);
        }

        private float ResolveBoidClockSeconds()
        {
            return _boidClockSeconds;
        }

        public void RegisterAcousticPingRuntime(Vector3 runtimePosition, float radiusMeters, float intensity01, float lifetimeSeconds)
        {
            RegisterAcousticPingRuntime(runtimePosition, radiusMeters, intensity01, lifetimeSeconds, ResolveBoidClockSeconds());
        }

        public void RegisterAcousticPing(in AbsoluteUniversePosition pingAup, float radiusMeters, float intensity01, float lifetimeSeconds)
        {
            RegisterAcousticPing(in pingAup, radiusMeters, intensity01, lifetimeSeconds, ResolveBoidClockSeconds());
        }

        [Obsolete("Vector3 acoustic ping input is runtime-origin-local. Use RegisterAcousticPingRuntime(Vector3) or RegisterAcousticPing(AbsoluteUniversePosition).")]
        public void RegisterAcousticPing(Vector3 runtimePosition, float radiusMeters, float intensity01, float lifetimeSeconds)
        {
            RegisterAcousticPingRuntime(runtimePosition, radiusMeters, intensity01, lifetimeSeconds, ResolveBoidClockSeconds());
        }

        private void RegisterAcousticPing(in AbsoluteUniversePosition pingAup, float radiusMeters, float intensity01, float lifetimeSeconds, float currentTime)
        {
            if (!TryResolveRuntimePosition(in pingAup, out Vector3 runtimePosition))
                return;

            RegisterAcousticPingRuntime(runtimePosition, radiusMeters, intensity01, lifetimeSeconds, currentTime);
        }

        private void RegisterAcousticPingRuntime(Vector3 runtimePosition, float radiusMeters, float intensity01, float lifetimeSeconds, float currentTime)
        {
            if (!math.isfinite(radiusMeters) ||
                !math.isfinite(intensity01) ||
                !math.isfinite(lifetimeSeconds) ||
                !math.isfinite(currentTime) ||
                !TryToFiniteVector3(runtimePosition, out Vector3 finitePosition))
            {
                return;
            }

            float shockwaveWeight = ClampFinite(acousticPingShockwaveWeight, 0f, MaxBoidWeight, 1f);
            float radius = ClampFinite(radiusMeters, 0.001f, MaxBoidRadiusMeters, 0.001f);
            float intensity = ClampFinite(intensity01, 0f, 1f, 0f) * shockwaveWeight;
            if (intensity <= 0.0001f)
                return;

            float lifetime = ClampFinite(lifetimeSeconds, AcousticPingMinLifetimeSeconds, AcousticPingMaxLifetimeSeconds, AcousticPingMinLifetimeSeconds);
            _activeAcousticPingRuntimeRadius = new Vector4(finitePosition.x, finitePosition.y, finitePosition.z, radius);
            _activeAcousticPingParams = new Vector4(
                intensity,
                radius * radius,
                currentTime + lifetime,
                1f);
        }

        /// <summary>
        /// Ð¡Ð±Ñ€Ð°ÑÑ‹Ð²Ð°ÐµÑ‚ Ð¿Ð¾Ð·Ð¸Ñ†Ð¸Ð¸ Ð²ÑÐµÑ… Ð±Ð¾Ð¹Ð´Ð¾Ð² Ð² Ñ†ÐµÐ½Ñ‚Ñ€.
        /// Ð˜ÑÐ¿Ð¾Ð»ÑŒÐ·ÑƒÐ¹ Ð¿Ñ€Ð¸ Ñ‚ÐµÐ»ÐµÐ¿Ð¾Ñ€Ñ‚Ðµ Ð¸Ð³Ñ€Ð¾ÐºÐ°.
        /// Uses owner-created staging without runtime resize and uploads to BOTH Ping-Pong buffers.
        ///
        /// SPAWN Y RANGE:
        ///   ÐÐ¸Ð¶Ð½ÑÑ Ð³Ñ€Ð°Ð½Ð¸Ñ†Ð°: center.y - boundsSize.y (Ð¿Ð¾Ð»Ð½Ð°Ñ Ð²Ñ‹ÑÐ¾Ñ‚Ð° Ð±Ð¾ÐºÑÐ° Ð²Ð½Ð¸Ð·).
        ///   Ð’ÐµÑ€Ñ…Ð½ÑÑ Ð³Ñ€Ð°Ð½Ð¸Ñ†Ð°: waterSurfaceY - 2f.
        /// </summary>
        public void ResetPositions(Vector3 center)
        {
            if (_boidsBufferA == null || _boidsBufferB == null) return;
            if (!TryToFiniteVector3(center, out Vector3 safeCenter))
                return;

            UploadSpawnSetToBoidBuffers(safeCenter, 0xB01D2E57u, 0xB01D5A7Eu, true, allowResize: false);
            boundsCenter = safeCenter;
            _simulationBounds.center = safeCenter;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  EDITOR
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // Simulation bounds
            Gizmos.color = new Color(0f, 0.5f, 1f, 0.1f);
            Gizmos.DrawWireCube(boundsCenter, boundsSize * 2f);

            // Spawn radius
            Gizmos.color = new Color(0f, 1f, 0.5f, 0.15f);
            Gizmos.DrawWireSphere(boundsCenter, spawnRadius);

            // Water surface
            Gizmos.color = new Color(0f, 0.3f, 1f, 0.05f);
            Gizmos.DrawCube(
                new Vector3(boundsCenter.x, waterSurfaceY, boundsCenter.z),
                new Vector3(boundsSize.x * 2f, 0.1f, boundsSize.z * 2f));
        }

        private void OnValidate()
        {
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            boidCount = Mathf.Clamp(boidCount, 64, 8192);
            if (separationRadius > perceptionRadius)
                separationRadius = perceptionRadius * 0.5f;
            if (minSpeed > maxSpeed) minSpeed = maxSpeed * 0.5f;
            if (spawnRadius > boundsSize.magnitude) spawnRadius = boundsSize.magnitude * 0.5f;

            if (Application.isPlaying && _initialized)
            {
                RefreshDispatchGroupCounts();
                _simulationBounds = new Bounds(boundsCenter, boundsSize * 2f);
            }
        }
#endif
    
        #region JulesLink_FlockingBoidSeparationVector
        private static void JulesLink_FlockingBoidSeparationVector() { _ = typeof(Hecton8.PureLogic.Ecosystem.FlockingBoidSeparationVector); }
        #endregion

        #region JulesLink_FlockingBoidCohesionVector
        private static void JulesLink_FlockingBoidCohesionVector() { _ = typeof(Hecton8.PureLogic.Ecosystem.FlockingBoidCohesionVector); }
        #endregion
}
}

