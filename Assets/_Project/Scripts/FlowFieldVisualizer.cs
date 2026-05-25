// ============================================================================
// HECTON-8 — FlowFieldVisualizer.cs
// Vizualizator vektornogo polya techeniy v redaktore.
//
// Risuet strelki/linii, pokazyvayuschie napravlenie i silu techeniy
// v zadannoy oblasti. Polezno dlya nastroyki CurrentVolume i globalnyh techeniy.
//
// ARHITEKTURA:
//   - Singleton dlya legkogo dostupa iz menyu
//   - Gizmos dlya renderinga (OnDrawGizmosSelected)
//   - Sampling v grid'e dlya proizvoditelnosti
//   - Tsvetovaya kodirovka sily techeniya
//
// PROIZVODITELNOST:
//   - Grid sampling: O(gridSize²) vmesto O(continuous)
//   - Tolko v Selected Gizmos (ne vsegda risuetsya)
//   - Burst-compatible sampling cherez CurrentManager
// ============================================================================

using System.Text;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Profiling;
using UnityEngine;
using System.Globalization;
using Unity.Mathematics;
using Hecton8.Core;
using Hecton8.World;

namespace Hecton8.Physics
{
    /// <summary>Stili vizualizatsii strelok techeniya</summary>
    public enum ArrowStyle
    {
        /// <summary>Prostye strelki s nakonechnikami</summary>
        Arrows = 0,

        /// <summary>Linii bez nakonechnikov (bystree)</summary>
        Lines = 1,

        /// <summary>Konusy (bolee zametnye)</summary>
        Cones = 2,

        /// <summary>Tsvetnye tochki (minimalnaya proizvoditelnost)</summary>
        Dots = 3
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton/Tools/Flow Field Visualizer")]
    public sealed class FlowFieldVisualizer : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════
        //  SINGLETON
        // ══════════════════════════════════════════════════════════

        private const NativeAllocationLifetime NativeTempJobLifetime = NativeAllocationLifetime.TempJob;
        private const int SelectedVolumeCapacity = 32;
        private const int ParticlePreviewCapacity = 32;
        private const int ForceLabelBuilderCapacity = 32;

        [Header("── Profile ───────────────────────────────────")]
        [Tooltip("Profil nastroek (optsionalno). Avtomaticheski primenyaet nastroyki pri vybore.")]
        [SerializeField] private FlowFieldProfile profile;

        [Header("── Grid Settings ─────────────────────────────")]
        [Tooltip("Razmer oblasti vizualizatsii (metry)")]
        [SerializeField] private Vector2 areaSize = new Vector2(50f, 50f);

        [Tooltip("Razreshenie grid'a (kolichestvo strelok po X i Z)")]
        [SerializeField] private Vector2Int gridResolution = new Vector2Int(20, 20);

        [Tooltip("Vysota semplinga nad poverhnostyu vody (Y-offset)")]
        [SerializeField] private float sampleHeight = 0.5f;

        [Header("── Arrow Settings ────────────────────────────")]
        [Tooltip("Dlina strelok (metry). Avtomaticheski masshtabiruetsya po sile.")]
        [SerializeField] private float arrowLength = 2f;

        [Tooltip("Tolschina liniy strelok")]
        [SerializeField] private float arrowThickness = 0.05f;

        [Tooltip("Masshtab sily dlya tsvetovoy kodirovki (0 = siniy, max = krasnyy)")]
        [SerializeField] private float maxForceScale = 5f;

        [Header("── Performance ──────────────────────────────")]
        [Tooltip("Maksimalnoe razreshenie grid'a dlya predotvrascheniya zavisaniy")]
        [SerializeField, Range(5, 100)] private int maxGridResolution = 50;

        [Tooltip("Porog dlya asinhronnogo rascheta (kletok)")]
        [SerializeField] private int asyncThreshold = 1000;

        [Tooltip("Taymaut asinhronnogo rascheta (sek)")]
        [SerializeField, Range(0.1f, 10f)] private float asyncTimeout = 2f;

        [Tooltip("Ispolzovat Burst dlya raschetov (bystree, no trebuet kompilyatsii)")]
        [SerializeField] private bool useBurstSampling = true;

        [Tooltip("Ispolzovat Job System dlya parallelnogo semplinga")]
        [SerializeField] private bool useJobSystem = true;

        [Header("── Advanced Visualization ──────────────────")]
        [Tooltip("HDR tsveta dlya luchshey vidimosti")]
        [SerializeField] private bool useHDRColors = true;

        [Tooltip("Plavnaya animatsiya v editor (dlya preview)")]
        [SerializeField] private bool animateInEditor = false;

        [Tooltip("Skorost animatsii (dlya preview)")]
        [SerializeField, Range(0.1f, 5f)] private float animationSpeed = 1f;

        [Tooltip("Ispolzovat particle effects dlya silnyh techeniy")]
        [SerializeField] private bool useParticleEffects = false;

        [Tooltip("Particle system prefab dlya effektov")]
        [SerializeField] private GameObject particlePrefab;

        [Header("── Current Sources ─────────────────────────")]
        [Tooltip("Vizualizirovat globalnoe phantom techenie iz HectonFluidEngine")]
        [SerializeField] private bool showGlobalCurrent = true;

        [Tooltip("Vizualizirovat lokalnye CurrentVolume obekty")]
        [SerializeField] private bool showLocalCurrents = true;

        [Tooltip("Ogranichit tolko vybrannymi CurrentVolume (iz spiska nizhe)")]
        [SerializeField] private bool onlySelectedVolumes = false;

        [Tooltip("Spisok vybrannyh CurrentVolume dlya vizualizatsii")]
        [SerializeField] private List<CurrentVolume> selectedVolumes = new List<CurrentVolume>(SelectedVolumeCapacity); // COLD ALLOC: List<CurrentVolume>[32] — selected current-volume preview filter — owner: FlowFieldVisualizer

        [Header("── Visualization ────────────────────────────")]
        [Tooltip("Stil vizualizatsii strelok")]
        [SerializeField] private ArrowStyle arrowStyle = ArrowStyle.Arrows;

        [Tooltip("Pokazyvat chislovye znacheniya sily")]
        [SerializeField] private bool showForceLabels = false;

        [Header("── LOD / Culling ───────────────────────────")]
        [Tooltip("Ispolzovat LOD po rasstoyaniyu")]
        [SerializeField] private bool useLod = true;

        [Tooltip("Minimalnaya distantsiya dlya otobrazheniya (m)")]
        [SerializeField] private float lodMinDistance = 0f;

        [Tooltip("Maksimalnaya distantsiya dlya otobrazheniya (m)")]
        [SerializeField] private float lodMaxDistance = 120f;

        [Tooltip("Ugolnoe porog (dot) dlya prosmotra/napravleniya")]
        [SerializeField, Range(-1f, 1f)] private float lodDotThreshold = -0.3f;

        [Tooltip("Razmer shrifta dlya leyblov")]
        [SerializeField, Range(8, 24)] private int labelFontSize = 12;

        [Tooltip("Pokazyvat tolko znachimye techeniya (vyshe poroga)")]
        [SerializeField] private bool cullWeakFlows = true;

        [Tooltip("Minimalnaya sila dlya otobrazheniya (m/s)")]
        [SerializeField, Range(0.01f, 1f)] private float minFlowStrength = 0.1f;

        [Header("── Debug / Diagnostics ────────────────────")]
        [Tooltip("Pokazyvat panel diagnostiki (v stsene)")]
        [SerializeField] private bool showDebugInfo = true;

        [Tooltip("Pokazyvat detalizatsiyu vremeni rascheta")]
        [SerializeField] private bool showPerformanceStats = true;

        // ══════════════════════════════════════════════════════════
        //  CACHED DATA
        // ══════════════════════════════════════════════════════════

        /// <summary>Keshirovannye pozitsii grid'a dlya semplinga</summary>
        private Vector3[] _samplePositions;

        /// <summary>Keshirovannye vektory techeniy dlya kazhdogo grid-tochki</summary>
        private Vector3[] _flowVectors;

        /// <summary>Keshirovannye velichiny techeniy dlya optimizatsii</summary>
        private float[] _flowMagnitudes;

        /// <summary>Nuzhno li pereschitat kesh (pri izmenenii nastroek)</summary>
        private bool _needsRecalculation = true;

        /// <summary>Profiler marker dlya Recalculate</summary>
        private static readonly ProfilerMarker RecalculateMarker
            = new ProfilerMarker("FlowFieldVisualizer.Recalculate");
        private HectonFluidEngine _subscribedFluidEngine;

        /// <summary>Object pool dlya particle effects (editor-only)</summary>
        private class ParticlePool
        {
            private readonly System.Func<ParticleSystem> _createFunc;
            private readonly System.Action<ParticleSystem> _getAction;
            private readonly System.Action<ParticleSystem> _releaseAction;
            private readonly Queue<ParticleSystem> _pool = new Queue<ParticleSystem>(ParticlePreviewCapacity); // COLD ALLOC: Queue<ParticleSystem>[32] - editor current-flow particle preview pool - owner: FlowFieldVisualizer.ParticlePool

            public ParticlePool(System.Func<ParticleSystem> createFunc,
                              System.Action<ParticleSystem> getAction = null,
                              System.Action<ParticleSystem> releaseAction = null)
            {
                _createFunc = createFunc;
                _getAction = getAction;
                _releaseAction = releaseAction;
            }

            public ParticleSystem Get()
            {
                ParticleSystem item = _pool.Count > 0 ? _pool.Dequeue() : _createFunc();
                _getAction?.Invoke(item);
                return item;
            }

            public void Release(ParticleSystem item)
            {
                _releaseAction?.Invoke(item);
                if (_pool.Count >= ParticlePreviewCapacity)
                {
                    if (item != null)
                        Object.DestroyImmediate(item.gameObject);
                    return;
                }

                _pool.Enqueue(item);
            }

            public void Dispose()
            {
                while (_pool.Count > 0)
                {
                    ParticleSystem item = _pool.Dequeue();
                    if (item != null)
                    {
                        Object.DestroyImmediate(item.gameObject);
                    }
                }
            }
        }

        /// <summary>Pul dlya particle systems</summary>
        private ParticlePool _particlePool;

        /// <summary>Aktivnye particle systems s vremenem zhizni</summary>
        private readonly List<(ParticleSystem particle, float expireTime)> _activeParticles = new List<(ParticleSystem, float)>(ParticlePreviewCapacity); // COLD ALLOC: List<(ParticleSystem,float)>[32] — editor current-flow particle preview pool lease tracking — owner: FlowFieldVisualizer

        private static readonly StringBuilder _forceLabelBuilder = new StringBuilder(ForceLabelBuilderCapacity); // COLD ALLOC: StringBuilder[32] — editor force-label scratch buffer — owner: FlowFieldVisualizer

        /// <summary>Keshirovannyy GUIStyle dlya leyblov (menshe GC v OnDrawGizmos).</summary>
        private GUIStyle _cachedLabelStyle;

        /// <summary>Kesh aktivnoy kamery stseny dlya izbezhaniya povtornogo resolve.</summary>
        private Camera _cachedMainCamera;

        private readonly List<CurrentVolume> _volumeScratch = new List<CurrentVolume>(SelectedVolumeCapacity); // COLD ALLOC: List<CurrentVolume>[32] — current-volume job-data gather scratch — owner: FlowFieldVisualizer

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct CurrentVolumeJobData
        {
            public int Shape; // 0 = box, 1 = sphere
            public float3 Position;
            public quaternion Rotation;
            public float3 HalfSize;
            public float SphereRadius;
            public float3 Direction;
            public float Strength;
            public float VerticalFactor;
            public float EdgeSoftness;
            public float PulseAmplitude;
            public float PulseFrequency;
            public float PhaseOffset;
        }

        /// <summary>Poslednee vremya rasscheta (sekundy)</summary>
        private float _lastCalculationTime = 0f;

        /// <summary>Poslednee kolichestvo tochek</summary>
        private int _lastPointCount = 0;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC PROPERTIES (dlya dostupa iz profiley i koda)
        // ══════════════════════════════════════════════════════════

        public FlowFieldProfile Profile
        {
            get => profile;
            set
            {
                profile = value;
                if (profile != null)
                    profile.ApplyTo(this);
            }
        }

        public Vector2 AreaSize
        {
            get => areaSize;
            set
            {
                areaSize = value;
                _needsRecalculation = true;
            }
        }

        public Vector2Int GridResolution
        {
            get => gridResolution;
            set
            {
                gridResolution = value;
                _needsRecalculation = true;
            }
        }

        public float SampleHeight
        {
            get => sampleHeight;
            set
            {
                sampleHeight = value;
                _needsRecalculation = true;
            }
        }

        public float ArrowLength
        {
            get => arrowLength;
            set => arrowLength = value;
        }

        public float ArrowThickness
        {
            get => arrowThickness;
            set => arrowThickness = value;
        }

        public float MaxForceScale
        {
            get => maxForceScale;
            set => maxForceScale = value;
        }

        public bool ShowGlobalCurrent
        {
            get => showGlobalCurrent;
            set
            {
                showGlobalCurrent = value;
                _needsRecalculation = true;
            }
        }

        public bool ShowLocalCurrents
        {
            get => showLocalCurrents;
            set
            {
                showLocalCurrents = value;
                _needsRecalculation = true;
            }
        }

        public bool OnlySelectedVolumes
        {
            get => onlySelectedVolumes;
            set
            {
                onlySelectedVolumes = value;
                _needsRecalculation = true;
            }
        }

        public List<CurrentVolume> SelectedVolumes => selectedVolumes;

        public int MaxGridResolution
        {
            get => maxGridResolution;
            set => maxGridResolution = Mathf.Max(5, value);
        }

        public ArrowStyle ArrowStyle
        {
            get => arrowStyle;
            set => arrowStyle = value;
        }

        public bool ShowForceLabels
        {
            get => showForceLabels;
            set => showForceLabels = value;
        }

        public bool CullWeakFlows
        {
            get => cullWeakFlows;
            set => cullWeakFlows = value;
        }

        public float MinFlowStrength
        {
            get => minFlowStrength;
            set => minFlowStrength = Mathf.Max(0.01f, value);
        }

        public int AsyncThreshold
        {
            get => asyncThreshold;
            set => asyncThreshold = Mathf.Max(100, value);
        }

        public float AsyncTimeout
        {
            get => asyncTimeout;
            set => asyncTimeout = Mathf.Max(0.1f, value);
        }

        public int LabelFontSize
        {
            get => labelFontSize;
            set => labelFontSize = Mathf.Clamp(value, 8, 24);
        }

        public float AnimationSpeed
        {
            get => animationSpeed;
            set => animationSpeed = Mathf.Max(0.1f, value);
        }

        public bool UseBurstSampling
        {
            get => useBurstSampling;
            set
            {
                useBurstSampling = value;
                _needsRecalculation = true;
            }
        }

        public bool UseJobSystem
        {
            get => useJobSystem;
            set
            {
                useJobSystem = value;
                _needsRecalculation = true;
            }
        }

        public bool UseHDRColors
        {
            get => useHDRColors;
            set => useHDRColors = value;
        }

        public bool ShowDebugInfo
        {
            get => showDebugInfo;
            set => showDebugInfo = value;
        }

        public bool ShowPerformanceStats
        {
            get => showPerformanceStats;
            set => showPerformanceStats = value;
        }

        public bool AnimateInEditor
        {
            get => animateInEditor;
            set => animateInEditor = value;
        }

        public bool UseParticleEffects
        {
            get => useParticleEffects;
            set => useParticleEffects = value;
        }

        public GameObject ParticlePrefab
        {
            get => particlePrefab;
            set => particlePrefab = value;
        }

        /// <summary>
        /// Forces the visualizer to rebuild its cached flow samples.
        /// </summary>
        public void Recalculate()
        {
            _needsRecalculation = true;
        }

        // ══════════════════════════════════════════════════════════
        //  VISUALIZATION LOGIC
        // ══════════════════════════════════════════════════════════

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct FlowSamplingJob : IJobParallelFor
        {
            [ReadOnly, NoAlias] public NativeArray<float3> SamplePositions;
            [NoAlias] public NativeArray<float3> FlowVectors;
            [ReadOnly, NoAlias] public NativeArray<CurrentVolumeJobData> VolumeData;
            public int VolumeCount;
            public byte ShowGlobalCurrent;
            public float Time;
            public float NoiseScale;
            public float TimeScale;
            public float Strength;
            public float VerticalFactor;
            public byte ShowLocalCurrents;

            public void Execute(int index)
            {
                float3 pos = SamplePositions[index];
                float3 flow = ShowGlobalCurrent != 0
                    ? CurrentManager.SampleCurrent(pos, Time, NoiseScale, TimeScale, Strength, VerticalFactor)
                    : float3.zero;

                if (ShowLocalCurrents != 0 && VolumeCount > 0)
                {
                    for (int v = 0; v < VolumeCount; v++)
                    {
                        var vol = VolumeData[v];
                        float weight = 0f;

                        if (vol.Shape == 0)
                        {
                            float3 local = math.mul(math.inverse(vol.Rotation), pos - vol.Position);
                            if (math.abs(local.x) <= vol.HalfSize.x && math.abs(local.y) <= vol.HalfSize.y && math.abs(local.z) <= vol.HalfSize.z)
                            {
                                float3 safe = new float3(
                                    vol.HalfSize.x > 0f ? 1f - math.abs(local.x) / vol.HalfSize.x : 1f,
                                    vol.HalfSize.y > 0f ? 1f - math.abs(local.y) / vol.HalfSize.y : 1f,
                                    vol.HalfSize.z > 0f ? 1f - math.abs(local.z) / vol.HalfSize.z : 1f);
                                float edge = math.min(safe.x, math.min(safe.y, safe.z));
                                weight = math.clamp(edge / math.max(0.01f, vol.EdgeSoftness), 0f, 1f);
                            }
                        }
                        else
                        {
                            float d = ApproximateVectorMagnitude(pos - vol.Position);
                            if (d < vol.SphereRadius)
                            {
                                float edge = 1f - d / math.max(0.01f, vol.SphereRadius);
                                weight = math.clamp(edge / math.max(0.01f, vol.EdgeSoftness), 0f, 1f);
                            }
                        }

                        if (weight > 0f)
                        {
                            float pulse = 1f;
                            if (vol.PulseAmplitude > 0f && vol.PulseFrequency > 0f)
                            {
                                pulse += FastTriangleSigned(Time * vol.PulseFrequency * 6.2831855f + vol.PhaseOffset) * vol.PulseAmplitude;
                            }

                            flow += vol.Direction * (vol.Strength * pulse * weight);
                        }
                    }
                }

                FlowVectors[index] = flow;
            }
        }

        /// <summary>
        /// Osnovnaya funktsiya vizualizatsii. Vyzyvaetsya Unity v OnDrawGizmosSelected.
        /// </summary>
        public void DrawFlowField()
        {
            if (_needsRecalculation)
            {
                RecalculateFlowField();
                _needsRecalculation = false;
            }

            if (_samplePositions == null || _flowVectors == null)
                return;

            // Animatsionnyy faktor dlya preview
            float animationFactor = animateInEditor ?
                FastTriangleSigned(Time.realtimeSinceStartup * animationSpeed) * 0.5f + 0.5f : 1f;

            // Risuem strelki dlya kazhdoy tochki grid'a
            UpdateActiveParticles();

            GetVisualizationCameraPose(out Vector3 camPos, out Vector3 camForward);

            float lodMaxDistanceSq = lodMaxDistance * lodMaxDistance;
            float lodMinDistanceSq = lodMinDistance * lodMinDistance;

            for (int i = 0; i < _samplePositions.Length; i++)
            {
                Vector3 pos = _samplePositions[i];
                Vector3 flow = _flowVectors[i];
                float magnitude = _flowMagnitudes[i];

                if (useLod)
                {
                    Vector3 toPos = pos - camPos;
                    float distSq = toPos.sqrMagnitude;
                    if (distSq > lodMaxDistanceSq || distSq < lodMinDistanceSq)
                        continue;

                    if (IsOutsideLodConeSq(toPos, camForward, lodDotThreshold, distSq))
                        continue;
                }

                // Filtratsiya slabyh techeniy
                if (cullWeakFlows && magnitude < minFlowStrength)
                    continue;

                // Animirovannaya sila dlya preview
                float animatedMagnitude = animateInEditor ?
                    magnitude * (0.5f + animationFactor * 0.5f) : magnitude;

                DrawFlowArrow(pos, flow, animatedMagnitude);
            }

            if (showDebugInfo)
            {
                DrawDebugPanel();
            }
        }

        /// <summary>Obnovlyaet ssylki na keshirovannye komponenty, ispolzuemye dlya vizualizatsii.</summary>
        private void EnsureSampleBuffers(int totalPoints)
        {
            if (_samplePositions == null || _samplePositions.Length != totalPoints ||
                _flowVectors == null || _flowVectors.Length != totalPoints ||
                _flowMagnitudes == null || _flowMagnitudes.Length != totalPoints)
            {
                _samplePositions = new Vector3[totalPoints];
                _flowVectors = new Vector3[totalPoints];
                _flowMagnitudes = new float[totalPoints];
            }
        }

        private void UpdateCaches()
        {
            Camera currentCamera = Camera.current;

#if UNITY_EDITOR
            if (currentCamera == null)
                currentCamera = UnityEditor.SceneView.lastActiveSceneView?.camera;
#endif

            if (currentCamera != null)
                _cachedMainCamera = currentCamera;
        }

        /// <summary>Risuet tekuschie statistiki raboty v stsene</summary>
        private void DrawDebugPanel()
        {
#if UNITY_EDITOR
            if (Event.current == null)
                return;

            using (var scope = StringBuilderScope.Get())
            {
                var sb = scope.Value;
                sb.AppendLine("FlowFieldVisualizer:");
                sb.Append("  Resolution: ").Append(gridResolution.x).Append('x').Append(gridResolution.y).AppendLine();
                sb.Append("  Total points: ").Append(_samplePositions != null ? _samplePositions.Length : 0).AppendLine();
                sb.AppendLine("  Sampling mode: immediate");

                if (showPerformanceStats)
                {
                    float throughput = _lastCalculationTime > 0f ? _lastPointCount / _lastCalculationTime : 0f;
                    sb.Append("  Last calc: ").Append(_lastCalculationTime.ToString("F3", CultureInfo.InvariantCulture)).Append("s (").Append(_lastPointCount).Append(" points)").AppendLine();
                    sb.Append("  Throughput: ").Append(throughput.ToString("F0", CultureInfo.InvariantCulture)).AppendLine(" pts/s");
                }

                Vector3 labelPos = transform.position + Vector3.up * (sampleHeight + 0.5f);
                UnityEditor.Handles.Label(labelPos, sb.ToString(), _cachedLabelStyle);
            }
#endif
        }

        /// <summary>Obnovlyaet srok zhizni vremennyh particle effects</summary>
        private void UpdateActiveParticles()
        {
            float now = Time.realtimeSinceStartup;
            for (int i = _activeParticles.Count - 1; i >= 0; i--)
            {
                var (ps, expireTime) = _activeParticles[i];
                if (ps == null || !ps.gameObject.activeSelf || now >= expireTime)
                {
                    if (ps != null && _particlePool != null)
                    {
                        _particlePool.Release(ps);
                    }
                    _activeParticles.RemoveAt(i);
                }
            }
        }

        /// <summary>Osvobozhdaet vse aktivnye particle effects pri otklyuchenii instrumenta.</summary>
        private void ClearActiveParticles()
        {
            for (int i = 0; i < _activeParticles.Count; i++)
            {
                ParticleSystem ps = _activeParticles[i].particle;
                if (ps != null && _particlePool != null)
                {
                    _particlePool.Release(ps);
                }
            }
            _activeParticles.Clear();
        }

        private void DisposeParticlePreviewPool()
        {
            ClearActiveParticles();

            if (_particlePool != null)
            {
                _particlePool.Dispose();
                _particlePool = null;
            }
        }

        /// <summary>
        /// Pereschityvaet flow field dlya tekuschih nastroek.
        /// Sozdaet grid tochek i semplit techeniya v kazhdoy.
        /// </summary>
        private void RecalculateFlowField()
        {
            // Validatsiya nastroek
            ValidateSettings();

            int totalPoints = gridResolution.x * gridResolution.y;

            // Zaschita ot slishkom bolshih grid'ov
            if (totalPoints > maxGridResolution * maxGridResolution)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("[FlowFieldVisualizer] Grid resolution clamped to configured maximum.", this);
#endif
                gridResolution.x = Mathf.Min(gridResolution.x, maxGridResolution);
                gridResolution.y = Mathf.Min(gridResolution.y, maxGridResolution);
                totalPoints = gridResolution.x * gridResolution.y;
            }

            // Proverka na razumnye razmery
            if (totalPoints <= 0)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError("[FlowFieldVisualizer] Invalid grid resolution. Must be > 0.", this);
#endif
                return;
            }

            EnsureSampleBuffers(totalPoints);

            PerformCalculation(totalPoints);
        }

        /// <summary>Vypolnyaet fakticheskiy raschet techeniy</summary>
        private void PerformCalculation(int totalPoints)
        {
            if (HectonFloatingOrigin.IsShiftInProgress)
                return;

            using (RecalculateMarker.Auto())
            {
                float calculationStartTime = Time.realtimeSinceStartup;
                uint calculationShiftSequence = HectonFloatingOrigin.CurrentShiftSequence;
                EnsureSampleBuffers(totalPoints);
                PrepareSamplePositions(totalPoints);

                if (useJobSystem && useBurstSampling)
                {
                    HectonFluidEngine engine = GlobalRegistry.Fluid;
                    bool includeGlobalCurrent = showGlobalCurrent && engine != null;

                    var positions = new NativeArray<float3>(totalPoints, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                    var flowResults = new NativeArray<float3>(totalPoints, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                    var volumeData = BuildVolumeJobData(Allocator.TempJob);
                    NativeMemorySentinel.RegisterNativeArray(positions, nameof(FlowFieldVisualizer), "syncPositions", NativeTempJobLifetime);
                    NativeMemorySentinel.RegisterNativeArray(flowResults, nameof(FlowFieldVisualizer), "syncFlowResults", NativeTempJobLifetime);
                    NativeMemorySentinel.RegisterNativeArray(volumeData, nameof(FlowFieldVisualizer), "syncVolumeData", NativeTempJobLifetime);

                    try
                    {
                        for (int i = 0; i < totalPoints; i++)
                        {
                            Vector3 p = _samplePositions[i];
                            positions[i] = new float3(p.x, p.y, p.z);
                        }

                        var samplingJob = new FlowSamplingJob
                        {
                            SamplePositions = positions,
                            FlowVectors = flowResults,
                            VolumeData = volumeData,
                            VolumeCount = volumeData.IsCreated ? volumeData.Length : 0,
                            ShowGlobalCurrent = includeGlobalCurrent ? (byte)1 : (byte)0,
                            Time = Time.realtimeSinceStartup,
                            NoiseScale = includeGlobalCurrent ? engine.CurrentNoiseScale : 0f,
                            TimeScale = includeGlobalCurrent ? engine.CurrentTimeScale : 0f,
                            Strength = includeGlobalCurrent ? engine.PhantomCurrentStrength : 0f,
                            VerticalFactor = includeGlobalCurrent ? engine.CurrentVerticalFactor : 0f,
                            ShowLocalCurrents = showLocalCurrents ? (byte)1 : (byte)0
                        };

                        for (int i = 0; i < totalPoints; i++)
                        {
                            samplingJob.Execute(i);
                        }
                        if (HectonFloatingOrigin.IsShiftInProgress ||
                            calculationShiftSequence != HectonFloatingOrigin.CurrentShiftSequence)
                        {
                            return;
                        }

                        for (int i = 0; i < totalPoints; i++)
                        {
                            float3 sampledFlow = flowResults[i];
                            Vector3 flow = new Vector3(sampledFlow.x, sampledFlow.y, sampledFlow.z);
                            _flowVectors[i] = flow;
                            _flowMagnitudes[i] = ApproximateVectorMagnitude(flow);
                        }

                        _lastCalculationTime = Time.realtimeSinceStartup - calculationStartTime;
                        _lastPointCount = totalPoints;
                    }
                    finally
                    {
                        if (volumeData.IsCreated)
                        {
                            NativeMemorySentinel.UnregisterNativeArray(volumeData);
                            volumeData.Dispose();
                        }

                        if (flowResults.IsCreated)
                        {
                            NativeMemorySentinel.UnregisterNativeArray(flowResults);
                            flowResults.Dispose();
                        }

                        if (positions.IsCreated)
                        {
                            NativeMemorySentinel.UnregisterNativeArray(positions);
                            positions.Dispose();
                        }
                    }

                    return;
                }

                for (int i = 0; i < totalPoints; i++)
                {
                    Vector3 flow = SampleCurrentAt(_samplePositions[i]);
                    _flowVectors[i] = flow;
                    _flowMagnitudes[i] = ApproximateVectorMagnitude(flow);
                }

                _lastCalculationTime = Time.realtimeSinceStartup - calculationStartTime;
                _lastPointCount = totalPoints;
            }
        }

        private static float ApproximateVectorMagnitude(Vector3 value)
        {
            float ax = math.abs(value.x);
            float ay = math.abs(value.y);
            float az = math.abs(value.z);
            float max = math.max(ax, math.max(ay, az));
            float min = math.min(ax, math.min(ay, az));
            float mid = ax + ay + az - max - min;
            return max + (mid * 0.375f) + (min * 0.125f);
        }

        private static float ApproximateVectorMagnitude(float3 value)
        {
            float ax = math.abs(value.x);
            float ay = math.abs(value.y);
            float az = math.abs(value.z);
            float max = math.max(ax, math.max(ay, az));
            float min = math.min(ax, math.min(ay, az));
            float mid = ax + ay + az - max - min;
            return max + (mid * 0.375f) + (min * 0.125f);
        }

        private static float FastTriangleSigned(float phase)
        {
            float triangle01 = 1f - math.abs(math.frac(phase * 0.15915494f + 0.25f) * 2f - 1f);
            return triangle01 * 2f - 1f;
        }

        private static Vector3 DominantAxisOrDefault(Vector3 value, Vector3 fallback)
        {
            float lengthSq = value.x * value.x + value.y * value.y + value.z * value.z;
            if (lengthSq <= 0.0001f)
                return fallback;

            float ax = math.abs(value.x);
            float ay = math.abs(value.y);
            float az = math.abs(value.z);
            if (ax >= ay && ax >= az)
                return new Vector3(value.x >= 0f ? 1f : -1f, 0f, 0f);

            if (ay >= az)
                return new Vector3(0f, value.y >= 0f ? 1f : -1f, 0f);

            return new Vector3(0f, 0f, value.z >= 0f ? 1f : -1f);
        }

        private static Color BlendColorUnclamped(Color from, Color to, float t)
        {
            return new Color(
                from.r + ((to.r - from.r) * t),
                from.g + ((to.g - from.g) * t),
                from.b + ((to.b - from.b) * t),
                from.a + ((to.a - from.a) * t));
        }

        private static bool IsOutsideLodConeSq(Vector3 toPosition, Vector3 cameraForward, float dotThreshold, float distanceSq)
        {
            float dot = Vector3.Dot(toPosition, cameraForward);
            float thresholdSq = dotThreshold * dotThreshold * distanceSq;
            float dotSq = dot * dot;

            if (dotThreshold >= 0f)
                return dot <= 0f || dotSq < thresholdSq;

            return dot < 0f && dotSq > thresholdSq;
        }

        private void PrepareSamplePositions(int totalPoints)
        {
            Vector3 center = transform.position;
            Vector3 start = center - new Vector3(areaSize.x * 0.5f, 0f, areaSize.y * 0.5f);

            float stepX = areaSize.x / Mathf.Max(1, gridResolution.x - 1);
            float stepZ = areaSize.y / Mathf.Max(1, gridResolution.y - 1);

            int index = 0;
            for (int z = 0; z < gridResolution.y; z++)
            {
                for (int x = 0; x < gridResolution.x; x++)
                {
                    _samplePositions[index] = start + new Vector3(x * stepX, sampleHeight, z * stepZ);
                    index++;
                }
            }
        }

        private NativeArray<CurrentVolumeJobData> BuildVolumeJobData(Allocator allocator)
        {
            if (!showLocalCurrents || HectonFloatingOrigin.IsShiftInProgress)
                return default;

            CollectVolumes(_volumeScratch);
            if (_volumeScratch.Count <= 0)
                return default;

            var volumeData = new NativeArray<CurrentVolumeJobData>(_volumeScratch.Count, allocator, NativeArrayOptions.ClearMemory);

            for (int i = 0; i < _volumeScratch.Count; i++)
            {
                CurrentVolume volume = _volumeScratch[i];
                Transform volumeTransform = volume.transform;
                Vector3 volumePosition = volumeTransform.position;
                Quaternion volumeRotation = volumeTransform.rotation;
                Vector3 localFlowDirection = DominantAxisOrDefault(volume.LocalDirection, Vector3.forward);
                Vector3 worldFlowDirection = volumeTransform.TransformDirection(localFlowDirection);
                Vector3 shapedFlowDirection = Vector3.Scale(
                    worldFlowDirection,
                    new Vector3(1f, volume.VerticalFactor, 1f));
                volumeData[i] = new CurrentVolumeJobData
                {
                    Shape = (int)volume.Shape,
                    Position = new float3(volumePosition.x, volumePosition.y, volumePosition.z),
                    Rotation = volumeRotation,
                    HalfSize = new float3(volume.BoxSize.x, volume.BoxSize.y, volume.BoxSize.z) * 0.5f,
                    SphereRadius = Mathf.Max(0.01f, volume.SphereRadius),
                    Direction = DominantAxisOrDefault(shapedFlowDirection, Vector3.forward),
                    Strength = volume.Strength,
                    VerticalFactor = volume.VerticalFactor,
                    EdgeSoftness = Mathf.Clamp01(volume.EdgeSoftness),
                    PulseAmplitude = volume.PulseAmplitude,
                    PulseFrequency = volume.PulseFrequency,
                    PhaseOffset = volume.PhaseOffset
                };
            }

            return volumeData;
        }

        /// <summary>Validiruet nastroyki pered ispolzovaniem</summary>
        private void ValidateSettings()
        {
            // Proverka selected volumes
            if (onlySelectedVolumes && selectedVolumes != null)
            {
                for (int i = selectedVolumes.Count - 1; i >= 0; i--)
                {
                    if (selectedVolumes[i] == null)
                        selectedVolumes.RemoveAt(i);
                }
            }

            if (gridResolution.x > maxGridResolution || gridResolution.y > maxGridResolution)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("[FlowFieldVisualizer] Grid too large. Clamping to configured maximum.", this);
#endif
            }

            // Korrektsiya nedopustimyh znacheniy
            gridResolution.x = Mathf.Clamp(gridResolution.x, 2, maxGridResolution);
            gridResolution.y = Mathf.Clamp(gridResolution.y, 2, maxGridResolution);
            areaSize.x = Mathf.Max(1f, areaSize.x);
            areaSize.y = Mathf.Max(1f, areaSize.y);
            sampleHeight = Mathf.Max(0f, sampleHeight);
            arrowLength = Mathf.Max(0.1f, arrowLength);
            arrowThickness = Mathf.Max(0.01f, arrowThickness);
            maxForceScale = Mathf.Max(0.1f, maxForceScale);
            minFlowStrength = Mathf.Max(0.01f, minFlowStrength);

            // Keshiruem stil teksta dlya leyblov, chtoby ne allotsirovat GUIStyle na kazhdom kadre
            if (_cachedLabelStyle == null)
            {
                _cachedLabelStyle = new GUIStyle();
            }
            _cachedLabelStyle.fontSize = Mathf.Clamp(labelFontSize, 8, 24);
            _cachedLabelStyle.normal.textColor = Color.white;
            _cachedLabelStyle.alignment = TextAnchor.MiddleCenter;
        }

        private void CollectVolumes(List<CurrentVolume> output)
        {
            output.Clear();

            if (onlySelectedVolumes)
            {
                if (selectedVolumes == null)
                    return;

                for (int i = 0; i < selectedVolumes.Count; i++)
                {
                    CurrentVolume volume = selectedVolumes[i];
                    if (volume != null && volume.isActiveAndEnabled)
                        output.Add(volume);
                }

                return;
            }

            int activeVolumeCount = CurrentVolume.ActiveCount;
            for (int i = 0; i < activeVolumeCount; i++)
            {
                CurrentVolume volume = CurrentVolume.GetActiveVolumeAt(i);
                if (volume != null && volume.isActiveAndEnabled)
                    output.Add(volume);
            }
        }

        private void GetVisualizationCameraPose(out Vector3 cameraPosition, out Vector3 cameraForward)
        {
            Camera currentCamera = Camera.current ?? _cachedMainCamera;

#if UNITY_EDITOR
            if (currentCamera == null)
                currentCamera = UnityEditor.SceneView.lastActiveSceneView?.camera;
#endif

            if (currentCamera != null)
            {
                _cachedMainCamera = currentCamera;
                Transform cameraTransform = currentCamera.transform;
                cameraPosition = cameraTransform.position;
                cameraForward = cameraTransform.forward;
                return;
            }

            cameraPosition = transform.position;
            cameraForward = transform.forward;
        }

        /// <summary>
        /// Semplit vektor techeniya v mirovoy tochke.
        /// Kombiniruet globalnoe phantom techenie + lokalnye CurrentVolume.
        /// </summary>
        private Vector3 SampleCurrentAt(Vector3 worldPos)
        {
            Vector3 totalFlow = Vector3.zero;

            // Globalnoe phantom techenie
            HectonFluidEngine engine = GlobalRegistry.Fluid;
            if (showGlobalCurrent && engine != null)
            {
                float3 pos = new float3(worldPos.x, worldPos.y, worldPos.z);

                float3 sampledFlow = CurrentManager.SampleCurrent(
                    pos,
                    Time.realtimeSinceStartup, // Ispolzuem realtime dlya preview
                    engine.CurrentNoiseScale,
                    engine.CurrentTimeScale,
                    engine.PhantomCurrentStrength,
                    engine.CurrentVerticalFactor
                );
                totalFlow += new Vector3(sampledFlow.x, sampledFlow.y, sampledFlow.z);
            }

            // Lokalnye CurrentVolume
            if (showLocalCurrents)
            {
                if (onlySelectedVolumes)
                {
                    // Tolko vybrannye volumes
                    for (int i = 0; i < selectedVolumes.Count; i++)
                    {
                        CurrentVolume volume = selectedVolumes[i];
                        if (volume != null && volume.isActiveAndEnabled)
                            totalFlow += volume.Sample(worldPos);
                    }
                }
                else
                {
                    // Vse aktivnye volumes
                    totalFlow += CurrentVolume.SampleAt(worldPos);
                }
            }

            return totalFlow;
        }

        /// <summary>
        /// Risuet strelku techeniya v zadannoy pozitsii.
        /// Podderzhivaet raznye stili vizualizatsii.
        /// </summary>
        private void DrawFlowArrow(Vector3 position, Vector3 flow, float magnitude)
        {
            // Filtratsiya slabyh techeniy
            if (cullWeakFlows && magnitude < minFlowStrength)
                return;

            // Tsvetovaya kodirovka sily s podderzhkoy HDR
            float t = math.saturate(magnitude / math.max(maxForceScale, 0.0001f));
            Color color = useHDRColors
                ? BlendColorUnclamped(Color.blue * 2f, Color.red * 3f, t)
                : BlendColorUnclamped(Color.blue, Color.red, t);
            Gizmos.color = color;

            // Napravlenie i dlina strelki
            Vector3 direction = DominantAxisOrDefault(flow, Vector3.forward);
            float length = arrowLength * math.lerp(0.1f, 1f, t); // Minimalnaya dlina dlya vidimosti

            switch (arrowStyle)
            {
                case ArrowStyle.Arrows:
                    DrawArrow(position, direction, length);
                    break;
                case ArrowStyle.Lines:
                    Gizmos.DrawLine(position, position + direction * length);
                    break;
                case ArrowStyle.Cones:
                    DrawCone(position, direction, length);
                    break;
                case ArrowStyle.Dots:
                    DrawDot(position, magnitude);
                    break;
            }

            // Particle effects dlya silnyh techeniy
            if (useParticleEffects && particlePrefab != null && magnitude > maxForceScale * 0.7f)
            {
                SpawnParticleEffect(position, flow, magnitude);
            }

            // Leybly sily (optsionalno)
            if (showForceLabels && magnitude >= minFlowStrength)
            {
                DrawForceLabel(position + direction * length * 0.5f, magnitude);
            }
        }

        /// <summary>Risuet klassicheskuyu strelku s nakonechnikom</summary>
        private void DrawArrow(Vector3 position, Vector3 direction, float length)
        {
            // Stvol strelki
            Gizmos.DrawLine(position, position + direction * length);

            // Nakonechnik strelki
            Vector3 arrowTip = position + direction * length;
            Vector3 rightUnit = DominantAxisOrDefault(Vector3.Cross(direction, Vector3.up), Vector3.zero);
            if (rightUnit.sqrMagnitude <= 0.0001f)
                rightUnit = DominantAxisOrDefault(Vector3.Cross(direction, Vector3.forward), Vector3.right);
            Vector3 right = rightUnit * arrowThickness;

            Vector3 left = -right;
            Vector3 back = -direction * arrowThickness * 2f;

            Gizmos.DrawLine(arrowTip, arrowTip + back + right);
            Gizmos.DrawLine(arrowTip, arrowTip + back + left);
            Gizmos.DrawLine(arrowTip + back + right, arrowTip + back + left);
        }

        /// <summary>Risuet konus (bolee zametnyy)</summary>
        private void DrawCone(Vector3 position, Vector3 direction, float length)
        {
            Vector3 tip = position + direction * length;
            float radius = arrowThickness * 2f;

            // Prostoy konus cherez Gizmos.DrawLine (Unity ne imeet Gizmos.DrawCone)
            Vector3 up = DominantAxisOrDefault(Vector3.Cross(direction, Vector3.right), Vector3.zero);
            if (up.sqrMagnitude <= 0.0001f)
                up = DominantAxisOrDefault(Vector3.Cross(direction, Vector3.forward), Vector3.up);

            Vector3 right = DominantAxisOrDefault(Vector3.Cross(direction, up), Vector3.right);

            // Osnovanie konusa
            Vector3 baseCenter = position + direction * length * 0.7f;
            Gizmos.DrawLine(tip, baseCenter + up * radius);
            Gizmos.DrawLine(tip, baseCenter - up * radius);
            Gizmos.DrawLine(tip, baseCenter + right * radius);
            Gizmos.DrawLine(tip, baseCenter - right * radius);
        }

        /// <summary>Risuet tsvetnuyu tochku</summary>
        private void DrawDot(Vector3 position, float magnitude)
        {
            float size = math.lerp(0.05f, 0.2f, math.saturate(magnitude / math.max(maxForceScale, 0.0001f)));
            Gizmos.DrawSphere(position, size);
        }

        /// <summary>Risuet leybl s siloy techeniya</summary>
        private void DrawForceLabel(Vector3 position, float magnitude)
        {
#if UNITY_EDITOR
            UnityEditor.Handles.color = Color.white;
            _forceLabelBuilder.Clear();
            _forceLabelBuilder.Append(magnitude.ToString("F2", CultureInfo.InvariantCulture));
            _forceLabelBuilder.Append(" m/s");

            UnityEditor.Handles.Label(
                position,
                _forceLabelBuilder.ToString(),
                _cachedLabelStyle);
#endif
        }

        /// <summary>Sozdaet particle effect dlya silnyh techeniy</summary>
        private void SpawnParticleEffect(Vector3 position, Vector3 flow, float magnitude)
        {
#if UNITY_EDITOR
            if (particlePrefab == null)
                return;

            if (_particlePool == null)
            {
                _particlePool = new ParticlePool(() =>
                {
                    GameObject go = Instantiate(particlePrefab);
                    go.hideFlags = HideFlags.HideAndDontSave;
                    go.TryGetComponent(out ParticleSystem ps);
                    if (ps == null)
                    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        Debug.LogError("[FlowFieldVisualizer] Particle prefab must contain ParticleSystem", this);
#endif
                        DestroyImmediate(go);
                        return null;
                    }
                    return ps;
                }, ps => ps.gameObject.SetActive(true), ps => ps.gameObject.SetActive(false));
            }

            ParticleSystem ps = _particlePool.Get();
            if (ps == null)
                return;

            ps.transform.position = position;
            ps.transform.rotation = Quaternion.LookRotation(DominantAxisOrDefault(flow, Vector3.forward));

            var main = ps.main;
            float lifetime = Mathf.Max(0.1f, 1f + magnitude * 0.5f);
            main.startSpeed = magnitude * 2f;
            main.startLifetime = lifetime;

            _activeParticles.Add((ps, Time.realtimeSinceStartup + lifetime));
#endif
        }

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void OnEnable()
        {
            // Podpisyvaemsya na izmeneniya nastroek techeniy
            _subscribedFluidEngine = GlobalRegistry.Fluid;
            if (_subscribedFluidEngine != null)
            {
                _subscribedFluidEngine.OnCurrentSettingsChangedEvent += OnCurrentSettingsChanged;
            }
        }

        private void OnDisable()
        {
            // Otpisyvaemsya ot sobytiy
            if (_subscribedFluidEngine != null)
            {
                _subscribedFluidEngine.OnCurrentSettingsChangedEvent -= OnCurrentSettingsChanged;
                _subscribedFluidEngine = null;
            }

            // Polnostyu osvobozhdaem preview-resursy, chtoby ne ostavlyat hidden editor objects.
            DisposeParticlePreviewPool();
        }

        /// <summary>Obrabotchik izmeneniya nastroek techeniy v HectonFluidEngine.</summary>
        private void OnCurrentSettingsChanged()
        {
            if (showGlobalCurrent)
            {
                _needsRecalculation = true;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  EDITOR INTEGRATION
        // ══════════════════════════════════════════════════════════

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Primenyaem profil, esli on izmenilsya
            if (profile != null)
                profile.ApplyTo(this);

            // Validatsiya i korrektsiya nastroek
            ValidateSettings();

            _needsRecalculation = true;
        }

        private void OnDrawGizmosSelected()
        {
            DrawFlowField();
        }
#endif
    }
}
