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
//   â€¢ ITickable â€” Ð¸Ð½Ñ‚ÐµÐ³Ñ€Ð°Ñ†Ð¸Ñ Ñ GameTickManager. ÐÐµÑ‚ Update().
//   â€¢ Graphics.RenderMeshIndirect (Unity 6) â€” one GPU-visible draw call.
//   â€¢ Ping-Pong GraphicsBuffer â€” Ð´Ð²Ð° Ð±ÑƒÑ„ÐµÑ€Ð°, swap ÐºÐ°Ð¶Ð´Ñ‹Ð¹ ÐºÐ°Ð´Ñ€, zero race conditions.
//   â€¢ MaterialPropertyBlock â€” zero GC per-frame (reuse).
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
//   â€¢ MaterialPropertyBlock.SetBuffer â€” zero GC (reuse).
//   â€¢ ComputeShader.SetFloat/SetVector/SetInt â€” zero GC.
//   â€¢ Graphics.RenderMeshIndirect â€” zero GC.
//   â€¢ GeometryUtility.TestPlanesAABB â€” zero GC (struct arrays).
//   â€¢ Ping-Pong swap â€” integer increment, zero allocation.
// ============================================================================

using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Gameplay;
using Hecton8.Optimization;
using Hecton8.World;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.AI.GPU
{
    [DisallowMultipleComponent]
    public sealed class HectonBoidController : MonoBehaviour, ITickable, IUpdatable, Hecton8.Physics.IAcousticPingEventListener
    {
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  BOID DATA â€” must match compute shader struct exactly
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>Stride of BoidData in bytes. Must match GPU struct.</summary>
        private const int BoidStride = 32; // 8 Ã— sizeof(float)
        private const int SpatialGridMaxAxisResolution = 32;
        private const int SpatialGridMaxCellCount = SpatialGridMaxAxisResolution * SpatialGridMaxAxisResolution * SpatialGridMaxAxisResolution;
        private const int SpatialGridMaxBoidsPerCell = 32;
        private const int SpatialGridCounterStride = 4;
        private const int SpatialGridCellEntryStride = 4;
        private const int MaxPredatorAupPositions = 16;
        private const float DefaultBoidCullingRadiusScale = 2.25f;

        /// <summary>
        /// GPU-compatible boid data structure.
        /// 32 bytes total (8 floats Ã— 4 bytes).
        /// Matches HLSL struct BoidData layout exactly.
        /// Blittable â€” no GC, direct GPU upload.
        /// </summary>
        [StructLayout(LayoutKind.Sequential, Size = BoidStride)]
        private struct BoidData
        {
            public Vector3 position;  // 12 bytes
            public Vector3 velocity;  // 12 bytes
            public float   panic;     // 4 bytes
            public uint    stateFlags;// 4 bytes
            // TOTAL: 32 bytes
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  INSPECTOR â€” CORE
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        [Header("â”€â”€ Core References â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("Compute Shader Ð´Ð»Ñ ÑÐ¸Ð¼ÑƒÐ»ÑÑ†Ð¸Ð¸ Ð±Ð¾Ð¹Ð´Ð¾Ð².")]
        [SerializeField] private ComputeShader boidShader;

        [Tooltip("Mesh Ð¾Ð´Ð½Ð¾Ð¹ Ñ€Ñ‹Ð±Ñ‹ (low-poly, ~100-300 tris).")]
        [SerializeField] private Mesh fishMesh;

        [Tooltip("Material Ð´Ð»Ñ instanced Ñ€ÐµÐ½Ð´ÐµÑ€Ð°. Ð”Ð¾Ð»Ð¶ÐµÐ½ Ð¿Ð¾Ð´Ð´ÐµÑ€Ð¶Ð¸Ð²Ð°Ñ‚ÑŒ " +
                 "StructuredBuffer<BoidData> Ð² vertex shader.")]
        [SerializeField] private Material fishMaterial;

        [Header("â”€â”€ Population â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("ÐšÐ¾Ð»Ð¸Ñ‡ÐµÑÑ‚Ð²Ð¾ Ñ€Ñ‹Ð± Ð² ÑÑ‚Ð°Ðµ. Max recommended: 5000.")]
        [Range(64, 8192)]
        [SerializeField] private int boidCount = 2000;
        private bool _registeredToTickManager;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  INSPECTOR â€” BOID RULES
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        [Header("â”€â”€ Boid Weights â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [SerializeField] private float separationWeight = 2.5f;
        [SerializeField] private float alignmentWeight  = 1.0f;
        [SerializeField] private float cohesionWeight   = 1.0f;
        [SerializeField] private float targetWeight     = 0.5f;
        [SerializeField] private float obstacleWeight   = 3.0f;
        [SerializeField] private float boundsWeight     = 1.5f;

        [Header("â”€â”€ Boid Radii â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("Ð Ð°Ð´Ð¸ÑƒÑ Ð²Ð¾ÑÐ¿Ñ€Ð¸ÑÑ‚Ð¸Ñ (alignment + cohesion).")]
        [SerializeField] private float perceptionRadius    = 5f;
        [Tooltip("Ð Ð°Ð´Ð¸ÑƒÑ Ñ€Ð°Ð·Ð´ÐµÐ»ÐµÐ½Ð¸Ñ (separation). Ð”Ð¾Ð»Ð¶ÐµÐ½ Ð±Ñ‹Ñ‚ÑŒ < perception.")]
        [SerializeField] private float separationRadius    = 2f;
        [Tooltip("Ð’Ñ‹ÑÐ¾Ñ‚Ð° Ð½Ð°Ð´ Ð´Ð½Ð¾Ð¼, Ñ ÐºÐ¾Ñ‚Ð¾Ñ€Ð¾Ð¹ Ð½Ð°Ñ‡Ð¸Ð½Ð°ÐµÑ‚ÑÑ ÑƒÐºÐ»Ð¾Ð½ÐµÐ½Ð¸Ðµ.")]
        [SerializeField] private float obstacleAvoidRadius = 5f;

        [Header("â”€â”€ Speed â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [SerializeField] private float minSpeed = 2f;
        [SerializeField] private float maxSpeed = 6f;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  INSPECTOR â€” SPAWN ZONE
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        [Header("â”€â”€ Simulation Zone â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("Ð¦ÐµÐ½Ñ‚Ñ€ Ð·Ð¾Ð½Ñ‹ ÑÐ¸Ð¼ÑƒÐ»ÑÑ†Ð¸Ð¸ (Ð¼Ð¸Ñ€Ð¾Ð²Ñ‹Ðµ ÐºÐ¾Ð¾Ñ€Ð´Ð¸Ð½Ð°Ñ‚Ñ‹).")]
        [SerializeField] private Vector3 boundsCenter = Vector3.zero;
        [Tooltip("ÐŸÐ¾Ð»ÑƒÑ€Ð°Ð·Ð¼ÐµÑ€Ñ‹ Ð·Ð¾Ð½Ñ‹ ÑÐ¸Ð¼ÑƒÐ»ÑÑ†Ð¸Ð¸.")]
        [SerializeField] private Vector3 boundsSize   = new Vector3(100f, 30f, 100f);

        [Tooltip("Ð Ð°Ð´Ð¸ÑƒÑ Ð½Ð°Ñ‡Ð°Ð»ÑŒÐ½Ð¾Ð³Ð¾ ÑÐ¿Ð°Ð²Ð½Ð° Ð²Ð¾ÐºÑ€ÑƒÐ³ Ñ†ÐµÐ½Ñ‚Ñ€Ð°.")]
        [SerializeField] private float spawnRadius = 30f;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  INSPECTOR â€” HEIGHTMAP
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        [Header("â”€â”€ Heightmap (Terrain) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("Ð¢ÐµÐºÑÑ‚ÑƒÑ€Ð° Ð²Ñ‹ÑÐ¾Ñ‚ Ð¸Ð· MapMagic/Terrain. " +
                 "R-ÐºÐ°Ð½Ð°Ð» = Ð½Ð¾Ñ€Ð¼Ð°Ð»Ð¸Ð·Ð¾Ð²Ð°Ð½Ð½Ð°Ñ Ð²Ñ‹ÑÐ¾Ñ‚Ð° [0..1]. " +
                 "Ð•ÑÐ»Ð¸ null â€” obstacle avoidance Ð¸ÑÐ¿Ð¾Ð»ÑŒÐ·ÑƒÐµÑ‚ flat plane.")]
        [SerializeField] private Texture2D heightMap;

        [Tooltip("ÐœÐ¸Ñ€Ð¾Ð²Ð°Ñ Ð¿Ð¾Ð·Ð¸Ñ†Ð¸Ñ Ð½Ð°Ñ‡Ð°Ð»Ð° Ñ‚ÐµÑ€Ñ€ÐµÐ¹Ð½Ð° (XZ).")]
        [SerializeField] private Vector2 worldOffset = Vector2.zero;

        [Tooltip("ÐœÐ¸Ñ€Ð¾Ð²Ð¾Ð¹ Ñ€Ð°Ð·Ð¼ÐµÑ€ Ñ‚ÐµÑ€Ñ€ÐµÐ¹Ð½Ð° (XZ).")]
        [SerializeField] private Vector2 worldSize = new Vector2(1024f, 1024f);

        [Tooltip("ÐœÐ°ÑÑˆÑ‚Ð°Ð± Ð²Ñ‹ÑÐ¾Ñ‚Ñ‹ Ñ‚ÐµÑ€Ñ€ÐµÐ¹Ð½Ð° (Ð¼Ð°ÐºÑÐ¸Ð¼Ð°Ð»ÑŒÐ½Ð°Ñ Y).")]
        [SerializeField] private float heightScale = 100f;

        [Tooltip("Ð£Ñ€Ð¾Ð²ÐµÐ½ÑŒ Ð¿Ð¾Ð²ÐµÑ€Ñ…Ð½Ð¾ÑÑ‚Ð¸ Ð²Ð¾Ð´Ñ‹ (Ð¼Ð¸Ñ€Ð¾Ð²Ð°Ñ Y).")]
        [SerializeField] private float waterSurfaceY = 0f;

        [Header("GPU Ecosystem Inputs")]
        [SerializeField] private bool enableVoxelSdfAvoidance = true;
        [SerializeField] private HectonCaveVoxelLightingVolume caveSdfOverride;
        [SerializeField] private float voxelSdfWeight = 1.35f;
        [SerializeField] private bool enableAbyssalFlowAdvection = true;
        [SerializeField] private float abyssalFlowWeight = 0.35f;
        [SerializeField] private float predatorPanicRadius = 18f;
        [SerializeField] private float predatorEvasionWeight = 1f;
        [SerializeField] private float acousticPingShockwaveWeight = 1f;
        [SerializeField] private float panicDecayPerSecond = 2.5f;
        [SerializeField] private float panicAccelerationThreshold = 14f;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  INSPECTOR â€” RENDERING
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        [Header("â”€â”€ Rendering â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("ÐœÐ°ÑÑˆÑ‚Ð°Ð± Ð¼Ð¾Ð´ÐµÐ»Ð¸ Ñ€Ñ‹Ð±Ñ‹ (uniform).")]
        [SerializeField] private float fishScale = 0.3f;

        [Tooltip("Rendering layer mask.")]
        [SerializeField] private int renderingLayerMask = 1;

        [Tooltip("Shadow casting mode for instanced fish.")]
        [SerializeField] private ShadowCastingMode shadowMode = ShadowCastingMode.Off;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  INSPECTOR â€” DIAGNOSTICS
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        [Header("â”€â”€ Diagnostics â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [SerializeField] private bool  _debugIsVisible;
        [SerializeField] private float _debugComputeMs;
        [SerializeField] private int   _debugDispatchGroups;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  COMPUTE SHADER PROPERTY IDs â€” cached, zero GC
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private static class ShaderProps
        {
            // â”€â”€ Buffers (Compute Shader â€” Ping-Pong) â”€â”€
            public static readonly int BoidsBufferRead  = Shader.PropertyToID("_BoidsBufferRead");
            public static readonly int BoidsBufferWrite = Shader.PropertyToID("_BoidsBufferWrite");
            public static readonly int SpatialGridCounts = Shader.PropertyToID("_SpatialGridCounts");
            public static readonly int SpatialGridCells  = Shader.PropertyToID("_SpatialGridCells");

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

            public static readonly int PredatorAupPositions = Shader.PropertyToID("_PredatorAupPositions");
            public static readonly int PredatorCount = Shader.PropertyToID("_PredatorCount");
            public static readonly int PredatorPanicRadiusSq = Shader.PropertyToID("_PredatorPanicRadiusSq");
            public static readonly int PredatorWeight = Shader.PropertyToID("_PredatorWeight");
            public static readonly int AcousticPingAupRadius = Shader.PropertyToID("_AcousticPingAupRadius");
            public static readonly int AcousticPingParams = Shader.PropertyToID("_AcousticPingParams");
            public static readonly int PanicDecay = Shader.PropertyToID("_PanicDecay");
            public static readonly int PanicAccelerationThresholdSq = Shader.PropertyToID("_PanicAccelerationThresholdSq");
            public static readonly int FoveatedVatTimeScale = Shader.PropertyToID("_H8FoveatedVatTimeScale");
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
        private GraphicsBuffer _spatialGridCountBuffer;
        private GraphicsBuffer _spatialGridCellBuffer;
        private GraphicsBuffer _fallbackFlowFieldBuffer;
        private GraphicsBuffer _visibleBoidIndexBuffer;
        private GraphicsBuffer _visibleIndirectArgsBuffer;
        private Texture3D _fallbackVoxelSdfTexture;
        private Texture3D _fallbackAbyssalFlowTexture;
        private readonly Vector4[] _cameraFrustumPlaneUpload = new Vector4[6];
        private Mesh _indirectArgsMesh;
        private readonly Vector4[] _predatorAupPositions = new Vector4[MaxPredatorAupPositions];
        private int _predatorAupCount;
        private Vector4 _activeAcousticPingAupRadius;
        private Vector4 _activeAcousticPingParams;
        private bool _acousticPingSubscribed;

        /// <summary>
        /// Frame counter for Ping-Pong buffer swap.
        /// Incremented each Tick. Used as: _frameIndex % 2.
        /// Zero allocation swap â€” only integer arithmetic.
        /// </summary>
        private int _frameIndex;
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
        private int _kernelClearVisibleIndirectArgs;
        private int _kernelCullVisibleBoids;

        /// <summary>Thread group size X (read from shader).</summary>
        private int _threadGroupSizeX;

        /// <summary>Number of dispatch groups = ceil(boidCount / threadGroupSize).</summary>
        private int _dispatchGroupCount;
        private int _clearSpatialGridGroupCount = 1;

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

        /// <summary>MaterialPropertyBlock for instanced rendering. Reused â€” zero GC.</summary>
        private MaterialPropertyBlock _materialProps;

        /// <summary>ÐšÑÑˆÐ¸Ñ€Ð¾Ð²Ð°Ð½Ð½Ð°Ñ ÐºÐ°Ð¼ÐµÑ€Ð°.</summary>
        private Camera _mainCamera;
        private IFoveatedSimulationDirector _foveatedSimulationDirector;
        private FoveatedSimulationTier _foveatedSimulationTier = FoveatedSimulationTier.Active;

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
        /// v2.2 FIX: Ð”Ð¾Ð±Ð°Ð²Ð»ÐµÐ½Ð° Ð·Ð°Ñ‰Ð¸Ñ‚Ð° Ð¾Ñ‚ Ð¿Ð¾Ð²Ñ‚Ð¾Ñ€Ð½Ð¾Ð³Ð¾ Ð²Ñ‹Ð·Ð¾Ð²Ð° Ñ‡ÐµÑ€ÐµÐ· _initialized.
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
            EnsureMaterialPropertyBlock();
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
                Debug.LogWarning(
                    "[HectonBoidController] Awake() called while already initialized. " +
                    "Releasing old GPU resources before re-init.",
                    this);
                ReleaseBuffers();
                _initialized = false;
            }

            if (boidShader == null || fishMesh == null || fishMaterial == null)
            {
                Debug.LogError("[HectonBoidController] Missing required references!");
                enabled = false;
                return;
            }

            InitializeCompute();
            InitializeBuffers();
            InitializeRendering();

            _simulationBounds = new Bounds(boundsCenter, boundsSize * 2f);
            _initialized      = true;
        }

        private void EnsureMaterialPropertyBlock()
        {
            if (_materialProps != null)
                return;

            // COLD ALLOC: MaterialPropertyBlock[1] — boid instanced render state — owner: HectonBoidController
            _materialProps = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            if (_registeredToTickManager || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
            _registeredToTickManager = GlobalRegistry.Updatables.Contains(this);
            _foveatedSimulationDirector = GlobalRegistry.FoveatedSimulationDirector;
            Hecton8.Physics.PhysicsEventBus.Register((Hecton8.Physics.IAcousticPingEventListener)this);
            _acousticPingSubscribed = true;

            if (_playerTransform == null)
                FindPlayer();
        }

        private void OnDisable()
        {
            if (_registeredToTickManager)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _registeredToTickManager = false;
            }

            _foveatedSimulationDirector = null;
            _foveatedSimulationTier = FoveatedSimulationTier.Active;

            if (_acousticPingSubscribed)
            {
                Hecton8.Physics.PhysicsEventBus.Unregister((Hecton8.Physics.IAcousticPingEventListener)this);
                _acousticPingSubscribed = false;
            }
        }

        private void OnDestroy()
        {
            if (_acousticPingSubscribed)
            {
                Hecton8.Physics.PhysicsEventBus.Unregister((Hecton8.Physics.IAcousticPingEventListener)this);
                _acousticPingSubscribed = false;
            }

            ReleaseBuffers();
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  ITickable â€” MAIN LOOP
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>
        /// Called every frame by GameTickManager.
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
        public void Tick(float deltaTime)
        {
            using (ProfilerRegistry.AiTick.Auto())
            {
            if (!_initialized) return;
            EnsureRuntimeBufferCapacity();

            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
            //  1. UPDATE TARGET
            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

            UpdateTarget();
            UpdateSpatialGridLayout();
            if (_foveatedSimulationDirector == null)
                _foveatedSimulationDirector = GlobalRegistry.FoveatedSimulationDirector;

            if (_foveatedSimulationDirector != null)
                _foveatedSimulationTier = _foveatedSimulationDirector.ResolveTierForPosition(boundsCenter);
            bool simulateBoids = _foveatedSimulationTier != FoveatedSimulationTier.Frozen;

            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
            //  2. SET UNIFORMS (includes Ping-Pong buffer binding)
            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

            if (simulateBoids)
                SetComputeUniforms(deltaTime);

            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
            //  3. DISPATCH COMPUTE
            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

#if UNITY_EDITOR
            float t0 = Time.realtimeSinceStartup;
#endif

            if (simulateBoids)
            {
                DispatchSpatialGridBuild();
                boidShader.Dispatch(_kernelCSMain, _dispatchGroupCount, 1, 1);
            }

#if UNITY_EDITOR
            _debugComputeMs = simulateBoids ? (Time.realtimeSinceStartup - t0) * 1000f : 0f;
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
            }
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  INITIALIZATION â€” COMPUTE
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>
        /// Finds kernel, reads thread group size, computes dispatch count.
        /// </summary>
        private void InitializeCompute()
        {
            _kernelCSMain = boidShader.FindKernel("CSMain");
            _kernelClearSpatialGrid = boidShader.FindKernel("ClearSpatialGrid");
            _kernelBuildSpatialGrid = boidShader.FindKernel("BuildSpatialGrid");
            _kernelClearVisibleIndirectArgs = boidShader.FindKernel("ClearVisibleIndirectArgs");
            _kernelCullVisibleBoids = boidShader.FindKernel("CullVisibleBoids");

            uint threadX, threadY, threadZ;
            boidShader.GetKernelThreadGroupSizes(_kernelCSMain, out threadX, out threadY, out threadZ);
            _threadGroupSizeX = (int)threadX;

            _dispatchGroupCount = Mathf.Max(1, CeilDivPositive(boidCount, _threadGroupSizeX));
            _clearSpatialGridGroupCount = Mathf.Max(1, CeilDivPositive(SpatialGridMaxCellCount, _threadGroupSizeX));

#if UNITY_EDITOR
            _debugDispatchGroups = _dispatchGroupCount;
#endif
        }

        private void EnsureRuntimeBufferCapacity()
        {
            bool requiresReallocation =
                _boidsBufferA == null ||
                _boidsBufferB == null ||
                _spatialGridCountBuffer == null ||
                _spatialGridCellBuffer == null ||
                _visibleBoidIndexBuffer == null ||
                _visibleIndirectArgsBuffer == null ||
                _boidsBufferA.count != boidCount ||
                _boidsBufferB.count != boidCount ||
                _spatialGridCountBuffer.count != SpatialGridMaxCellCount ||
                _spatialGridCellBuffer.count != SpatialGridMaxCellCount * SpatialGridMaxBoidsPerCell ||
                _visibleBoidIndexBuffer.count != boidCount;
            if (!requiresReallocation)
                return;

            _dispatchGroupCount = Mathf.Max(1, CeilDivPositive(boidCount, _threadGroupSizeX));
            InitializeBuffers();
            InitializeRendering();
            _simulationBounds = new Bounds(boundsCenter, boundsSize * 2f);
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  INITIALIZATION â€” BUFFERS (v2.2 â€” GPU Memory Leak Fix)
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>
        /// Creates both Ping-Pong GraphicsBuffers, fills with identical initial positions,
        /// uploads to GPU. Creates args buffer for indirect draw.
        ///
        /// v2.2 FIX: ÐŸÐµÑ€ÐµÐ´ ÑÐ¾Ð·Ð´Ð°Ð½Ð¸ÐµÐ¼ ÐºÐ°Ð¶Ð´Ð¾Ð³Ð¾ GPU-Ñ€ÐµÑÑƒÑ€ÑÐ° Ð²Ñ‹Ð·Ñ‹Ð²Ð°ÐµÑ‚ÑÑ Release/Destroy
        /// Ð´Ð»Ñ ÑÑ‚Ð°Ñ€Ð¾Ð³Ð¾, ÐµÑÐ»Ð¸ Ð¾Ð½ Ð½Ðµ null. Ð­Ñ‚Ð¾ Ð¿Ñ€ÐµÐ´Ð¾Ñ‚Ð²Ñ€Ð°Ñ‰Ð°ÐµÑ‚ ÑƒÑ‚ÐµÑ‡ÐºÑƒ VRAM Ð¿Ñ€Ð¸:
        ///   â€¢ ÐŸÐ¾Ð²Ñ‚Ð¾Ñ€Ð½Ð¾Ð¼ Ð²Ñ‹Ð·Ð¾Ð²Ðµ InitializeBuffers() (hot reload, redesign).
        ///   â€¢ ÐŸÐµÑ€ÐµÑÐ¾Ð·Ð´Ð°Ð½Ð¸Ð¸ ÑÐ¸ÑÑ‚ÐµÐ¼Ñ‹ Ñ‡ÐµÑ€ÐµÐ· public API (SetBoidCount Ð² Ð±ÑƒÐ´ÑƒÑ‰ÐµÐ¼).
        ///   â€¢ Edge case Ñ Awake (ÑÐ¼. ÐºÐ¾Ð¼Ð¼ÐµÐ½Ñ‚Ð°Ñ€Ð¸Ð¹ Ð² Awake).
        ///
        /// ÐŸÐžÐ Ð¯Ð”ÐžÐš: Release old â†’ Create new â†’ SetData.
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

            if (_spatialGridCountBuffer != null)
            {
                _spatialGridCountBuffer.Release();
                _spatialGridCountBuffer = null;
            }

            if (_spatialGridCellBuffer != null)
            {
                _spatialGridCellBuffer.Release();
                _spatialGridCellBuffer = null;
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

            _indirectArgsMesh = null;

            if (_fallbackVoxelSdfTexture != null)
            {
                Destroy(_fallbackVoxelSdfTexture);
                _fallbackVoxelSdfTexture = null;
            }

            if (_fallbackAbyssalFlowTexture != null)
            {
                Destroy(_fallbackAbyssalFlowTexture);
                _fallbackAbyssalFlowTexture = null;
            }

            // â”€â”€ Release old args buffer â”€â”€
            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
            //  STEP 2: Create Ping-Pong boids buffers
            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

            _boidsBufferA = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<BoidData>(boidCount);
            _boidsBufferB = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<BoidData>(boidCount);
            _spatialGridCountBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Raw,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                SpatialGridMaxCellCount,
                SpatialGridCounterStride);
            _spatialGridCellBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                SpatialGridMaxCellCount * SpatialGridMaxBoidsPerCell,
                SpatialGridCellEntryStride);
            _fallbackFlowFieldBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<Vector4>(1);
            UploadFallbackFlowField();
            _visibleBoidIndexBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<uint>(boidCount);
            _visibleIndirectArgsBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Raw,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                1,
                GraphicsBuffer.IndirectDrawIndexedArgs.size);
            UploadIndirectArgsStaticMeshData();

            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
            //  STEP 3: Fill initial data
            //  One array, uploaded to BOTH buffers.
            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

            UploadSpawnSetToBoidBuffers(boundsCenter, 0xB01D5EEDu, 0xB01D7101u, false);

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

            if (heightMap == null && _fallbackHeightMap == null)
            {
                // Ð¡Ð¾Ð·Ð´Ð°Ñ‘Ð¼ Ð¼Ð¸Ð½Ð¸Ð¼Ð°Ð»ÑŒÐ½ÑƒÑŽ Ñ‚ÐµÐºÑÑ‚ÑƒÑ€Ñƒ 4Ã—4 (R8 = 16 Ð±Ð°Ð¹Ñ‚ Ð½Ð° GPU).
                // Ð§Ñ‘Ñ€Ð½Ð°Ñ = Ð²Ñ‹ÑÐ¾Ñ‚Ð° 0 = Ð¿Ð»Ð¾ÑÐºÐ¾Ðµ Ð´Ð½Ð¾.
                // hideFlags Ð¿Ñ€ÐµÐ´Ð¾Ñ‚Ð²Ñ€Ð°Ñ‰Ð°ÐµÑ‚ Ð¿Ð¾ÑÐ²Ð»ÐµÐ½Ð¸Ðµ Ð² Project/Hierarchy.
                _fallbackHeightMap = new Texture2D(4, 4, TextureFormat.R8, false)
                {
                    name       = "[HectonBoid] FallbackHeightMap",
                    hideFlags  = HideFlags.HideAndDontSave,
                    wrapMode   = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };

                // NativeArray path: zero managed Color[] allocation.
                // GetRawTextureData returns existing native buffer â€” zero GC.
                var rawData = _fallbackHeightMap.GetRawTextureData<byte>();
                for (int i = 0; i < rawData.Length; i++)
                {
                    rawData[i] = 0; // Black = height 0
                }

                _fallbackHeightMap.Apply(false, false);
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

            _fallbackVoxelSdfTexture = new Texture3D(2, 2, 2, TextureFormat.R8, false)
            {
                name = "[HectonBoid] FallbackCaveSdf",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            for (int z = 0; z < 2; z++)
            {
                for (int y = 0; y < 2; y++)
                {
                    for (int x = 0; x < 2; x++)
                        _fallbackVoxelSdfTexture.SetPixel(x, y, z, Color.white);
                }
            }

            _fallbackVoxelSdfTexture.Apply(false, false);
        }

        private void EnsureFallbackAbyssalFlowTexture()
        {
            if (_fallbackAbyssalFlowTexture != null)
                return;

            _fallbackAbyssalFlowTexture = new Texture3D(1, 1, 1, TextureFormat.RGBAHalf, false)
            {
                name = "[HectonBoid] FallbackAbyssalFlow",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            }; // COLD ALLOC: Texture3D[1] - zero fallback abyssal-flow volume for boid compute binding - owner: HectonBoidController
            _fallbackAbyssalFlowTexture.SetPixel(0, 0, 0, Color.clear);
            _fallbackAbyssalFlowTexture.Apply(false, true);
        }

        private void UploadFallbackFlowField()
        {
            if (_fallbackFlowFieldBuffer == null)
                return;

            var mapped = _fallbackFlowFieldBuffer.LockBufferForWrite<Vector4>(0, 1);
            mapped[0] = Vector4.zero;
            _fallbackFlowFieldBuffer.UnlockBufferAfterWrite<Vector4>(1);
        }

        private void UploadIndirectArgsStaticMeshData()
        {
            if (_visibleIndirectArgsBuffer == null || fishMesh == null || ReferenceEquals(_indirectArgsMesh, fishMesh))
                return;

            var mapped =
                _visibleIndirectArgsBuffer.LockBufferForWrite<GraphicsBuffer.IndirectDrawIndexedArgs>(0, 1);
            mapped[0] = new GraphicsBuffer.IndirectDrawIndexedArgs
            {
                indexCountPerInstance = fishMesh.GetIndexCount(0),
                instanceCount = 0u,
                startIndex = fishMesh.GetIndexStart(0),
                baseVertexIndex = (uint)Mathf.Max(0, fishMesh.GetBaseVertex(0)),
                startInstance = 0u
            };
            _visibleIndirectArgsBuffer.UnlockBufferAfterWrite<GraphicsBuffer.IndirectDrawIndexedArgs>(1);
            _indirectArgsMesh = fishMesh;
        }

        private void UploadSpawnSetToBoidBuffers(Vector3 center, uint positionSeed, uint velocitySeed, bool useMinimumVelocity)
        {
            if (_boidsBufferA == null || _boidsBufferB == null)
                return;

            int safeCount = math.min(boidCount, math.min(_boidsBufferA.count, _boidsBufferB.count));
            if (safeCount <= 0)
                return;

            var writeA = _boidsBufferA.LockBufferForWrite<BoidData>(0, safeCount);
            var writeB = _boidsBufferB.LockBufferForWrite<BoidData>(0, safeCount);
            float spawnSpeed = useMinimumVelocity ? minSpeed : (minSpeed + maxSpeed) * 0.5f;
            for (int i = 0; i < safeCount; i++)
            {
                Vector3 position = center + ResolveDeterministicScatterVector(i, center, positionSeed) * spawnRadius;
                position.y = Mathf.Clamp(position.y, center.y - boundsSize.y, waterSurfaceY - 2f);

                Vector3 velocity = ResolveDeterministicScatterVector(i, center, velocitySeed) * spawnSpeed;
                float minimumSpeedSq = minSpeed * minSpeed;
                if (velocity.sqrMagnitude < minimumSpeedSq)
                    velocity = ResolveDeterministicCardinalAxis(i, velocitySeed) * minSpeed;

                BoidData boid = new BoidData
                {
                    position = position,
                    velocity = velocity,
                    panic = 0f,
                    stateFlags = 0u
                };
                writeA[i] = boid;
                writeB[i] = boid;
            }

            _boidsBufferA.UnlockBufferAfterWrite<BoidData>(safeCount);
            _boidsBufferB.UnlockBufferAfterWrite<BoidData>(safeCount);
        }

        /// <summary>
        /// Sets up MaterialPropertyBlock and RenderParams.
        /// One-time allocation. Reused every frame.
        /// Initial buffer binding uses _boidsBufferB (first frame's write target).
        /// </summary>
        private void InitializeRendering()
        {
            _materialProps.Clear();

            // Frame 0: Read=A, Write=B â†’ after dispatch, fresh data is in B
            _materialProps.SetBuffer(ShaderProps.BoidsBuffer, _boidsBufferB);
            _materialProps.SetBuffer(ShaderProps.VisibleBoidIndices, _visibleBoidIndexBuffer);
            _materialProps.SetFloat(ShaderProps.BoidUseVisibleIndices, 1f);
            _materialProps.SetFloat("_FishScale", fishScale);
            _materialProps.SetFloat(ShaderProps.FoveatedVatTimeScale, 1f);

            _renderParams = new RenderParams(fishMaterial)
            {
                matProps             = _materialProps,
                worldBounds          = _simulationBounds,
                shadowCastingMode    = shadowMode,
                receiveShadows       = false,
                renderingLayerMask   = HectonLayerMasks.ToRenderingLayerMask(renderingLayerMask)
            };
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

            if (_spatialGridCountBuffer != null)
            {
                _spatialGridCountBuffer.Release();
                _spatialGridCountBuffer = null;
            }

            if (_spatialGridCellBuffer != null)
            {
                _spatialGridCellBuffer.Release();
                _spatialGridCellBuffer = null;
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

            _indirectArgsMesh = null;

            if (_fallbackHeightMap != null)
            {
                Destroy(_fallbackHeightMap);
                _fallbackHeightMap = null;
            }

            if (_fallbackVoxelSdfTexture != null)
            {
                Destroy(_fallbackVoxelSdfTexture);
                _fallbackVoxelSdfTexture = null;
            }

            if (_fallbackAbyssalFlowTexture != null)
            {
                Destroy(_fallbackAbyssalFlowTexture);
                _fallbackAbyssalFlowTexture = null;
            }
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

            // â”€â”€ Ping-Pong Buffer Binding â”€â”€
            // Determine which buffer is Read and which is Write this frame.
            // No allocation â€” just pointer swap via ternary on existing references.
            GraphicsBuffer readBuffer  = (_frameIndex % 2 == 0) ? _boidsBufferA : _boidsBufferB;
            GraphicsBuffer writeBuffer = (_frameIndex % 2 == 0) ? _boidsBufferB : _boidsBufferA;

            cs.SetBuffer(kernel, ShaderProps.BoidsBufferRead, readBuffer);
            cs.SetBuffer(kernel, ShaderProps.BoidsBufferWrite, writeBuffer);
            cs.SetBuffer(kernel, ShaderProps.SpatialGridCounts, _spatialGridCountBuffer);
            cs.SetBuffer(kernel, ShaderProps.SpatialGridCells, _spatialGridCellBuffer);
            cs.SetBuffer(_kernelBuildSpatialGrid, ShaderProps.BoidsBufferRead, readBuffer);
            cs.SetBuffer(_kernelBuildSpatialGrid, ShaderProps.SpatialGridCounts, _spatialGridCountBuffer);
            cs.SetBuffer(_kernelBuildSpatialGrid, ShaderProps.SpatialGridCells, _spatialGridCellBuffer);
            cs.SetBuffer(_kernelClearSpatialGrid, ShaderProps.SpatialGridCounts, _spatialGridCountBuffer);

            // â”€â”€ Simulation â”€â”€
            cs.SetInt(ShaderProps.BoidCount, boidCount);
            cs.SetFloat(ShaderProps.DeltaTime, dt);
            cs.SetVector(ShaderProps.SpatialGridOrigin,
                new Vector4(_spatialGridOrigin.x, _spatialGridOrigin.y, _spatialGridOrigin.z, 0f));
            cs.SetVector(ShaderProps.SpatialGridResolution,
                new Vector4(_spatialGridResolution.x, _spatialGridResolution.y, _spatialGridResolution.z, 0f));
            cs.SetFloat(ShaderProps.SpatialGridCellSize, _spatialGridCellSize);
            cs.SetInt(ShaderProps.SpatialGridMaxBoidsPerCell, SpatialGridMaxBoidsPerCell);
            cs.SetInt(ShaderProps.BoidMathLodMode,
                DistanceMath.ResolveMathLodMode(GlobalRegistry.ScalabilityTier) == MathLodMode.High ? 1 : 0);

            // â”€â”€ Weights â”€â”€
            cs.SetFloat(ShaderProps.SeparationWeight, separationWeight);
            cs.SetFloat(ShaderProps.AlignmentWeight, alignmentWeight);
            cs.SetFloat(ShaderProps.CohesionWeight, cohesionWeight);
            cs.SetFloat(ShaderProps.TargetWeight, targetWeight);
            cs.SetFloat(ShaderProps.ObstacleWeight, obstacleWeight);
            cs.SetFloat(ShaderProps.BoundsWeight, boundsWeight);

            // â”€â”€ Radii â”€â”€
            cs.SetFloat(ShaderProps.PerceptionRadius, perceptionRadius);
            cs.SetFloat(ShaderProps.SeparationRadius, separationRadius);
            cs.SetFloat(ShaderProps.ObstacleAvoidRadius, obstacleAvoidRadius);

            // â”€â”€ Speed â”€â”€
            cs.SetFloat(ShaderProps.MinSpeed, minSpeed);
            cs.SetFloat(ShaderProps.MaxSpeed, maxSpeed);

            // â”€â”€ Target â”€â”€
            cs.SetVector(ShaderProps.TargetPosition,
                new Vector4(_targetPosition.x, _targetPosition.y, _targetPosition.z, 0f));

            // â”€â”€ Bounds â”€â”€
            cs.SetVector(ShaderProps.BoundsCenter,
                new Vector4(boundsCenter.x, boundsCenter.y, boundsCenter.z, 0f));
            cs.SetVector(ShaderProps.BoundsSize,
                new Vector4(boundsSize.x, boundsSize.y, boundsSize.z, 0f));

            // â”€â”€ Heightmap â”€â”€
            Texture2D hmap = heightMap != null ? heightMap : _fallbackHeightMap;
            cs.SetTexture(kernel, ShaderProps.HeightMap, hmap);
            cs.SetVector(ShaderProps.WorldOffset,
                new Vector4(worldOffset.x, worldOffset.y, 0f, 0f));
            cs.SetVector(ShaderProps.WorldSize,
                new Vector4(worldSize.x, worldSize.y, 0f, 0f));
            cs.SetFloat(ShaderProps.HeightScaleProp, heightScale);
            cs.SetFloat(ShaderProps.WaterSurfaceY, waterSurfaceY);

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

            HectonCaveVoxelLightingVolume caveVolume = caveSdfOverride != null
                ? caveSdfOverride
                : HectonCaveVoxelLightingVolume.ActiveRuntimeInstance;

            if (enableVoxelSdfAvoidance &&
                caveVolume != null &&
                caveVolume.TryGetPublishedGpuSdfPayload(
                    out Texture3D publishedTexture,
                    out Matrix4x4 publishedWorldToLocal,
                    out Vector4 publishedHalfExtentsAndRange,
                    out Vector4 publishedInvDoubleHalfExtents))
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
            cs.SetFloat(ShaderProps.CaveVoxelWeight, Mathf.Max(0f, voxelSdfWeight));
        }

        private void BindAbyssalFlowPayload(ComputeShader cs, int kernel)
        {
            GraphicsBuffer flowBuffer = _fallbackFlowFieldBuffer;
            Texture flowTexture = _fallbackAbyssalFlowTexture;
            Vector4 gridResolution = Vector4.zero;
            Vector4 flowCenter = Vector4.zero;
            Vector4 flowSpacing = Vector4.zero;
            float active = 0f;

            var fluid = GlobalRegistry.Fluid;
            if (enableAbyssalFlowAdvection && fluid != null)
            {
                if (fluid.TryGetGpuAbyssalFlowFieldTexture(
                    out Texture publishedFlowTexture,
                    out Vector4 publishedTextureGridResolution,
                    out Vector4 publishedTextureFlowCenter,
                    out Vector4 publishedTextureFlowSpacing))
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
                    out Vector4 publishedFlowSpacing))
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
            cs.SetFloat(ShaderProps.AbyssalFlowWeight, Mathf.Max(0f, abyssalFlowWeight));
        }

        private void BindPanicPayload(ComputeShader cs)
        {
            float predatorRadius = Mathf.Max(0.001f, predatorPanicRadius);
            cs.SetVectorArray(ShaderProps.PredatorAupPositions, _predatorAupPositions);
            cs.SetInt(ShaderProps.PredatorCount, Mathf.Clamp(_predatorAupCount, 0, MaxPredatorAupPositions));
            cs.SetFloat(ShaderProps.PredatorPanicRadiusSq, predatorRadius * predatorRadius);
            cs.SetFloat(ShaderProps.PredatorWeight, Mathf.Max(0f, predatorEvasionWeight));

            float pingActive = _activeAcousticPingParams.x > 0.0001f && Time.time <= _activeAcousticPingParams.z ? 1f : 0f;
            _activeAcousticPingParams.w = pingActive;
            cs.SetVector(ShaderProps.AcousticPingAupRadius, _activeAcousticPingAupRadius);
            cs.SetVector(ShaderProps.AcousticPingParams, _activeAcousticPingParams);
            cs.SetFloat(ShaderProps.PanicDecay, Mathf.Max(0f, panicDecayPerSecond));
            float accelerationThreshold = Mathf.Max(0.001f, panicAccelerationThreshold);
            cs.SetFloat(ShaderProps.PanicAccelerationThresholdSq, accelerationThreshold * accelerationThreshold);
        }

        private void DispatchSpatialGridBuild()
        {
            boidShader.Dispatch(_kernelClearSpatialGrid, _clearSpatialGridGroupCount, 1, 1);
            boidShader.Dispatch(_kernelBuildSpatialGrid, _dispatchGroupCount, 1, 1);
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
            boidShader.SetBuffer(_kernelClearVisibleIndirectArgs, ShaderProps.VisibleIndirectArgs, _visibleIndirectArgsBuffer);
            boidShader.SetBuffer(_kernelCullVisibleBoids, ShaderProps.BoidsBufferRead, currentDataBuffer);
            boidShader.SetBuffer(_kernelCullVisibleBoids, ShaderProps.VisibleBoidIndices, _visibleBoidIndexBuffer);
            boidShader.SetBuffer(_kernelCullVisibleBoids, ShaderProps.VisibleIndirectArgs, _visibleIndirectArgsBuffer);
            boidShader.SetInt(ShaderProps.BoidCount, boidCount);
            boidShader.SetVectorArray(ShaderProps.CameraFrustumPlanes, _cameraFrustumPlaneUpload);
            boidShader.SetFloat(ShaderProps.BoidCullingRadius, Mathf.Max(0.01f, fishScale * DefaultBoidCullingRadiusScale));
            boidShader.SetInt(ShaderProps.GpuFrustumCullingActive, hasCameraFrustum ? 1 : 0);
            boidShader.Dispatch(_kernelClearVisibleIndirectArgs, 1, 1, 1);
            boidShader.Dispatch(_kernelCullVisibleBoids, _dispatchGroupCount, 1, 1);
        }

        private void UpdateSpatialGridLayout()
        {
            float baseCellSize = Mathf.Max(Mathf.Max(perceptionRadius, separationRadius), 0.001f);
            Vector3 doubledExtents = boundsSize * 2f;
            Vector3 fieldSize = new Vector3(
                Mathf.Max(doubledExtents.x, baseCellSize),
                Mathf.Max(doubledExtents.y, baseCellSize),
                Mathf.Max(doubledExtents.z, baseCellSize));
            float axisClampCellSize = Mathf.Max(
                fieldSize.x / SpatialGridMaxAxisResolution,
                Mathf.Max(fieldSize.y / SpatialGridMaxAxisResolution, fieldSize.z / SpatialGridMaxAxisResolution));
            _spatialGridCellSize = Mathf.Max(baseCellSize, axisClampCellSize);

            Vector3 fieldMin = boundsCenter - boundsSize;
            Vector3 fieldMax = boundsCenter + boundsSize;
            _spatialGridOrigin = new Vector3(
                FloorToMultiple(fieldMin.x, _spatialGridCellSize),
                FloorToMultiple(fieldMin.y, _spatialGridCellSize),
                FloorToMultiple(fieldMin.z, _spatialGridCellSize));

            int resolutionX = Mathf.Clamp(CeilToIntPositive((fieldMax.x - _spatialGridOrigin.x) / _spatialGridCellSize), 1, SpatialGridMaxAxisResolution);
            int resolutionY = Mathf.Clamp(CeilToIntPositive((fieldMax.y - _spatialGridOrigin.y) / _spatialGridCellSize), 1, SpatialGridMaxAxisResolution);
            int resolutionZ = Mathf.Clamp(CeilToIntPositive((fieldMax.z - _spatialGridOrigin.z) / _spatialGridCellSize), 1, SpatialGridMaxAxisResolution);
            _spatialGridResolution = new Vector3Int(resolutionX, resolutionY, resolutionZ);

            int cellCount = resolutionX * resolutionY * resolutionZ;
            _clearSpatialGridGroupCount = Mathf.Max(1, CeilDivPositive(cellCount, _threadGroupSizeX));
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

            _materialProps.SetBuffer(ShaderProps.BoidsBuffer, currentDataBuffer);
            _materialProps.SetBuffer(ShaderProps.VisibleBoidIndices, _visibleBoidIndexBuffer);
            _materialProps.SetFloat(ShaderProps.BoidUseVisibleIndices, 1f);
            _materialProps.SetFloat(ShaderProps.FoveatedVatTimeScale,
                _foveatedSimulationTier == FoveatedSimulationTier.Peripheral ? 0.5f : 1f);
            _renderParams.matProps = _materialProps;

            // Update world bounds in case center moved
            _renderParams.worldBounds = _simulationBounds;

            UnityEngine.Graphics.RenderMeshIndirect(_renderParams, fishMesh, _visibleIndirectArgsBuffer, 1, 0);
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
                _targetPosition = playerPosition;

                // Ð”Ð¸Ð½Ð°Ð¼Ð¸Ñ‡ÐµÑÐºÐ¸Ðµ Ð³Ñ€Ð°Ð½Ð¸Ñ†Ñ‹: Ñ†ÐµÐ½Ñ‚Ñ€ ÑÐ»ÐµÐ´ÑƒÐµÑ‚ Ð·Ð° Ð¸Ð³Ñ€Ð¾ÐºÐ¾Ð¼ Ð¿Ð¾ X, Y Ð¸ Z.
                // ÐžÐ³Ñ€Ð°Ð½Ð¸Ñ‡Ð¸Ð²Ð°ÐµÐ¼ Y, Ñ‡Ñ‚Ð¾Ð±Ñ‹ Ð²ÐµÑ€Ñ…Ð½ÑÑ Ð³Ñ€Ð°Ð½Ð¸Ñ†Ð° Ð±Ð¾ÐºÑÐ° (center.y + boundsSize.y)
                // Ð½Ðµ Ð¿Ñ€Ð¾Ð±Ð¸Ð²Ð°Ð»Ð° Ð¿Ð¾Ð²ÐµÑ€Ñ…Ð½Ð¾ÑÑ‚ÑŒ Ð²Ð¾Ð´Ñ‹ (waterSurfaceY).
                float maxCenterY = waterSurfaceY - boundsSize.y;
                float targetY    = Mathf.Min(_targetPosition.y, maxCenterY);

                boundsCenter = new Vector3(
                    _targetPosition.x,
                    targetY,
                    _targetPosition.z);

                _simulationBounds.center = boundsCenter;
            }
            else
            {
                _targetPosition = boundsCenter;
            }
        }

        /// <summary>
        /// Resolves the player transform through the shared runtime path.
        /// </summary>
        private void FindPlayer()
        {
            _playerRuntimeContext = ResolvePlayerContext();
            _playerTransform = _playerRuntimeContext != null ? _playerRuntimeContext.PlayerTransform : null;
            if (_playerTransform == null)
                WorldRuntimeReferenceUtility.TryResolvePlayerTransform(ref _playerTransform);
        }

        private IPlayerRuntimeContext ResolvePlayerContext()
        {
            if (_playerRuntimeContext != null)
                return _playerRuntimeContext;

            _playerRuntimeContext = Hecton8.Core.GlobalRegistry.Player;
            return _playerRuntimeContext;
        }

        private bool TryResolvePlayerTargetPosition(out Vector3 playerPosition)
        {
            if (PlayerRuntimeContextService.TryGetActiveRuntimeContext(out PlayerRuntimeContext runtimeContext) &&
                runtimeContext != null)
            {
                PlayerMovementRuntimeState movementState = runtimeContext.MovementState;
                if ((movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u)
                {
                    if (TryToFiniteVector3(movementState.PredictedWorldPosition, out playerPosition) ||
                        TryToFiniteVector3(movementState.WorldPosition, out playerPosition))
                    {
                        return true;
                    }

                    float3 aupRuntime = movementState.PredictedAup.ToRuntimeFloat3();
                    if (TryToFiniteVector3(aupRuntime, out playerPosition))
                        return true;
                }
            }

            IPlayerRuntimeContext playerContext = ResolvePlayerContext();
            if (playerContext != null &&
                playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot pose) &&
                (pose.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u &&
                TryToFiniteVector3(pose.RuntimePosition, out playerPosition))
            {
                return true;
            }

            HectonPlayerMovement movement = playerContext != null ? playerContext.PlayerMovement : null;
            if (movement != null)
            {
                float3 runtime = movement.CurrentAup.ToRuntimeFloat3();
                if (TryToFiniteVector3(runtime, out playerPosition))
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

        /// <summary>
        /// Resolves the gameplay view camera from the current player hierarchy.
        /// </summary>
        private bool TryResolveViewCamera()
        {
            if (_mainCamera != null)
                return true;

            if (_playerTransform == null)
                FindPlayer();

            if (_playerTransform == null)
                return false;

            IPlayerRuntimeContext playerContext = ResolvePlayerContext();
            _mainCamera = playerContext != null && playerContext.PlayerCamera != null
                ? playerContext.PlayerCamera
                : Hecton8.Core.ComponentReferenceUtility.ResolveOwnedComponent<Camera>(_playerTransform);
            return _mainCamera != null;
        }

        private static int CeilDivPositive(int numerator, int denominator)
        {
            if (numerator <= 0)
                return 0;

            int safeDenominator = Mathf.Max(1, denominator);
            return 1 + ((numerator - 1) / safeDenominator);
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
            worldOffset = offset;
            worldSize   = size;
            heightScale = maxHeight;
        }

        public void ClearPredatorAupPositions()
        {
            _predatorAupCount = 0;
        }

        public bool SetPredatorAupPosition(int index, Vector3 aupPosition)
        {
            if ((uint)index >= MaxPredatorAupPositions)
                return false;

            _predatorAupPositions[index] = new Vector4(aupPosition.x, aupPosition.y, aupPosition.z, 1f);
            if (index + 1 > _predatorAupCount)
                _predatorAupCount = index + 1;

            return true;
        }

        public void SetPredatorAupCount(int count)
        {
            _predatorAupCount = Mathf.Clamp(count, 0, MaxPredatorAupPositions);
        }

        public void OnAcousticPing(in Hecton8.Physics.AcousticPingEvent pingEvent)
        {
            RegisterAcousticPing(
                pingEvent.RuntimePosition,
                pingEvent.RadiusMeters,
                pingEvent.Intensity01,
                pingEvent.LifetimeSeconds);
        }

        public void RegisterAcousticPing(Vector3 aupPosition, float radiusMeters, float intensity01, float lifetimeSeconds)
        {
            float radius = Mathf.Max(0.001f, radiusMeters);
            float intensity = Mathf.Clamp01(intensity01) * Mathf.Max(0f, acousticPingShockwaveWeight);
            if (intensity <= 0.0001f)
                return;

            _activeAcousticPingAupRadius = new Vector4(aupPosition.x, aupPosition.y, aupPosition.z, radius);
            _activeAcousticPingParams = new Vector4(
                intensity,
                radius * radius,
                Time.time + Mathf.Max(0.001f, lifetimeSeconds),
                1f);
        }

        /// <summary>
        /// Ð¡Ð±Ñ€Ð°ÑÑ‹Ð²Ð°ÐµÑ‚ Ð¿Ð¾Ð·Ð¸Ñ†Ð¸Ð¸ Ð²ÑÐµÑ… Ð±Ð¾Ð¹Ð´Ð¾Ð² Ð² Ñ†ÐµÐ½Ñ‚Ñ€.
        /// Ð˜ÑÐ¿Ð¾Ð»ÑŒÐ·ÑƒÐ¹ Ð¿Ñ€Ð¸ Ñ‚ÐµÐ»ÐµÐ¿Ð¾Ñ€Ñ‚Ðµ Ð¸Ð³Ñ€Ð¾ÐºÐ°.
        /// Ð’Ñ‹Ð·Ñ‹Ð²Ð°ÐµÑ‚ SetData â€” Ð¾Ð´Ð½Ð° Ð°Ð»Ð»Ð¾ÐºÐ°Ñ†Ð¸Ñ managed Ð¼Ð°ÑÑÐ¸Ð²Ð°.
        /// Uploads to BOTH Ping-Pong buffers to ensure consistency.
        ///
        /// SPAWN Y RANGE:
        ///   ÐÐ¸Ð¶Ð½ÑÑ Ð³Ñ€Ð°Ð½Ð¸Ñ†Ð°: center.y - boundsSize.y (Ð¿Ð¾Ð»Ð½Ð°Ñ Ð²Ñ‹ÑÐ¾Ñ‚Ð° Ð±Ð¾ÐºÑÐ° Ð²Ð½Ð¸Ð·).
        ///   Ð’ÐµÑ€Ñ…Ð½ÑÑ Ð³Ñ€Ð°Ð½Ð¸Ñ†Ð°: waterSurfaceY - 2f.
        /// </summary>
        public void ResetPositions(Vector3 center)
        {
            if (_boidsBufferA == null || _boidsBufferB == null) return;

            UploadSpawnSetToBoidBuffers(center, 0xB01D2E57u, 0xB01D5A7Eu, true);
            boundsCenter = center;
            _simulationBounds.center = center;
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
                _dispatchGroupCount = (boidCount + _threadGroupSizeX - 1) / _threadGroupSizeX;
                EnsureRuntimeBufferCapacity();
                _simulationBounds = new Bounds(boundsCenter, boundsSize * 2f);
            }
        }
#endif
    }
}

