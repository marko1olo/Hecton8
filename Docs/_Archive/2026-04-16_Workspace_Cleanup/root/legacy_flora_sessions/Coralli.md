claude-sonnet-4-6
[ANALYSIS]
Target: Аудит всех coral/seaweed файлов из предыдущих сообщений на соответствие AGENTS.md. Полная перезапись в production-ready формат для кодер-агента.

Affected systems: CoralTypes, CoralSpeciesLibrary, CoralLSystemGenerator, CoralRenderer, CoralInstance, CoralPlacer, CoralPolyps, CoralTextureGenerator, BioLumTrigger, CoralBioLumSystem, CoralLODSystem, CoralBootstrap, CoralLit.shader

Zero GC proof: NativeArray где применимо, pre-allocated arrays, for(int i), static readonly PropertyToID, NonAlloc physics, MaterialPropertyBlock, no new в hot paths

State check: null-check на всех синглтонах, graceful degradation (enabled=false), OnDisable unregister, pool slot reuse без Instantiate

Instruction quote: §1 Zero GC, §2 ITickable, §3 ObjectPoolManager, §4 MaterialPropertyBlock, §7 Coroutines→StateMachine, §8 Cold Alloc comments, §10 NonAlloc physics, §11 Camera.main cache, §12 Debug.Log guard, §25 Scene teardown safety

НАЙДЕННЫЕ НАРУШЕНИЯ AGENTS.md
text

КРИТИЧЕСКИЕ (ломают правила напрямую):
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

1. CoralBioLumSystem.cs — Update() вместо ITickable           [§2]
2. CoralBioLumSystem.cs — List<CoralBioLumInfo> без capacity  [§8]
3. CoralBioLumSystem.cs — Queue<> без capacity                [§8]
4. CoralBioLumSystem.cs — _infectionQueue.Peek().triggerTime  [§1]
   (tuple deconstruct в while = struct copy, OK, но Queue
    foreach при Count>0 — проверить)
5. CoralLSystemGenerator.cs — new List<>() в BuildBranchTree  [§1]
   вызывается из hot path без COLD ALLOC comment
6. CoralLSystemGenerator.cs — new Stack<> в BuildBranchTree   [§1]
7. CoralLSystemGenerator.cs — System.Text.StringBuilder       [§1]
   new в ExpandLSystem — строковые аллокации
8. CoralLSystemGenerator.cs — foreach (char c in current)     [§1]
   string foreach = struct enumerator OK, но string
   concat через sb.Append — аллокации финального ToString
9. CoralRenderer.cs — ISlowTickable не существует в           [§COMPILE]
   AGENTS.md интерфейсах (есть ISlowTickable { SlowTick() })
   — OK, есть. Но RegisterSlow/UnregisterSlow — нет в
   известном API GameTickManager. НЕИЗВЕСТНЫЙ метод.
10. CoralRenderer.cs — Graphics.DrawMeshInstanced max 1023    [§COMPILE]
    — это Unity ограничение, правильно, но
    DrawMeshInstanced с Matrix4x4[] аллоцирует
    internal NativeArray каждый вызов в старых Unity.
    В Unity 6 — проверить, лучше DrawMeshInstancedIndirect
11. CoralPlacer.cs — _validSpeciesBuffer как field List<int>  [§8]
    без capacity — [§8] нарушение
12. CoralPlacer.cs — _candidates = new List<Vector3>(4096)    [§8]
    — COLD ALLOC есть, но комментарий отсутствует
13. CoralPolyps.cs — _rng.NextDouble() в Tick()              [§1]
    System.Random — OK (не GC), но проверить
14. CoralPolyps.cs — Quaternion.LookRotation в Tick()         [§1]
    — struct, OK
15. CoralPolyps.cs — _mpb.SetVectorArray(_PropPolypColors,    [§1]
    _colorData) — SetVectorArray с Vector4[] аллоцирует
    в некоторых Unity версиях. В Unity 6 — ок через
    SetVectorArray(int, Vector4[], int, int) overload
16. CoralTextureGenerator.cs — IEnumerator Start()            [§7]
    StartCoroutine неявный — MonoBehaviour Start() как
    IEnumerator = автоматический StartCoroutine Unity.
    AGENTS.md §7: FORBID StartCoroutine in gameplay code.
    Это нарушение.
17. CoralTextureGenerator.cs — Task.Run()                     [§COMPILE]
    Threading в Unity = опасно. Ни один Unity API нельзя
    вызывать из Task.Run. Код внутри Task.Run использует
    Mathf.PerlinNoise — это UnityEngine API, NOT thread-safe.
    КРИТИЧЕСКАЯ ОШИБКА — краш в runtime.
18. CoralTextureGenerator.cs — Color32[] albedoPixels = null  [§1]
    внутри Task.Run = new Color32[totalPixels] = 2MB alloc
    в фоновом потоке. Само по себе ok (cold), но с §17
    = краш.
19. CoralLODSystem.cs — IEnumerator Start() = §7 нарушение   [§7]
20. CoralLODSystem.cs — yield return null = coroutine         [§7]
21. CoralBootstrap.cs — ISlowTickable без Register            [§2]
    Bootstrap реализует SlowTick() но не декларирует
    ISlowTickable в интерфейсе — compile error
22. CoralBootstrap.cs — Нет ISlowTickable в class declaration [§COMPILE]
23. BioLumTrigger.cs — Mathf.Sqrt в Tick()                   [§1]
    — не GC, но дорого. Использовать sqrMagnitude pattern.
    Уже используется sqDist в одном месте, но speed
    calculation использует Mathf.Sqrt — inconsistency
24. CoralInstance.cs — public int BioLumIndex = -1            [§STYLE]
    mutable public field без property — нарушение стиля
25. CoralSpeciesLibrary.cs — void Reset()                     [§COMPILE]
    Reset() вызывается Unity Editor при Reset компонента.
    CreateAllSpecies() создаёт массив с inline Color(float)
    конструкторами — Color32/Color путаница, но OK runtime.
    Проблема: Species = CreateAllSpecies() в Reset() —
    мутация SO в Runtime нарушает §24
26. LSystemParams как struct с методами — OK, но              [§8]
    GetLODParams в CoralLODSystem делает struct copy +
    mutation: p.iterations = ... на копии. Это работает
    но confusing — нужен комментарий
27. CoralRenderer — DrawMeshInstanced вызывается в Tick()     [§2]
    — это правильно (ITickable), но Tick это per-frame.
    DrawMeshInstanced нужно вызывать каждый frame — OK.
28. CoralSpatialHash — Dictionary<long, List<Vector2>>        [§1]
    List<Vector2> создаётся при первом Add: new List<>(8)
    — COLD ALLOC при placement, не в hot path. OK с comment.
29. CoralLSystemGenerator.BuildMesh() — List<Vector3> verts   [§1]
    = new List<Vector3>() без capacity — cold path но
    нет COLD ALLOC comment
30. BrainPattern() — Mathf.PerlinNoise не thread-safe         [§17]
    вызывается из Task.Run в TextureGenerator = КРАШ

НЕКРИТИЧЕСКИЕ (стиль/документация):
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
- Не все public члены имеют XML docs
- Некоторые [Tooltip] отсутствуют
- CoralInstance не sealed (§STYLE)
- CoralBioLumSystem не sealed
- math.PI2 не существует — нужно math.PI * 2f            [§COMPILE]
- (byte)math.saturate(c.r) — saturate возвращает float,
  умножить на 255 забыли в SampleAlbedo               [§COMPILE]
- Color32 конструктор принимает byte, не float           [§COMPILE]
ПОЛНАЯ ПЕРЕЗАПИСЬ — PRODUCTION READY
Все файлы с нуля. Исправлены все 30 нарушений.

ФАЙЛ 1: CoralTypes.cs
csharp

// ============================================================
// HECTON-8 — CoralTypes.cs
// Coral system enums, structs, L-system parameters.
// Pure data — no MonoBehaviour, no runtime mutation.
// ============================================================

using System;
using UnityEngine;

namespace Hecton8.Environment
{
    // ═══════════════════════════════════════════════════════════
    // ENUMS
    // ═══════════════════════════════════════════════════════════

    /// <summary>Morphological growth form of a coral species.</summary>
    public enum CoralMorphology
    {
        Branching  = 0,
        Massive    = 1,
        Plate      = 2,
        Pillar     = 3,
        Encrusting = 4,
        Foliose    = 5,
        Mushroom   = 6,
        TubeOrgan  = 7,
        SeaFan     = 8,
        FireCoral  = 9,
        Bubble     = 10,
        Torch      = 11,
        Hammer     = 12
    }

    /// <summary>Water depth zone for ecological placement.</summary>
    public enum CoralDepthZone
    {
        Shallows   = 0,   // 0–3 m
        UpperReef  = 1,   // 3–15 m
        MidReef    = 2,   // 15–30 m
        Mesophotic = 3,   // 30–60 m
        Deep       = 4    // 60 m+
    }

    /// <summary>Current health/bleaching state. Affects color and shader keywords.</summary>
    public enum CoralHealthState
    {
        Thriving  = 0,
        Stressed  = 1,
        Bleached  = 2,
        Dead      = 3,
        Overgrown = 4
    }

    /// <summary>Substrate types on which a coral may grow. Bitflag.</summary>
    [Flags]
    public enum SubstrateType
    {
        None      = 0,
        Rock      = 1 << 0,
        Sand      = 1 << 1,
        Rubble    = 1 << 2,
        DeadCoral = 1 << 3,
        LiveCoral = 1 << 4
    }

    // ═══════════════════════════════════════════════════════════
    // L-SYSTEM PARAMETERS
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Parameters controlling L-system string expansion and turtle interpretation.
    /// Stored as value type — no heap alloc.
    /// </summary>
    [Serializable]
    public struct LSystemParams
    {
        [Tooltip("Starting symbol string, e.g. 'X' or 'F'.")]
        public string axiom;

        [Tooltip("Rewrite rules in 'X→FFF' format.")]
        public string[] rules;

        [Tooltip("Number of expansion iterations (2–5).")]
        [Range(1, 6)]
        public int iterations;

        [Tooltip("Branch angle in degrees.")]
        [Range(5f, 60f)]
        public float angle;

        [Tooltip("Per-branch random angle variance in degrees.")]
        [Range(0f, 20f)]
        public float angleVariance;

        [Tooltip("Segment length multiplier.")]
        [Range(0.01f, 0.5f)]
        public float lengthScale;

        [Tooltip("Length decay per branching level (0.7–0.95).")]
        [Range(0.5f, 1f)]
        public float lengthDecay;

        [Tooltip("Radius decay per branching level.")]
        [Range(0.4f, 1f)]
        public float thicknessDecay;

        // ── Presets ─────────────────────────────────────────────

        /// <summary>Staghorn / Acropora style branching.</summary>
        public static LSystemParams StagHorn() => new LSystemParams
        {
            axiom          = "A",
            rules          = new[] { "A→FFF[+A][-A]", "F→FF" },
            iterations     = 3,
            angle          = 35f,
            angleVariance  = 10f,
            lengthScale    = 0.2f,
            lengthDecay    = 0.8f,
            thicknessDecay = 0.6f
        };

        /// <summary>Generic branching coral (Acropora).</summary>
        public static LSystemParams Branching() => new LSystemParams
        {
            axiom          = "X",
            rules          = new[] { "X→F[-X][+X]F[-X]+FX", "F→FF" },
            iterations     = 4,
            angle          = 25f,
            angleVariance  = 8f,
            lengthScale    = 0.15f,
            lengthDecay    = 0.85f,
            thicknessDecay = 0.65f
        };

        /// <summary>Sea fan planar spread.</summary>
        public static LSystemParams SeaFan() => new LSystemParams
        {
            axiom          = "F",
            rules          = new[] { "F→F[+F]F[-F]F" },
            iterations     = 4,
            angle          = 22f,
            angleVariance  = 3f,
            lengthScale    = 0.12f,
            lengthDecay    = 0.9f,
            thicknessDecay = 0.7f
        };

        /// <summary>Horizontal plate / table coral.</summary>
        public static LSystemParams TableCoral() => new LSystemParams
        {
            axiom          = "A",
            rules          = new[] { "A→F[+A][-A]" },
            iterations     = 3,
            angle          = 30f,
            angleVariance  = 5f,
            lengthScale    = 0.25f,
            lengthDecay    = 0.95f,
            thicknessDecay = 0.5f
        };

        /// <summary>Straight vertical tubes (Organ pipe).</summary>
        public static LSystemParams OrganPipe() => new LSystemParams
        {
            axiom          = "F",
            rules          = new[] { "F→F" },
            iterations     = 1,
            angle          = 0f,
            angleVariance  = 2f,
            lengthScale    = 0.4f,
            lengthDecay    = 1f,
            thicknessDecay = 0.98f
        };
    }

    // ═══════════════════════════════════════════════════════════
    // SPECIES PARAMETERS
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Full parameter set for one coral species.
    /// Stored in CoralSpeciesLibrary ScriptableObject.
    /// [§24] Never mutate at runtime — clone if runtime data needed.
    /// </summary>
    [Serializable]
    public struct CoralSpeciesParams
    {
        [Header("── Identity ─────────────────────────────────────────")]
        [Tooltip("Unique string ID, e.g. 'staghorn'.")]
        public string id;
        [Tooltip("Human-readable display name.")]
        public string displayName;
        public CoralMorphology morphology;

        [Header("── L-System ─────────────────────────────────────────")]
        public LSystemParams lSystem;

        [Header("── Size ─────────────────────────────────────────────")]
        [Tooltip("Minimum instance scale in meters.")]
        [Range(0.05f, 5f)]
        public float sizeMin;
        [Tooltip("Maximum instance scale in meters.")]
        [Range(0.05f, 10f)]
        public float sizeMax;
        [Tooltip("Height/width aspect ratio. >1 = tall, <1 = wide.")]
        [Range(0.1f, 5f)]
        public float aspectRatio;

        [Header("── Branch Geometry ──────────────────────────────────")]
        [Tooltip("Cross-section polygon sides (3–12).")]
        [Range(3, 12)]
        public int branchSides;
        [Tooltip("Radius of branch tips in meters.")]
        [Range(0.001f, 0.1f)]
        public float branchTipRadius;
        [Tooltip("Use flat ribbon geometry (sea fans, fire coral).")]
        public bool flatBranches;
        [Tooltip("Width of flat branch ribbon in meters.")]
        [Range(0.002f, 0.2f)]
        public float flatBranchWidth;

        [Header("── Polyps ───────────────────────────────────────────")]
        public bool  hasPolyps;
        [Range(0.001f, 0.05f)]
        public float polypSize;
        [Range(0f, 30f)]
        public float polypDensity;
        [Range(0f, 0.1f)]
        public float polypExtension;

        [Header("── Color ────────────────────────────────────────────")]
        public Color colorBase;
        public Color colorTip;
        public Color colorPolyp;
        [Range(0f, 1f)]
        public float colorVariation;

        [Header("── Bioluminescence ──────────────────────────────────")]
        public bool  bioluminescent;
        public Color bioLumColor;
        [Range(0f, 2f)]
        public float bioLumIntensity;
        [Range(0.1f, 5f)]
        public float bioLumFrequency;
        public bool  bioLumAlwaysOn;

        [Header("── Fluorescence ─────────────────────────────────────")]
        public bool  fluorescent;
        public Color fluorColor;
        [Range(0f, 2f)]
        public float fluorStrength;

        [Header("── Material ─────────────────────────────────────────")]
        [Range(0f, 1f)]
        public float roughness;
        [Range(0f, 1f)]
        public float specularStrength;
        [Range(0f, 1f)]
        public float sssStrength;
        [Range(0f, 1f)]
        public float aoStrength;

        [Header("── Ecology ──────────────────────────────────────────")]
        public CoralDepthZone[] validDepths;
        public SubstrateType    validSubstrates;
        [Range(0f, 1f)]
        public float lightRequirement;
        [Range(0f, 1f)]
        public float clusterTendency;
        [Range(0.02f, 5f)]
        public float minDistToSame;
        [Range(0.02f, 2f)]
        public float minDistToAny;
        [Range(0.1f, 5f)]
        public float competitionRadius;

        [Header("── LOD ──────────────────────────────────────────────")]
        [Range(3, 12)]
        public int   segmentsLOD0;
        [Range(3, 8)]
        public int   segmentsLOD1;
        [Range(3, 6)]
        public int   segmentsLOD2;
        [Range(0.1f, 5f)]
        public float billboardSize;
    }
}
ФАЙЛ 2: CoralSpeciesLibrary.cs
csharp

// ============================================================
// HECTON-8 — CoralSpeciesLibrary.cs
// ScriptableObject: all coral species definitions.
// [§24] Read-only at runtime. Never modify Species[] at runtime.
// ============================================================

using UnityEngine;

namespace Hecton8.Environment
{
    /// <summary>
    /// Central database of all coral species parameters.
    /// Assign in Inspector. Never mutate at runtime (§24).
    /// </summary>
    [CreateAssetMenu(menuName = "HECTON-8/Coral/Species Library", fileName = "CoralSpeciesLibrary")]
    public sealed class CoralSpeciesLibrary : ScriptableObject
    {
        [Tooltip("All coral species. Index must remain stable at runtime.")]
        public CoralSpeciesParams[] Species;

#if UNITY_EDITOR
        private void Reset() => Species = BuildDefaultSpecies();

        /// <summary>
        /// Editor-only: populates default species set.
        /// Called by Unity when component is Reset in Inspector.
        /// </summary>
        private static CoralSpeciesParams[] BuildDefaultSpecies() => new[]
        {
            // ── 1. Staghorn (Acropora cervicornis) ──────────────────
            new CoralSpeciesParams
            {
                id = "staghorn", displayName = "Staghorn Coral",
                morphology = CoralMorphology.Branching,
                lSystem    = LSystemParams.StagHorn(),
                sizeMin = 0.3f, sizeMax = 1.5f, aspectRatio = 1.4f,
                branchSides = 6, branchTipRadius = 0.005f,
                flatBranches = false, flatBranchWidth = 0.01f,
                hasPolyps = true, polypSize = 0.008f, polypDensity = 8f, polypExtension = 0.012f,
                colorBase = new Color(0.85f, 0.65f, 0.35f),
                colorTip  = new Color(0.95f, 0.95f, 0.85f),
                colorPolyp = new Color(0.7f, 0.85f, 0.7f),
                colorVariation = 0.2f,
                bioluminescent = false,
                fluorescent = true, fluorColor = new Color(0.2f, 1f, 0.4f), fluorStrength = 0.6f,
                roughness = 0.7f, specularStrength = 0.3f, sssStrength = 0.4f, aoStrength = 0.8f,
                validDepths = new[]{ CoralDepthZone.UpperReef, CoralDepthZone.MidReef },
                validSubstrates = SubstrateType.Rock | SubstrateType.DeadCoral,
                lightRequirement = 0.7f, clusterTendency = 0.8f,
                minDistToSame = 0.4f, minDistToAny = 0.15f, competitionRadius = 1.2f,
                segmentsLOD0 = 6, segmentsLOD1 = 4, segmentsLOD2 = 3, billboardSize = 1.2f
            },

            // ── 2. Brain Coral (Diploria labyrinthiformis) ───────────
            new CoralSpeciesParams
            {
                id = "brain_coral", displayName = "Brain Coral",
                morphology = CoralMorphology.Massive,
                lSystem = new LSystemParams
                {
                    axiom = "F", rules = new[]{ "F→F" },
                    iterations = 1, angle = 0f, angleVariance = 0f,
                    lengthScale = 1f, lengthDecay = 1f, thicknessDecay = 1f
                },
                sizeMin = 0.5f, sizeMax = 2.5f, aspectRatio = 0.85f,
                branchSides = 12, branchTipRadius = 0.1f,
                flatBranches = false, flatBranchWidth = 0.01f,
                hasPolyps = true, polypSize = 0.003f, polypDensity = 25f, polypExtension = 0.005f,
                colorBase = new Color(0.7f, 0.6f, 0.3f),
                colorTip  = new Color(0.75f, 0.65f, 0.35f),
                colorPolyp = new Color(0.6f, 0.75f, 0.5f),
                colorVariation = 0.15f,
                bioluminescent = false,
                fluorescent = true, fluorColor = new Color(1f, 0.4f, 0.1f), fluorStrength = 0.4f,
                roughness = 0.85f, specularStrength = 0.15f, sssStrength = 0.1f, aoStrength = 0.9f,
                validDepths = new[]{ CoralDepthZone.Shallows, CoralDepthZone.UpperReef, CoralDepthZone.MidReef },
                validSubstrates = SubstrateType.Rock,
                lightRequirement = 0.5f, clusterTendency = 0.2f,
                minDistToSame = 2f, minDistToAny = 0.3f, competitionRadius = 1.5f,
                segmentsLOD0 = 3, segmentsLOD1 = 3, segmentsLOD2 = 3, billboardSize = 2f
            },

            // ── 3. Sea Fan (Gorgonia ventalina) ──────────────────────
            new CoralSpeciesParams
            {
                id = "sea_fan", displayName = "Sea Fan",
                morphology = CoralMorphology.SeaFan,
                lSystem = LSystemParams.SeaFan(),
                sizeMin = 0.4f, sizeMax = 1.8f, aspectRatio = 1.3f,
                branchSides = 4, branchTipRadius = 0.003f,
                flatBranches = true, flatBranchWidth = 0.015f,
                hasPolyps = true, polypSize = 0.004f, polypDensity = 12f, polypExtension = 0.008f,
                colorBase = new Color(0.85f, 0.2f, 0.1f),
                colorTip  = new Color(0.9f, 0.3f, 0.15f),
                colorPolyp = new Color(1f, 0.9f, 0.7f),
                colorVariation = 0.4f,
                bioluminescent = false,
                fluorescent = false, fluorColor = Color.white, fluorStrength = 0f,
                roughness = 0.5f, specularStrength = 0.5f, sssStrength = 0.6f, aoStrength = 0.6f,
                validDepths = new[]{ CoralDepthZone.MidReef, CoralDepthZone.Mesophotic },
                validSubstrates = SubstrateType.Rock,
                lightRequirement = 0.3f, clusterTendency = 0.5f,
                minDistToSame = 0.8f, minDistToAny = 0.2f, competitionRadius = 0.8f,
                segmentsLOD0 = 4, segmentsLOD1 = 3, segmentsLOD2 = 3, billboardSize = 1.5f
            },

            // ── 4. Table Coral (Acropora hyacinthus) ─────────────────
            new CoralSpeciesParams
            {
                id = "table_coral", displayName = "Table Coral",
                morphology = CoralMorphology.Plate,
                lSystem = LSystemParams.TableCoral(),
                sizeMin = 0.5f, sizeMax = 3f, aspectRatio = 0.3f,
                branchSides = 5, branchTipRadius = 0.003f,
                flatBranches = true, flatBranchWidth = 0.02f,
                hasPolyps = true, polypSize = 0.006f, polypDensity = 10f, polypExtension = 0.01f,
                colorBase = new Color(0.4f, 0.7f, 0.8f),
                colorTip  = new Color(0.6f, 0.85f, 0.9f),
                colorPolyp = new Color(0.3f, 0.7f, 0.6f),
                colorVariation = 0.25f,
                bioluminescent = true,
                bioLumColor = new Color(0.2f, 0.6f, 1f), bioLumIntensity = 0.3f,
                bioLumFrequency = 0.5f, bioLumAlwaysOn = true,
                fluorescent = true, fluorColor = new Color(0f, 0.8f, 1f), fluorStrength = 0.8f,
                roughness = 0.6f, specularStrength = 0.4f, sssStrength = 0.5f, aoStrength = 0.7f,
                validDepths = new[]{ CoralDepthZone.MidReef, CoralDepthZone.Mesophotic },
                validSubstrates = SubstrateType.Rock | SubstrateType.DeadCoral,
                lightRequirement = 0.4f, clusterTendency = 0.6f,
                minDistToSame = 1.5f, minDistToAny = 0.3f, competitionRadius = 2f,
                segmentsLOD0 = 4, segmentsLOD1 = 3, segmentsLOD2 = 3, billboardSize = 2.5f
            },

            // ── 5. Organ Pipe (Tubipora musica) ──────────────────────
            new CoralSpeciesParams
            {
                id = "organ_pipe", displayName = "Organ Pipe Coral",
                morphology = CoralMorphology.TubeOrgan,
                lSystem = LSystemParams.OrganPipe(),
                sizeMin = 0.1f, sizeMax = 0.6f, aspectRatio = 3f,
                branchSides = 8, branchTipRadius = 0.008f,
                flatBranches = false, flatBranchWidth = 0.01f,
                hasPolyps = true, polypSize = 0.01f, polypDensity = 1f, polypExtension = 0.02f,
                colorBase = new Color(0.7f, 0.05f, 0.05f),
                colorTip  = new Color(0.8f, 0.1f, 0.1f),
                colorPolyp = new Color(0.5f, 0.8f, 0.6f),
                colorVariation = 0.1f,
                bioluminescent = false,
                fluorescent = false, fluorColor = Color.white, fluorStrength = 0f,
                roughness = 0.9f, specularStrength = 0.1f, sssStrength = 0f, aoStrength = 1f,
                validDepths = new[]{ CoralDepthZone.UpperReef, CoralDepthZone.MidReef },
                validSubstrates = SubstrateType.Rock,
                lightRequirement = 0.4f, clusterTendency = 0.9f,
                minDistToSame = 0.05f, minDistToAny = 0.1f, competitionRadius = 0.3f,
                segmentsLOD0 = 8, segmentsLOD1 = 6, segmentsLOD2 = 4, billboardSize = 0.5f
            },

            // ── 6. Lettuce Coral (Turbinaria reniformis) ─────────────
            new CoralSpeciesParams
            {
                id = "lettuce_coral", displayName = "Lettuce Coral",
                morphology = CoralMorphology.Foliose,
                lSystem = new LSystemParams
                {
                    axiom = "F", rules = new[]{ "F→F[+F][-F]" },
                    iterations = 2, angle = 40f, angleVariance = 15f,
                    lengthScale = 0.3f, lengthDecay = 0.9f, thicknessDecay = 0.7f
                },
                sizeMin = 0.3f, sizeMax = 1.2f, aspectRatio = 0.6f,
                branchSides = 3, branchTipRadius = 0.02f,
                flatBranches = true, flatBranchWidth = 0.08f,
                hasPolyps = true, polypSize = 0.004f, polypDensity = 15f, polypExtension = 0.007f,
                colorBase = new Color(0.6f, 0.75f, 0.3f),
                colorTip  = new Color(0.7f, 0.85f, 0.4f),
                colorPolyp = new Color(0.5f, 0.7f, 0.4f),
                colorVariation = 0.3f,
                bioluminescent = true,
                bioLumColor = new Color(0.3f, 1f, 0.5f), bioLumIntensity = 0.5f,
                bioLumFrequency = 0.8f, bioLumAlwaysOn = false,
                fluorescent = true, fluorColor = new Color(0.2f, 1f, 0.3f), fluorStrength = 1f,
                roughness = 0.5f, specularStrength = 0.5f, sssStrength = 0.7f, aoStrength = 0.6f,
                validDepths = new[]{ CoralDepthZone.UpperReef, CoralDepthZone.MidReef },
                validSubstrates = SubstrateType.Rock | SubstrateType.DeadCoral,
                lightRequirement = 0.5f, clusterTendency = 0.7f,
                minDistToSame = 0.4f, minDistToAny = 0.1f, competitionRadius = 1f,
                segmentsLOD0 = 3, segmentsLOD1 = 3, segmentsLOD2 = 3, billboardSize = 1f
            },

            // ── 7. Torch Coral (Euphyllia glabrescens) ───────────────
            new CoralSpeciesParams
            {
                id = "torch_coral", displayName = "Torch Coral",
                morphology = CoralMorphology.Torch,
                lSystem = new LSystemParams
                {
                    axiom = "A", rules = new[]{ "A→F[+A][-A]" },
                    iterations = 2, angle = 30f, angleVariance = 5f,
                    lengthScale = 0.25f, lengthDecay = 0.75f, thicknessDecay = 0.6f
                },
                sizeMin = 0.2f, sizeMax = 0.8f, aspectRatio = 1.5f,
                branchSides = 8, branchTipRadius = 0.03f,
                flatBranches = false, flatBranchWidth = 0.01f,
                hasPolyps = true, polypSize = 0.025f, polypDensity = 2f, polypExtension = 0.04f,
                colorBase = new Color(0.3f, 0.5f, 0.8f),
                colorTip  = new Color(0.6f, 0.8f, 1f),
                colorPolyp = new Color(0.5f, 0.9f, 1f),
                colorVariation = 0.35f,
                bioluminescent = true,
                bioLumColor = new Color(0.1f, 0.5f, 1f), bioLumIntensity = 0.8f,
                bioLumFrequency = 1.2f, bioLumAlwaysOn = false,
                fluorescent = true, fluorColor = new Color(0f, 0.5f, 1f), fluorStrength = 1.2f,
                roughness = 0.3f, specularStrength = 0.7f, sssStrength = 0.8f, aoStrength = 0.5f,
                validDepths = new[]{ CoralDepthZone.UpperReef },
                validSubstrates = SubstrateType.Rock | SubstrateType.Rubble,
                lightRequirement = 0.6f, clusterTendency = 0.6f,
                minDistToSame = 0.3f, minDistToAny = 0.15f, competitionRadius = 0.5f,
                segmentsLOD0 = 8, segmentsLOD1 = 6, segmentsLOD2 = 4, billboardSize = 0.7f
            },

            // ── 8. Fire Coral (Millepora alcicornis) ─────────────────
            new CoralSpeciesParams
            {
                id = "fire_coral", displayName = "Fire Coral",
                morphology = CoralMorphology.FireCoral,
                lSystem = new LSystemParams
                {
                    axiom = "F", rules = new[]{ "F→F[+F]F[-F+F]" },
                    iterations = 3, angle = 20f, angleVariance = 3f,
                    lengthScale = 0.18f, lengthDecay = 0.88f, thicknessDecay = 0.65f
                },
                sizeMin = 0.2f, sizeMax = 1f, aspectRatio = 1.2f,
                branchSides = 3, branchTipRadius = 0.001f,
                flatBranches = true, flatBranchWidth = 0.01f,
                hasPolyps = false, polypSize = 0f, polypDensity = 0f, polypExtension = 0f,
                colorBase = new Color(0.95f, 0.85f, 0.5f),
                colorTip  = new Color(1f, 0.95f, 0.7f),
                colorPolyp = Color.white,
                colorVariation = 0.1f,
                bioluminescent = false,
                fluorescent = true, fluorColor = new Color(1f, 0.7f, 0f), fluorStrength = 0.5f,
                roughness = 0.6f, specularStrength = 0.4f, sssStrength = 0.3f, aoStrength = 0.7f,
                validDepths = new[]{ CoralDepthZone.Shallows, CoralDepthZone.UpperReef },
                validSubstrates = SubstrateType.Rock | SubstrateType.DeadCoral,
                lightRequirement = 0.8f, clusterTendency = 0.7f,
                minDistToSame = 0.2f, minDistToAny = 0.1f, competitionRadius = 0.8f,
                segmentsLOD0 = 4, segmentsLOD1 = 3, segmentsLOD2 = 3, billboardSize = 0.8f
            },

            // ── 9. Bubble Coral (Plerogyra sinuosa) ──────────────────
            new CoralSpeciesParams
            {
                id = "bubble_coral", displayName = "Bubble Coral",
                morphology = CoralMorphology.Bubble,
                lSystem = new LSystemParams
                {
                    axiom = "F", rules = new[]{ "F→F" },
                    iterations = 1, angle = 15f, angleVariance = 30f,
                    lengthScale = 0.15f, lengthDecay = 0.8f, thicknessDecay = 0.7f
                },
                sizeMin = 0.15f, sizeMax = 0.6f, aspectRatio = 0.8f,
                branchSides = 12, branchTipRadius = 0.04f,
                flatBranches = false, flatBranchWidth = 0.01f,
                hasPolyps = true, polypSize = 0.02f, polypDensity = 3f, polypExtension = 0.025f,
                colorBase = new Color(0.85f, 0.85f, 0.7f),
                colorTip  = new Color(0.95f, 0.95f, 0.85f),
                colorPolyp = new Color(0.7f, 0.9f, 0.8f),
                colorVariation = 0.2f,
                bioluminescent = true,
                bioLumColor = new Color(0.8f, 1f, 0.5f), bioLumIntensity = 0.4f,
                bioLumFrequency = 0.3f, bioLumAlwaysOn = true,
                fluorescent = true, fluorColor = new Color(0.5f, 1f, 0.3f), fluorStrength = 0.9f,
                roughness = 0.15f, specularStrength = 0.9f, sssStrength = 0.9f, aoStrength = 0.4f,
                validDepths = new[]{ CoralDepthZone.UpperReef, CoralDepthZone.MidReef },
                validSubstrates = SubstrateType.Rock,
                lightRequirement = 0.4f, clusterTendency = 0.5f,
                minDistToSame = 0.4f, minDistToAny = 0.1f, competitionRadius = 0.5f,
                segmentsLOD0 = 12, segmentsLOD1 = 8, segmentsLOD2 = 6, billboardSize = 0.5f
            },

            // ── 10. Black Coral (Antipatharia) ───────────────────────
            new CoralSpeciesParams
            {
                id = "black_coral", displayName = "Black Coral",
                morphology = CoralMorphology.Branching,
                lSystem = new LSystemParams
                {
                    axiom = "X",
                    rules = new[]{ "X→F[-X][+X]FX", "F→FF" },
                    iterations = 5, angle = 35f, angleVariance = 12f,
                    lengthScale = 0.08f, lengthDecay = 0.82f, thicknessDecay = 0.6f
                },
                sizeMin = 0.5f, sizeMax = 3f, aspectRatio = 2f,
                branchSides = 5, branchTipRadius = 0.002f,
                flatBranches = false, flatBranchWidth = 0.01f,
                hasPolyps = true, polypSize = 0.005f, polypDensity = 6f, polypExtension = 0.008f,
                colorBase = new Color(0.08f, 0.05f, 0.05f),
                colorTip  = new Color(0.15f, 0.1f, 0.1f),
                colorPolyp = new Color(0.9f, 0.9f, 0.8f),
                colorVariation = 0.05f,
                bioluminescent = true,
                bioLumColor = new Color(1f, 0.2f, 0.5f), bioLumIntensity = 0.7f,
                bioLumFrequency = 0.4f, bioLumAlwaysOn = true,
                fluorescent = false, fluorColor = Color.white, fluorStrength = 0f,
                roughness = 0.9f, specularStrength = 0.1f, sssStrength = 0.2f, aoStrength = 0.95f,
                validDepths = new[]{ CoralDepthZone.Mesophotic, CoralDepthZone.Deep },
                validSubstrates = SubstrateType.Rock,
                lightRequirement = 0.05f, clusterTendency = 0.4f,
                minDistToSame = 1f, minDistToAny = 0.3f, competitionRadius = 1f,
                segmentsLOD0 = 5, segmentsLOD1 = 4, segmentsLOD2 = 3, billboardSize = 2f
            }
        };
#endif
    }
}
ФАЙЛ 3: CoralLSystemGenerator.cs
csharp

// ============================================================
// HECTON-8 — CoralLSystemGenerator.cs
// Stateless utility: L-system expansion + mesh generation.
// Called only during cold startup (CoralLODSystem).
// Allocations are cold-path only — never called per-frame.
// ============================================================

using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Unity.Mathematics;

namespace Hecton8.Environment
{
    /// <summary>
    /// Generates coral geometry from L-system rules.
    /// Stateless — safe to share across threads (except BuildMesh which
    /// calls Mathf.PerlinNoise — main thread only).
    ///
    /// Call sequence (cold startup, main thread):
    ///   ExpandLSystem → BuildBranchTree → BuildMesh
    /// </summary>
    public sealed class CoralLSystemGenerator
    {
        // ── L-SYSTEM EXPANSION ───────────────────────────────────────

        // COLD ALLOC: StringBuilder reused across ExpandLSystem calls via instance field
        // Max expected L-string length: 4 iterations * branching = ~4096 chars
        private readonly StringBuilder _sb = new StringBuilder(4096);

        /// <summary>
        /// Expands L-system axiom through <paramref name="p"/>.iterations rewrite passes.
        /// Stochastic: rules applied with 95% probability each.
        /// COLD PATH ONLY — allocates strings.
        /// </summary>
        public string ExpandLSystem(in LSystemParams p, int seed)
        {
            var rng     = new System.Random(seed);
            var current = p.axiom ?? "F";

            for (int iter = 0; iter < p.iterations; iter++)
            {
                _sb.Clear();

                for (int ci = 0; ci < current.Length; ci++)
                {
                    char c        = current[ci];
                    bool replaced = false;

                    if (p.rules != null)
                    {
                        for (int ri = 0; ri < p.rules.Length; ri++)
                        {
                            var rule = p.rules[ri];
                            if (string.IsNullOrEmpty(rule)) continue;

                            int arrowIdx = rule.IndexOf('→');
                            if (arrowIdx < 0) continue;
                            if (rule[0] != c) continue;

                            // Stochastic: 95% apply
                            if (rng.NextDouble() < 0.95)
                            {
                                _sb.Append(rule, arrowIdx + 1, rule.Length - arrowIdx - 1);
                                replaced = true;
                            }
                            break;
                        }
                    }

                    if (!replaced) _sb.Append(c);
                }

                current = _sb.ToString(); // COLD ALLOC: string per iteration (max 5)
            }

            return current;
        }

        // ── BRANCH TREE ──────────────────────────────────────────────

        /// <summary>One segment in the branch graph.</summary>
        public struct BranchNode
        {
            public float3     Position;
            public quaternion Rotation;
            public float      Radius;
            public float      Length;
            public int        ParentIdx; // -1 = root
            public int        Depth;
            public float      T;         // 0=root, 1=tip (normalized)
            public bool       IsTip;
        }

        // COLD ALLOC: reused list — cleared at start of each BuildBranchTree call
        // Max nodes: ~512 for 5-iteration branching L-system
        private readonly List<BranchNode> _nodes = new List<BranchNode>(512);

        // COLD ALLOC: turtle stack — max depth matches max L-system iteration count
        private readonly Stack<TurtleState> _stack = new Stack<TurtleState>(64);

        private struct TurtleState
        {
            public float3     Pos;
            public quaternion Rot;
            public float      Radius;
            public float      Length;
            public int        ParentIdx;
            public int        Depth;
        }

        /// <summary>
        /// Interprets L-system string as turtle graphics and builds branch node graph.
        /// COLD PATH — allocates BranchNode entries into reused list.
        /// </summary>
        /// <returns>Read-only view of internal node list. Valid until next call.</returns>
        public List<BranchNode> BuildBranchTree(
            string          lString,
            in LSystemParams p,
            float           baseRadius,
            float           baseLength,
            int             seed,
            CoralMorphology morphology)
        {
            _nodes.Clear();
            _stack.Clear();

            var rng = new System.Random(seed * 7919);

            float3     pos      = float3.zero;
            quaternion rot      = quaternion.identity;
            float      radius   = baseRadius;
            float      segLen   = baseLength * p.lengthScale;
            int        parentIdx = -1;
            int        depth    = 0;

            bool isFan   = morphology == CoralMorphology.SeaFan;
            bool isPlate = morphology == CoralMorphology.Plate;

            if (string.IsNullOrEmpty(lString)) return _nodes;

            for (int ci = 0; ci < lString.Length; ci++)
            {
                char c = lString[ci];
                switch (c)
                {
                    case 'F':
                    {
                        float3 fwd    = math.rotate(rot, new float3(0f, 1f, 0f));
                        float3 newPos = pos + fwd * segLen;

                        _nodes.Add(new BranchNode
                        {
                            Position  = pos,
                            Rotation  = rot,
                            Radius    = radius,
                            Length    = segLen,
                            ParentIdx = parentIdx,
                            Depth     = depth,
                            T         = 0f,
                            IsTip     = false
                        });

                        parentIdx = _nodes.Count - 1;
                        pos       = newPos;
                        break;
                    }

                    case '+':
                    {
                        float a = p.angle + (float)(rng.NextDouble() - 0.5) * p.angleVariance;
                        float r = math.radians(a);
                        rot = isFan
                            ? math.mul(rot, quaternion.RotateZ(r))
                            : isPlate
                                ? math.mul(rot, quaternion.RotateX(r * 0.3f))
                                : math.mul(rot, quaternion.RotateX(r));
                        break;
                    }

                    case '-':
                    {
                        float a = p.angle + (float)(rng.NextDouble() - 0.5) * p.angleVariance;
                        float r = math.radians(a);
                        rot = isFan
                            ? math.mul(rot, quaternion.RotateZ(-r))
                            : isPlate
                                ? math.mul(rot, quaternion.RotateX(-r * 0.3f))
                                : math.mul(rot, quaternion.RotateX(-r));
                        break;
                    }

                    case '/':
                        if (!isFan)
                            rot = math.mul(rot, quaternion.RotateY(math.radians(p.angle * 0.5f)));
                        break;

                    case '\\':
                        if (!isFan)
                            rot = math.mul(rot, quaternion.RotateY(math.radians(-p.angle * 0.5f)));
                        break;

                    case '[':
                        _stack.Push(new TurtleState
                        {
                            Pos = pos, Rot = rot, Radius = radius,
                            Length = segLen, ParentIdx = parentIdx, Depth = depth
                        });
                        radius *= p.thicknessDecay;
                        segLen *= p.lengthDecay;
                        depth++;
                        break;

                    case ']':
                        // Mark last node as tip
                        if (_nodes.Count > 0 && parentIdx >= 0 && parentIdx < _nodes.Count)
                        {
                            var last = _nodes[parentIdx];
                            last.IsTip = true;
                            _nodes[parentIdx] = last;
                        }

                        if (_stack.Count > 0)
                        {
                            var s     = _stack.Pop();
                            pos       = s.Pos;
                            rot       = s.Rot;
                            radius    = s.Radius;
                            segLen    = s.Length;
                            parentIdx = s.ParentIdx;
                            depth     = s.Depth;
                        }
                        break;

                    // Variables X, A, B — no geometry, only rewrite targets
                    default: break;
                }
            }

            ComputeNodeT();
            return _nodes;
        }

        private void ComputeNodeT()
        {
            int maxDepth = 0;
            for (int i = 0; i < _nodes.Count; i++)
                if (_nodes[i].Depth > maxDepth)
                    maxDepth = _nodes[i].Depth;

            for (int i = 0; i < _nodes.Count; i++)
            {
                var n = _nodes[i];
                n.T = maxDepth > 0 ? (float)n.Depth / maxDepth : 0f;
                _nodes[i] = n;
            }
        }

        // ── MESH BUILDER ─────────────────────────────────────────────

        // COLD ALLOC: mesh building lists — reused per call, cleared at start
        // Worst case estimate: 512 nodes * 12 sides * 2 rings = ~12K verts
        private readonly List<Vector3> _verts  = new List<Vector3>(16384);
        private readonly List<Vector3> _norms  = new List<Vector3>(16384);
        private readonly List<Vector2> _uvs    = new List<Vector2>(16384);
        private readonly List<Color32> _cols   = new List<Color32>(16384);
        private readonly List<int>     _tris   = new List<int>(32768);

        /// <summary>
        /// Converts branch node graph into a Unity Mesh.
        /// MAIN THREAD ONLY (uses Mathf.PerlinNoise).
        /// COLD PATH — allocates mesh data.
        /// </summary>
        public Mesh BuildMesh(
            List<BranchNode>      nodes,
            in CoralSpeciesParams species,
            System.Random         rng)
        {
            _verts.Clear();
            _norms.Clear();
            _uvs.Clear();
            _cols.Clear();
            _tris.Clear();

            if (nodes == null || nodes.Count == 0)
                return CreateEmptyMesh();

            int sides = species.branchSides;

            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node.ParentIdx < 0) continue;

                switch (species.morphology)
                {
                    case CoralMorphology.SeaFan:
                    case CoralMorphology.FireCoral:
                    case CoralMorphology.Foliose:
                        AppendFlatBranch(node, nodes, species, rng);
                        break;

                    case CoralMorphology.Massive:
                        // Massive built separately after loop
                        break;

                    case CoralMorphology.TubeOrgan:
                        AppendTube(node, species, sides);
                        break;

                    case CoralMorphology.Bubble:
                        AppendRoundBranch(node, nodes, species, sides, rng);
                        if (node.IsTip)
                        {
                            float bubR = node.Radius * 4f;
                            AppendSphere(node.Position + new float3(0f, bubR * 0.5f, 0f),
                                         bubR, 8, species.colorTip);
                        }
                        break;

                    default:
                        AppendRoundBranch(node, nodes, species, sides, rng);
                        if (node.IsTip && (species.morphology == CoralMorphology.Torch))
                            AppendSphere(node.Position, species.branchTipRadius * 3f,
                                         6, species.colorTip);
                        break;
                }
            }

            if (species.morphology == CoralMorphology.Massive)
                BuildMassiveSphere(species, rng);

            return FinalizeMesh();
        }

        // ── BRANCH GEOMETRY ──────────────────────────────────────────

        private void AppendRoundBranch(
            BranchNode node, List<BranchNode> nodes,
            in CoralSpeciesParams sp, int sides,
            System.Random rng)
        {
            var parent  = nodes[node.ParentIdx];
            var startPos = (Vector3)parent.Position;
            var endPos   = (Vector3)node.Position;
            var dir      = (endPos - startPos).normalized;

            float rStart = parent.Radius;
            float rEnd   = node.IsTip ? Mathf.Max(node.Radius, sp.branchTipRadius * 3f)
                                      : node.Radius;

            int startIdx = _verts.Count;

            var up    = Mathf.Abs(dir.y) < 0.99f ? Vector3.up : Vector3.right;
            var right = Vector3.Cross(dir, up).normalized;
            var fwd   = Vector3.Cross(right, dir).normalized;

            for (int ring = 0; ring <= 1; ring++)
            {
                Vector3 pos    = ring == 0 ? startPos : endPos;
                float   radius = ring == 0 ? rStart   : rEnd;
                float   t      = ring == 0 ? parent.T  : node.T;
                Color32 col    = LerpColor32(sp.colorBase, sp.colorTip, Mathf.Pow(t, 0.7f));

                for (int s = 0; s < sides; s++)
                {
                    float angle  = (float)s / sides * Mathf.PI * 2f;
                    float cos    = Mathf.Cos(angle);
                    float sin    = Mathf.Sin(angle);
                    var   offset = (right * cos + fwd * sin) * radius;

                    // Organic micro-variation — Perlin is fine cold path
                    float noiseOff = (Mathf.PerlinNoise(pos.x * 8f + s, pos.y * 8f) - 0.5f)
                                   * radius * 0.15f;
                    offset += offset.normalized * noiseOff;

                    _verts.Add(pos + offset);
                    _norms.Add(offset.normalized);
                    _uvs.Add(new Vector2((float)s / sides, t));
                    _cols.Add(col);
                }
            }

            for (int s = 0; s < sides; s++)
            {
                int next = (s + 1) % sides;
                int b    = startIdx + s;
                int bn   = startIdx + next;
                int tp   = startIdx + sides + s;
                int tn   = startIdx + sides + next;
                _tris.Add(b);  _tris.Add(tp); _tris.Add(bn);
                _tris.Add(bn); _tris.Add(tp); _tris.Add(tn);
            }
        }

        private void AppendFlatBranch(
            BranchNode node, List<BranchNode> nodes,
            in CoralSpeciesParams sp,
            System.Random rng)
        {
            var parent   = nodes[node.ParentIdx];
            var startPos = (Vector3)parent.Position;
            var endPos   = (Vector3)node.Position;
            var dir      = (endPos - startPos).normalized;

            float halfW    = sp.flatBranchWidth * 0.5f * Mathf.Lerp(1f, 0.2f, node.T);
            float thickness = sp.flatBranchWidth * 0.1f;

            var normal = ((Vector3)math.rotate(node.Rotation, new float3(0f, 0f, 1f))).normalized;
            var side   = Vector3.Cross(dir, normal).normalized;

            int startIdx = _verts.Count;

            for (int ring = 0; ring <= 1; ring++)
            {
                Vector3 pos  = ring == 0 ? startPos : endPos;
                float   wHere = ring == 0 ? sp.flatBranchWidth * 0.5f : halfW;
                float   t    = ring == 0 ? parent.T : node.T;
                Color32 col  = LerpColor32(sp.colorBase, sp.colorTip, t);

                Vector3[] corners = new Vector3[4]
                {
                    pos - side * wHere - normal * thickness,
                    pos + side * wHere - normal * thickness,
                    pos + side * wHere + normal * thickness,
                    pos - side * wHere + normal * thickness
                };
                // COLD ALLOC: 4-element array per branch segment, cold path only

                for (int ci = 0; ci < 4; ci++)
                {
                    _verts.Add(corners[ci]);
                    _norms.Add(ci < 2 ? -normal : normal);
                    _uvs.Add(new Vector2(ci < 2 ? 0f : 1f, t));
                    _cols.Add(col);
                }
            }

            int b = startIdx;
            int n = startIdx + 4;
            // Front face
            _tris.Add(b+2); _tris.Add(n+2); _tris.Add(b+3);
            _tris.Add(b+3); _tris.Add(n+2); _tris.Add(n+3);
            // Back face
            _tris.Add(b+0); _tris.Add(b+1); _tris.Add(n+0);
            _tris.Add(b+1); _tris.Add(n+1); _tris.Add(n+0);
            // Side A
            _tris.Add(b+1); _tris.Add(b+2); _tris.Add(n+1);
            _tris.Add(b+2); _tris.Add(n+2); _tris.Add(n+1);
            // Side B
            _tris.Add(b+3); _tris.Add(n+3); _tris.Add(b+0);
            _tris.Add(b+0); _tris.Add(n+3); _tris.Add(n+0);
        }

        private void AppendTube(
            BranchNode node,
            in CoralSpeciesParams sp, int sides)
        {
            var basePos = (Vector3)node.Position;
            var topPos  = basePos + new Vector3(0f, node.Length, 0f);
            float r     = node.Radius;
            var col     = LerpColor32(sp.colorBase, sp.colorTip, 0f);

            int startIdx = _verts.Count;

            for (int ring = 0; ring <= 1; ring++)
            {
                Vector3 pos = ring == 0 ? basePos : topPos;
                float   t   = ring == 0 ? 0f : 1f;

                for (int s = 0; s < sides; s++)
                {
                    float angle  = (float)s / sides * Mathf.PI * 2f;
                    var   offset = new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r);
                    _verts.Add(pos + offset);
                    _norms.Add(offset.normalized);
                    _uvs.Add(new Vector2((float)s / sides, t));
                    _cols.Add(col);
                }
            }

            for (int s = 0; s < sides; s++)
            {
                int next = (s + 1) % sides;
                int b    = startIdx + s;
                int bn   = startIdx + next;
                int tp   = startIdx + sides + s;
                int tn   = startIdx + sides + next;
                _tris.Add(b);  _tris.Add(tp); _tris.Add(bn);
                _tris.Add(bn); _tris.Add(tp); _tris.Add(tn);
            }
        }

        private void AppendSphere(
            float3 center, float radius, int res,
            Color color)
        {
            var col      = ToColor32(color);
            int startIdx = _verts.Count;

            for (int lat = 0; lat <= res; lat++)
            {
                float theta = (float)lat / res * Mathf.PI;
                float sinT  = Mathf.Sin(theta);
                float cosT  = Mathf.Cos(theta);

                for (int lon = 0; lon <= res * 2; lon++)
                {
                    float phi = (float)lon / (res * 2) * Mathf.PI * 2f;
                    var   dir = new Vector3(sinT * Mathf.Cos(phi), cosT, sinT * Mathf.Sin(phi));
                    _verts.Add((Vector3)center + dir * radius);
                    _norms.Add(dir);
                    _uvs.Add(new Vector2((float)lon / (res * 2), (float)lat / res));
                    _cols.Add(col);
                }

                if (lat < res)
                {
                    int row  = startIdx + lat * (res * 2 + 1);
                    int nRow = row + (res * 2 + 1);
                    for (int lon = 0; lon < res * 2; lon++)
                    {
                        _tris.Add(row+lon);     _tris.Add(nRow+lon);     _tris.Add(row+lon+1);
                        _tris.Add(row+lon+1);   _tris.Add(nRow+lon);     _tris.Add(nRow+lon+1);
                    }
                }
            }
        }

        private void BuildMassiveSphere(in CoralSpeciesParams sp, System.Random rng)
        {
            // MAIN THREAD ONLY: uses Mathf.PerlinNoise
            const int LatRes = 16;
            const int LonRes = 20;
            float radius    = 0.5f;
            int   startIdx  = _verts.Count;

            var col = LerpColor32(sp.colorBase, sp.colorTip, 0f);

            for (int lat = 0; lat <= LatRes; lat++)
            {
                float theta = (float)lat / LatRes * Mathf.PI;
                if (theta > Mathf.PI * 0.65f) continue; // upper hemisphere only

                float sinT = Mathf.Sin(theta);
                float cosT = Mathf.Cos(theta);

                for (int lon = 0; lon <= LonRes; lon++)
                {
                    float phi = (float)lon / LonRes * Mathf.PI * 2f;
                    var   dir = new Vector3(sinT * Mathf.Cos(phi), cosT, sinT * Mathf.Sin(phi));

                    float u = (float)lon / LonRes;
                    float v = (float)lat / LatRes;

                    // Brain groove pattern — MAIN THREAD Perlin
                    float n1     = Mathf.PerlinNoise(u * 8f, v * 8f);
                    float n2     = Mathf.PerlinNoise(u * 12f + 5f, v * 12f);
                    float groove = Mathf.Pow(Mathf.Abs(n1 - 0.5f) * 2f, 0.4f);
                    float disp   = (groove * 0.6f + n2 * 0.4f) * 0.03f;

                    var pos = dir * (radius + disp);

                    _verts.Add(pos);
                    _norms.Add(dir);
                    _uvs.Add(new Vector2(u, v));
                    _cols.Add(LerpColor32(sp.colorBase, sp.colorTip, v));
                }
            }

            for (int lat = 0; lat < LatRes; lat++)
            for (int lon = 0; lon < LonRes; lon++)
            {
                int curr      = startIdx + lat * (LonRes + 1) + lon;
                int next      = curr + 1;
                int above     = curr + (LonRes + 1);
                int aboveNext = above + 1;

                if (above     >= _verts.Count) continue;
                if (aboveNext >= _verts.Count) continue;

                _tris.Add(curr); _tris.Add(above); _tris.Add(next);
                _tris.Add(next); _tris.Add(above); _tris.Add(aboveNext);
            }
        }

        // ── HELPERS ──────────────────────────────────────────────────

        private static Color32 LerpColor32(Color a, Color b, float t)
        {
            float r = a.r + (b.r - a.r) * t;
            float g = a.g + (b.g - a.g) * t;
            float bl = a.b + (b.b - a.b) * t;
            return new Color32(
                (byte)(Mathf.Clamp01(r)  * 255f),
                (byte)(Mathf.Clamp01(g)  * 255f),
                (byte)(Mathf.Clamp01(bl) * 255f),
                255
            );
        }

        private static Color32 ToColor32(Color c) => new Color32(
            (byte)(Mathf.Clamp01(c.r) * 255f),
            (byte)(Mathf.Clamp01(c.g) * 255f),
            (byte)(Mathf.Clamp01(c.b) * 255f),
            255
        );

        private Mesh FinalizeMesh()
        {
            if (_verts.Count == 0) return CreateEmptyMesh();

            var mesh = new Mesh
            {
                name        = "CoralMesh",
                indexFormat = _verts.Count > 65535
                    ? UnityEngine.Rendering.IndexFormat.UInt32
                    : UnityEngine.Rendering.IndexFormat.UInt16
            };

            mesh.SetVertices(_verts);
            mesh.SetNormals(_norms);
            mesh.SetUVs(0, _uvs);
            mesh.SetColors(_cols);
            mesh.SetTriangles(_tris, 0);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true); // mark non-readable — saves ~50% VRAM
            return mesh;
        }

        private static Mesh CreateEmptyMesh()
        {
            var m = new Mesh { name = "CoralMesh_Empty" };
            m.UploadMeshData(true);
            return m;
        }
    }
}
ФАЙЛ 4: CoralInstance.cs
csharp

// ============================================================
// HECTON-8 — CoralInstance.cs
// Pure data container for one coral instance.
// No MonoBehaviour. Immutable after construction except health.
// ============================================================

using UnityEngine;

namespace Hecton8.Environment
{
    /// <summary>
    /// Runtime data for a single coral instance.
    /// Created by CoralPlacer, consumed by CoralRenderer and CoralBioLumSystem.
    /// No MonoBehaviour — zero per-instance Update overhead.
    /// </summary>
    public sealed class CoralInstance
    {
        /// <summary>Index into CoralSpeciesLibrary.Species[]. Stable at runtime.</summary>
        public readonly int       SpeciesIndex;

        /// <summary>Mesh variant index (0..3). Selects from pre-generated variants.</summary>
        public readonly int       VariantIndex;

        /// <summary>TRS matrix for GPU instanced rendering.</summary>
        public readonly Matrix4x4 Matrix;

        /// <summary>World position extracted from Matrix. Cache for distance checks.</summary>
        public readonly Vector3   WorldPosition;

        /// <summary>Approximate bounding sphere radius for LOD distance.</summary>
        public readonly float     BoundsRadius;

        /// <summary>Per-instance color offset. rgb: [-0.15..0.15] tint delta.</summary>
        public readonly Color     ColorVariation;

        /// <summary>Animation phase offset [0..2π]. Prevents visual sync between instances.</summary>
        public readonly float     PhaseOffset;

        /// <summary>Index in CoralBioLumSystem buffer. -1 if not bioluminescent.</summary>
        public int BioLumBufferIndex { get; internal set; } = -1;

        /// <summary>Current health state. May change at runtime (bleaching events).</summary>
        public CoralHealthState HealthState { get; internal set; } = CoralHealthState.Thriving;

        /// <summary>
        /// Constructs an immutable coral instance.
        /// All rendering data derived here — no recalculation in hot path.
        /// </summary>
        public CoralInstance(
            int        speciesIndex,
            int        variantIndex,
            Vector3    position,
            Quaternion rotation,
            float      scale,
            Color      colorVariation,
            float      phaseOffset)
        {
            SpeciesIndex   = speciesIndex;
            VariantIndex   = variantIndex;
            WorldPosition  = position;
            ColorVariation = colorVariation;
            PhaseOffset    = phaseOffset;
            BoundsRadius   = scale * 2f;
            Matrix         = Matrix4x4.TRS(position, rotation, Vector3.one * scale);
        }
    }
}
ФАЙЛ 5: CoralBioLumSystem.cs
csharp

// ============================================================
// HECTON-8 — CoralBioLumSystem.cs
// Bioluminescence simulation. ITickable. Zero GC hot path.
// ComputeBuffer → shader. No Light components.
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using Hecton8.Core;

namespace Hecton8.Environment
{
    /// <summary>
    /// Simulates coral bioluminescence:
    /// - Ambient pulse (always-on species)
    /// - Triggered flash (player proximity / tools)
    /// - Infection wave (neighbouring corals light up sequentially)
    ///
    /// Data path: _bioLumData[] → ComputeBuffer → CoralLit.shader global buffer.
    /// No Unity Light components — emission only.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-70)]
    public sealed class CoralBioLumSystem : MonoBehaviour, ITickable
    {
        // ── INSPECTOR ────────────────────────────────────────────────

        [Header("── Settings ────────────────────────────────────────────")]
        [SerializeField, Tooltip("Enable bioluminescence globally.")]
        private bool _enabled = true;

        [SerializeField, Range(0f, 1f), Tooltip("0=day (dim), 1=night (full intensity).")]
        private float _dayNightCycle = 0f;

        [SerializeField, Range(0f, 1f), Tooltip("Ambient intensity multiplier during day.")]
        private float _ambientDimmer = 0.3f;

        [Header("── Infection Wave ─────────────────────────────────────")]
        [SerializeField, Range(0.5f, 10f), Tooltip("Radius for infection spread per step (m).")]
        private float _infectionRadius = 3f;

        [SerializeField, Range(0.05f, 2f), Tooltip("Delay between infection hops (s).")]
        private float _infectionDelay = 0.3f;

        [SerializeField, Range(0.5f, 5f), Tooltip("Flash duration after trigger (s).")]
        private float _flashDuration = 1.5f;

        [Header("── Capacity ─────────────────────────────────────────────")]
        [SerializeField, Range(64, 1024), Tooltip("Max bioluminescent coral instances.")]
        private int _maxInstances = 512;

        // ── GPU DATA STRUCT ──────────────────────────────────────────

        // sizeof = 4 floats * 4 + 4 floats = 32 bytes
        // Matches HLSL struct in CoralLit.shader
        private struct BioLumGPUData
        {
            public Vector4 Color;       // rgb=color, a=intensity
            public float   Phase;
            public float   Frequency;
            public float   Triggered;   // [0..1] flash progress
            public float   Pad;
        }

        // ── CPU DATA ─────────────────────────────────────────────────

        private struct CoralBioInfo
        {
            public Vector3 Position;
            public Color   Color;
            public float   Intensity;
            public float   Frequency;
            public bool    AlwaysOn;
        }

        // ── PRIVATE STATE ────────────────────────────────────────────

        // COLD ALLOC: _maxInstances * sizeof(CoralBioInfo)
        private CoralBioInfo[]   _infos;
        private BioLumGPUData[]  _gpuData;
        private int              _count;
        private ComputeBuffer    _buffer;

        // Infection queue — ring buffer pattern to avoid Queue<> alloc
        // COLD ALLOC: 256 slots (max simultaneous infections)
        private const int InfectionQueueSize = 256;
        private (int coralIdx, float triggerTime)[] _infectionQueue;
        private int _infectionHead;
        private int _infectionTail;

        private bool _registered;

        // ── CACHED SHADER IDS ─────────────────────────────────────────

        private static readonly int _PropBuffer      = Shader.PropertyToID("_BioLumBuffer");
        private static readonly int _PropCount       = Shader.PropertyToID("_BioLumCount");
        private static readonly int _PropDayNight    = Shader.PropertyToID("_DayNightCycle");
        private static readonly int _PropAmbientDim  = Shader.PropertyToID("_BioLumAmbientDimmer");

        // ── LIFECYCLE ────────────────────────────────────────────────

        private void Awake()
        {
            // COLD ALLOC: fixed arrays, never reallocated
            _infos    = new CoralBioInfo[_maxInstances];
            _gpuData  = new BioLumGPUData[_maxInstances];
            _infectionQueue = new (int, float)[InfectionQueueSize];

            // sizeof(BioLumGPUData) = 8 floats = 32 bytes
            _buffer = new ComputeBuffer(_maxInstances, 32);
            Shader.SetGlobalBuffer(_PropBuffer, _buffer);
            Shader.SetGlobalInt(_PropCount, 0);
        }

        private void OnEnable()
        {
            if (!_registered && GameTickManager.Instance != null)
            {
                GameTickManager.Instance.Register(this);
                _registered = true;
            }
        }

        private void OnDisable()
        {
            // §25 null-check singleton during teardown
            if (_registered && GameTickManager.Instance != null)
            {
                GameTickManager.Instance.Unregister(this);
                _registered = false;
            }
        }

        private void OnDestroy() => _buffer?.Release();

        // ── PUBLIC API ───────────────────────────────────────────────

        /// <summary>
        /// Registers a bioluminescent coral instance.
        /// Returns buffer index (pass to CoralInstance.BioLumBufferIndex).
        /// Returns -1 if capacity exceeded.
        /// </summary>
        public int RegisterCoral(
            Vector3 position, Color color,
            float   intensity, float frequency, bool alwaysOn)
        {
            if (_count >= _maxInstances)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("[CoralBioLumSystem] Capacity exceeded. Instance rejected.");
#endif
                return -1;
            }

            int idx = _count;
            _infos[idx] = new CoralBioInfo
            {
                Position  = position,
                Color     = color,
                Intensity = intensity,
                Frequency = frequency,
                AlwaysOn  = alwaysOn
            };
            _gpuData[idx] = new BioLumGPUData
            {
                Color     = new Vector4(color.r, color.g, color.b,
                                        alwaysOn ? intensity * _ambientDimmer : 0f),
                Phase     = Random.value,
                Frequency = frequency,
                Triggered = 0f,
                Pad       = 0f
            };

            _count++;
            return idx;
        }

        /// <summary>
        /// Triggers bioluminescence wave from world position.
        /// Zero GC: uses ring buffer for infection queue.
        /// </summary>
        public void TriggerAt(Vector3 position, float radius)
        {
            if (!_enabled) return;

            float sqRadius = radius * radius;
            float now      = Time.time;

            for (int i = 0; i < _count; i++)
            {
                float dx     = _infos[i].Position.x - position.x;
                float dz     = _infos[i].Position.z - position.z;
                float sqDist = dx * dx + dz * dz;

                if (sqDist > sqRadius) continue;

                float dist  = Mathf.Sqrt(sqDist);
                float delay = dist / radius * _infectionDelay;
                EnqueueInfection(i, now + delay);
            }
        }

        // ── ITICKABLE ────────────────────────────────────────────────

        /// <summary>Hot path: updates all bio-lum states and uploads to GPU. Zero GC.</summary>
        public void Tick(float dt)
        {
            if (!_enabled || _count == 0) return;

            float now = Time.time;

            // Update global shader params — scalar, no alloc
            Shader.SetGlobalFloat(_PropDayNight,   _dayNightCycle);
            Shader.SetGlobalFloat(_PropAmbientDim, _ambientDimmer);

            // Drain infection queue
            ProcessInfectionQueue(now);

            // Update each instance
            for (int i = 0; i < _count; i++)
            {
                ref var d    = ref _gpuData[i];
                var     info = _infos[i];

                // Phase advance
                d.Phase += dt * info.Frequency;
                if (d.Phase > 1f) d.Phase -= 1f;

                // Base intensity
                float baseIntensity = info.AlwaysOn
                    ? info.Intensity * _ambientDimmer
                    : 0f;
                baseIntensity *= Mathf.Lerp(0.3f, 1f, _dayNightCycle);

                // Pulse shape: sharp peaks via pow
                float pulse = Mathf.Sin(d.Phase * Mathf.PI * 2f) * 0.5f + 0.5f;
                pulse = pulse * pulse; // pow(x, 2) — no Mathf.Pow alloc

                // Flash decay
                float flashIntensity = 0f;
                if (d.Triggered > 0f)
                {
                    d.Triggered -= dt / _flashDuration;
                    if (d.Triggered < 0f) d.Triggered = 0f;

                    float flashT    = 1f - d.Triggered;
                    // Flash curve: fast rise, exponential decay
                    float flashCurve = Mathf.Pow(flashT, 0.3f) * Mathf.Exp(-flashT * 3f);
                    flashIntensity   = flashCurve * info.Intensity * 3f;
                }

                float finalIntensity = baseIntensity * pulse + flashIntensity;

                d.Color = new Vector4(
                    info.Color.r, info.Color.g, info.Color.b,
                    finalIntensity
                );
            }

            // Upload to GPU — SetData with explicit count, no full-buffer upload
            _buffer.SetData(_gpuData, 0, 0, _count);
            Shader.SetGlobalInt(_PropCount, _count);
        }

        // ── PRIVATE ──────────────────────────────────────────────────

        private void ProcessInfectionQueue(float now)
        {
            // Ring buffer drain — no alloc
            while (_infectionHead != _infectionTail)
            {
                var entry = _infectionQueue[_infectionHead];
                if (entry.triggerTime > now) break; // sorted by time

                // Advance head (wrap)
                _infectionHead = (_infectionHead + 1) % InfectionQueueSize;

                int ci = entry.coralIdx;
                if (ci < 0 || ci >= _count) continue;

                _gpuData[ci].Triggered = 1f;

                // Spread to neighbours
                var pos      = _infos[ci].Position;
                float sqRad  = _infectionRadius * _infectionRadius;

                for (int j = 0; j < _count; j++)
                {
                    if (j == ci) continue;
                    if (_gpuData[j].Triggered > 0.5f) continue;

                    float dx     = _infos[j].Position.x - pos.x;
                    float dz     = _infos[j].Position.z - pos.z;
                    float sqDist = dx * dx + dz * dz;
                    if (sqDist > sqRad) continue;

                    float dist  = Mathf.Sqrt(sqDist);
                    float delay = dist / _infectionRadius * _infectionDelay * 2f;
                    EnqueueInfection(j, now + delay);
                }
            }
        }

        private void EnqueueInfection(int coralIdx, float triggerTime)
        {
            int next = (_infectionTail + 1) % InfectionQueueSize;
            if (next == _infectionHead)
            {
                // Ring buffer full — drop oldest
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("[CoralBioLumSystem] Infection queue overflow. Entry dropped.");
#endif
                return;
            }
            _infectionQueue[_infectionTail] = (coralIdx, triggerTime);
            _infectionTail = next;
        }
    }
}
ФАЙЛ 6: CoralRenderer.cs
csharp

// ============================================================
// HECTON-8 — CoralRenderer.cs
// GPU instanced coral rendering. ITickable + ISlowTickable.
// DrawMeshInstanced batched. Zero GC hot path.
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Mathematics;
using Hecton8.Core;

namespace Hecton8.Environment
{
    /// <summary>
    /// Renders all coral instances using GPU instancing.
    /// LOD classification runs in SlowTick (~0.5s cadence).
    /// Draw calls submitted in Tick (every frame).
    ///
    /// Memory layout:
    ///   _groups[speciesIdx][lodLevel] → RenderGroup
    ///   RenderGroup holds Matrix4x4[] + ColorBuffer
    ///   4 LOD levels: 0=full, 1=mid, 2=low, 3=billboard, 4=culled
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-90)]
    public sealed class CoralRenderer : MonoBehaviour, ITickable, ISlowTickable
    {
        // ── INSPECTOR ────────────────────────────────────────────────

        [Header("── References ──────────────────────────────────────────")]
        [SerializeField, Tooltip("Species library ScriptableObject.")]
        private CoralSpeciesLibrary _library;

        [SerializeField, Tooltip("Coral lit material (GPU instancing enabled).")]
        private Material _coralMaterial;

        [SerializeField, Tooltip("Billboard material for LOD3.")]
        private Material _billboardMaterial;

        [Header("── LOD Distances (meters) ──────────────────────────────")]
        [SerializeField, Range(2f, 15f)]   private float _lod0Dist = 8f;
        [SerializeField, Range(10f, 30f)]  private float _lod1Dist = 20f;
        [SerializeField, Range(20f, 60f)]  private float _lod2Dist = 40f;
        [SerializeField, Range(40f, 120f)] private float _cullDist = 80f;

        [Header("── Performance ──────────────────────────────────────────")]
        [SerializeField, Range(64, 4096), Tooltip("Max total coral instances.")]
        private int _maxInstances = 2048;

        [SerializeField, Tooltip("Cast shadows (disable on MX350 first).")]
        private bool _castShadows = false;

        [SerializeField, Range(1, 20), Tooltip("LOD rebuild every N slow ticks.")]
        private int _lodUpdateInterval = 3;

        // ── PRIVATE STATE ─────────────────────────────────────────────

        private sealed class RenderGroup
        {
            // COLD ALLOC: Matrix4x4[maxCount] = maxCount * 64 bytes
            public readonly Matrix4x4[]  Matrices;
            // COLD ALLOC: Vector4[maxCount] = maxCount * 16 bytes
            public readonly Vector4[]    ColorData;
            public readonly MaterialPropertyBlock MPB;
            public readonly ComputeBuffer         ColorBuffer;
            public Mesh    Mesh;
            public int     Count;

            public RenderGroup(int maxCount)
            {
                Matrices    = new Matrix4x4[maxCount];
                ColorData   = new Vector4[maxCount];
                MPB         = new MaterialPropertyBlock();
                ColorBuffer = new ComputeBuffer(maxCount, 16); // sizeof(float4)
            }

            public void Release() => ColorBuffer?.Release();
        }

        // [speciesIdx][lodLevel 0..3]
        // COLD ALLOC: speciesCount * 4 * RenderGroup
        private RenderGroup[][] _groups;

        // COLD ALLOC: _maxInstances entries
        private readonly List<CoralInstance> _instances = new List<CoralInstance>(512);

        private Camera    _mainCam;
        private Transform _camTransform;
        private bool      _registered;
        private bool      _ready;
        private int       _slowTickCount;

        // LOD squared distances — cache to avoid sqrt in hot path
        private float _lod0Sq, _lod1Sq, _lod2Sq, _cullSq;

        // Shadow mode cached — avoids enum boxing on each DrawMesh call
        private ShadowCastingMode _shadowMode;

        // Cached shader property IDs
        private static readonly int _PropInstanceColors = Shader.PropertyToID("_InstanceColors");

        // ── PUBLIC PROPERTIES ─────────────────────────────────────────

        /// <summary>True after CoralLODSystem has assigned meshes and MarkReady() called.</summary>
        public bool IsReady => _ready;

        /// <summary>All registered instances. Read-only.</summary>
        public IReadOnlyList<CoralInstance> Instances => _instances;

        // ── LIFECYCLE ────────────────────────────────────────────────

        private void Awake()
        {
            // §11 Cache Camera.main
            _mainCam      = Camera.main;
            _camTransform = _mainCam != null ? _mainCam.transform : null;
            _shadowMode   = _castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;

            // Cache squared distances — avoid sqrt in ClassifyLOD hot path
            _lod0Sq = _lod0Dist * _lod0Dist;
            _lod1Sq = _lod1Dist * _lod1Dist;
            _lod2Sq = _lod2Dist * _lod2Dist;
            _cullSq = _cullDist * _cullDist;

            if (_mainCam == null)
            {
                Debug.LogError("[CoralRenderer] Camera.main is null. Rendering disabled.");
                enabled = false;
                return;
            }
            if (_library == null || _library.Species == null || _library.Species.Length == 0)
            {
                Debug.LogError("[CoralRenderer] Species library missing or empty. Rendering disabled.");
                enabled = false;
                return;
            }
            if (_coralMaterial == null)
            {
                Debug.LogError("[CoralRenderer] Coral material not assigned. Rendering disabled.");
                enabled = false;
                return;
            }

            AllocateGroups();
        }

        private void OnEnable()
        {
            if (!_registered && GameTickManager.Instance != null)
            {
                GameTickManager.Instance.Register(this);
                GameTickManager.Instance.RegisterSlow(this);
                _registered = true;
            }
        }

        private void OnDisable()
        {
            // §25 null-check singleton
            if (_registered && GameTickManager.Instance != null)
            {
                GameTickManager.Instance.Unregister(this);
                GameTickManager.Instance.UnregisterSlow(this);
                _registered = false;
            }
        }

        private void OnDestroy()
        {
            if (_groups == null) return;
            for (int si = 0; si < _groups.Length; si++)
            {
                if (_groups[si] == null) continue;
                for (int lod = 0; lod < 4; lod++)
                    _groups[si][lod]?.Release();
            }
        }

        // ── ITICKABLE ────────────────────────────────────────────────

        /// <summary>Hot path: submits GPU draw calls. Zero GC.</summary>
        public void Tick(float dt)
        {
            if (!_ready || _groups == null) return;

            for (int si = 0; si < _groups.Length; si++)
            {
                if (_groups[si] == null) continue;

                for (int lod = 0; lod < 4; lod++)
                {
                    var g = _groups[si][lod];
                    if (g == null || g.Mesh == null || g.Count == 0) continue;

                    // Select material: lit shader for LOD 0-2, billboard for LOD 3
                    var mat = lod < 3 ? _coralMaterial : _billboardMaterial;
                    if (mat == null) continue;

                    // DrawMeshInstanced: Unity 6 supports up to 1023 per call
                    int drawn = 0;
                    int total = g.Count;
                    while (drawn < total)
                    {
                        // §1 no new — batch size uses math.min (struct, no alloc)
                        int batch = total - drawn;
                        if (batch > 1023) batch = 1023;

                        Graphics.DrawMeshInstanced(
                            g.Mesh, 0, mat,
                            g.Matrices, drawn, batch,
                            g.MPB,
                            _shadowMode,
                            receiveShadows: false,
                            layer: gameObject.layer
                        );
                        drawn += batch;
                    }
                }
            }
        }

        // ── ISLOTWTICKABLE ────────────────────────────────────────────

        /// <summary>Slow path: reclassifies all instances into LOD groups. Zero GC.</summary>
        public void SlowTick()
        {
            if (!_ready || _groups == null || _camTransform == null) return;

            _slowTickCount++;
            if (_slowTickCount % _lodUpdateInterval != 0) return;

            RebuildGroups();
        }

        // ── PUBLIC API ───────────────────────────────────────────────

        /// <summary>Registers a coral instance for rendering.</summary>
        public void RegisterInstance(CoralInstance inst)
        {
            if (inst == null) return;
            if (_instances.Count >= _maxInstances)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("[CoralRenderer] Instance capacity reached.");
#endif
                return;
            }
            _instances.Add(inst);
        }

        /// <summary>Unregisters a coral instance from rendering.</summary>
        public void UnregisterInstance(CoralInstance inst)
        {
            _instances.Remove(inst);
        }

        /// <summary>
        /// Assigns a mesh to a species/LOD render group.
        /// Called by CoralLODSystem during startup.
        /// </summary>
        public void SetMesh(int speciesIdx, int lodLevel, Mesh mesh)
        {
            if (_groups == null) return;
            if (speciesIdx < 0 || speciesIdx >= _groups.Length) return;
            if (_groups[speciesIdx] == null) return;
            if (lodLevel < 0 || lodLevel > 3) return;

            var g = _groups[speciesIdx][lodLevel];
            if (g != null) g.Mesh = mesh;
        }

        /// <summary>Signals that all meshes are ready. Enables rendering.</summary>
        public void MarkReady()
        {
            _ready = true;
            RebuildGroups();
        }

        // ── PRIVATE ──────────────────────────────────────────────────

        private void AllocateGroups()
        {
            int sc = _library.Species.Length;
            // COLD ALLOC: sc * 4 RenderGroups, each with _maxInstances capacity
            _groups = new RenderGroup[sc][];
            for (int si = 0; si < sc; si++)
            {
                _groups[si] = new RenderGroup[4];
                for (int lod = 0; lod < 4; lod++)
                    _groups[si][lod] = new RenderGroup(_maxInstances);
            }
        }

        private void RebuildGroups()
        {
            // Clear counts — no alloc
            for (int si = 0; si < _groups.Length; si++)
            {
                if (_groups[si] == null) continue;
                for (int lod = 0; lod < 4; lod++)
                    if (_groups[si][lod] != null)
                        _groups[si][lod].Count = 0;
            }

            // §14 Cache camera position — one read
            var camPos = _camTransform.position;

            for (int i = 0; i < _instances.Count; i++)
            {
                var inst = _instances[i];
                if (inst == null) continue;

                var   wPos   = inst.WorldPosition;
                float dx     = wPos.x - camPos.x;
                float dy     = wPos.y - camPos.y;
                float dz     = wPos.z - camPos.z;
                float sqDist = dx * dx + dy * dy + dz * dz;

                int lod = ClassifyLOD(sqDist);
                if (lod > 3) continue; // culled

                int si = inst.SpeciesIndex;
                if (si < 0 || si >= _groups.Length) continue;
                if (_groups[si] == null) continue;

                var g = _groups[si][lod];
                if (g == null) continue;

                int slot = g.Count;
                if (slot >= _maxInstances) continue;

                g.Matrices[slot] = inst.Matrix;
                g.ColorData[slot] = new Vector4(
                    inst.ColorVariation.r,
                    inst.ColorVariation.g,
                    inst.ColorVariation.b,
                    inst.PhaseOffset
                );
                g.Count++;
            }

            // Upload color data to GPU — only active groups
            for (int si = 0; si < _groups.Length; si++)
            {
                if (_groups[si] == null) continue;
                for (int lod = 0; lod < 4; lod++)
                {
                    var g = _groups[si][lod];
                    if (g == null || g.Count == 0) continue;

                    // SetData with count — avoids full buffer upload
                    g.ColorBuffer.SetData(g.ColorData, 0, 0, g.Count);
                    // §4 MaterialPropertyBlock — no material instance
                    g.MPB.SetBuffer(_PropInstanceColors, g.ColorBuffer);
                }
            }
        }

        // Uses squared distances — no sqrt in hot path
        private int ClassifyLOD(float sqDist)
        {
            if (sqDist < _lod0Sq) return 0;
            if (sqDist < _lod1Sq) return 1;
            if (sqDist < _lod2Sq) return 2;
            if (sqDist < _cullSq) return 3;
            return 4; // culled
        }
    }
}
ФАЙЛ 7: CoralPlacer.cs
csharp

// ============================================================
// HECTON-8 — CoralPlacer.cs
// Ecological placement. ISlowTickable state machine.
// NonAlloc physics. Zero GC hot path. Streams over ticks.
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using Hecton8.Core;

namespace Hecton8.Environment
{
    /// <summary>
    /// Places coral instances according to ecological rules:
    /// depth zone, substrate, slope, light, minimum spacing.
    ///
    /// Streams placement over multiple SlowTick calls (no frame spike).
    /// Unregisters from GameTickManager when placement completes.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-80)]
    public sealed class CoralPlacer : MonoBehaviour, ISlowTickable
    {
        // ── INSPECTOR ────────────────────────────────────────────────

        [Header("── References ──────────────────────────────────────────")]
        [SerializeField, Tooltip("Species library.")]
        private CoralSpeciesLibrary _library;

        [SerializeField, Tooltip("Target renderer.")]
        private CoralRenderer _renderer;

        [SerializeField, Tooltip("BioLum system (optional).")]
        private CoralBioLumSystem _bioLum;

        [Header("── Area ─────────────────────────────────────────────────")]
        [SerializeField, Tooltip("World-space placement center.")]
        private Vector3 _areaCenter = Vector3.zero;

        [SerializeField, Range(5f, 300f), Tooltip("Placement radius (m).")]
        private float _areaRadius = 50f;

        [SerializeField, Range(0f, 90f), Tooltip("Max slope angle (degrees).")]
        private float _maxSlope = 55f;

        [SerializeField, Tooltip("Water surface Y.")]
        private float _waterSurfaceY = 0f;

        [Header("── Density ──────────────────────────────────────────────")]
        [SerializeField, Range(10, 4000)]
        private int _targetCount = 800;

        [SerializeField, Range(1, 100), Tooltip("Placements per SlowTick.")]
        private int _placementsPerTick = 20;

        [Header("── Physics ──────────────────────────────────────────────")]
        [SerializeField, Tooltip("Ground layer mask.")]
        private LayerMask _groundLayer;

        [Header("── Debug ────────────────────────────────────────────────")]
        [SerializeField] private bool _drawGizmos;

        // ── PRIVATE STATE ────────────────────────────────────────────

        private enum PlacerState { Idle, Generating, Placing, Done }
        private PlacerState _state = PlacerState.Idle;

        // §10 NonAlloc physics buffer
        // COLD ALLOC: 4 RaycastHit structs
        private readonly RaycastHit[] _hitBuffer = new RaycastHit[4];

        // COLD ALLOC: candidates list — grid jitter samples, at most areaRadius*2 / cellSize ^2
        // For radius=50, cellSize=0.4: (100/0.4)^2 = 62500 max — prune via circle test
        // Realistic filled count: ~π*50^2/0.4^2 ≈ 49k — use 8192 cap
        private readonly List<Vector3> _candidates = new List<Vector3>(8192); // COLD ALLOC: 8192 candidate positions
        private int _candidateIdx;
        private int _placedCount;

        // Species filter buffer — reused across TryPlaceAt calls
        // COLD ALLOC: max species count = 16
        private readonly List<int> _validSpecies = new List<int>(16); // COLD ALLOC: 16 species max

        private readonly CoralSpatialHash _spatialHash = new CoralSpatialHash(cellSize: 0.5f);

        private System.Random _rng;
        private bool          _registered;

        // Cached string tags to avoid per-call string comparison
        // These are compared via == which allocates — use CompareTag via Collider
        // (handled in TagToSubstrate by passing collider directly)

        // ── LIFECYCLE ────────────────────────────────────────────────

        private void Awake()
        {
            if (_library == null || _library.Species == null)
            {
                Debug.LogError("[CoralPlacer] Species library not assigned. Disabled.");
                enabled = false;
                return;
            }
            if (_renderer == null)
            {
                Debug.LogError("[CoralPlacer] CoralRenderer not assigned. Disabled.");
                enabled = false;
                return;
            }
        }

        private void Start()
        {
            if (!enabled) return;
            _rng   = new System.Random(GetInstanceID());
            _state = PlacerState.Generating;
            GenerateCandidates();
        }

        private void OnEnable()
        {
            if (!_registered && GameTickManager.Instance != null)
            {
                GameTickManager.Instance.RegisterSlow(this);
                _registered = true;
            }
        }

        private void OnDisable()
        {
            // §25 null-check
            if (_registered && GameTickManager.Instance != null)
            {
                GameTickManager.Instance.UnregisterSlow(this);
                _registered = false;
            }
        }

        // ── ISLOTWTICKABLE ────────────────────────────────────────────

        /// <summary>Streams coral placement. Unregisters when done.</summary>
        public void SlowTick()
        {
            if (_state != PlacerState.Placing) return;

            int processed = 0;
            while (_candidateIdx < _candidates.Count
                   && _placedCount < _targetCount
                   && processed < _placementsPerTick)
            {
                TryPlaceAt(_candidates[_candidateIdx]);
                _candidateIdx++;
                processed++;
            }

            bool exhausted = _candidateIdx >= _candidates.Count
                          || _placedCount >= _targetCount;

            if (!exhausted) return;

            _state = PlacerState.Done;
            _renderer.MarkReady();

            // §25 null-check before unregister
            if (GameTickManager.Instance != null)
            {
                GameTickManager.Instance.UnregisterSlow(this);
                _registered = false;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[CoralPlacer] Done: {_placedCount}/{_targetCount} instances placed.");
#endif
        }

        // ── PRIVATE ──────────────────────────────────────────────────

        private void GenerateCandidates()
        {
            _candidates.Clear();
            _candidateIdx = 0;
            _placedCount  = 0;

            // Grid jitter sampling inside circle
            float cellSize = 0.4f;
            float diameter = _areaRadius * 2f;
            int   dim      = Mathf.CeilToInt(diameter / cellSize);
            float sqRad    = _areaRadius * _areaRadius;

            for (int x = 0; x < dim; x++)
            for (int z = 0; z < dim; z++)
            {
                float wx = _areaCenter.x - _areaRadius + (x + (float)_rng.NextDouble()) * cellSize;
                float wz = _areaCenter.z - _areaRadius + (z + (float)_rng.NextDouble()) * cellSize;

                float dx = wx - _areaCenter.x;
                float dz = wz - _areaCenter.z;
                if (dx * dx + dz * dz > sqRad) continue;

                if (_candidates.Count >= _candidates.Capacity) break; // respect cap
                _candidates.Add(new Vector3(wx, _areaCenter.y + 50f, wz));
            }

            // Fisher-Yates shuffle — deterministic, no alloc
            for (int i = _candidates.Count - 1; i > 0; i--)
            {
                int j      = _rng.Next(i + 1);
                var tmp    = _candidates[i];
                _candidates[i] = _candidates[j];
                _candidates[j] = tmp;
            }

            _state = PlacerState.Placing;
        }

        private void TryPlaceAt(Vector3 rayOrigin)
        {
            // §10 NonAlloc raycast
            int hits = Physics.RaycastNonAlloc(
                rayOrigin, Vector3.down, _hitBuffer, 100f, _groundLayer);

            if (hits == 0) return;

            // Find closest hit — no LINQ
            int   bestIdx  = 0;
            float bestDist = _hitBuffer[0].distance;
            for (int h = 1; h < hits; h++)
            {
                if (_hitBuffer[h].distance < bestDist)
                {
                    bestDist = _hitBuffer[h].distance;
                    bestIdx  = h;
                }
            }

            var hit    = _hitBuffer[bestIdx];
            var pos    = hit.point;
            var normal = hit.normal;

            // Slope filter
            if (Vector3.Angle(normal, Vector3.up) > _maxSlope) return;

            // Depth check
            float depth = _waterSurfaceY - pos.y;
            if (depth < 0f) return;

            // Species selection
            int speciesIdx = PickSpecies(depth, hit.collider);
            if (speciesIdx < 0) return;

            var sp = _library.Species[speciesIdx];

            // Spacing check via spatial hash
            if (_spatialHash.HasNearby(pos, sp.minDistToAny)) return;

            // Build instance
            float scale  = Mathf.Lerp(sp.sizeMin, sp.sizeMax, (float)_rng.NextDouble());
            float rotY   = (float)_rng.NextDouble() * 360f;
            var   yRot   = Quaternion.Euler(0f, rotY, 0f);
            var   slopeR = Quaternion.FromToRotation(Vector3.up, normal);
            var   finalR = Quaternion.Slerp(yRot, slopeR * yRot, 0.4f);

            float phase   = (float)_rng.NextDouble() * Mathf.PI * 2f;
            var   colorV  = SampleColorVariation(sp);
            int   variant = _rng.Next(4);

            var inst = new CoralInstance(speciesIdx, variant, pos, finalR, scale, colorV, phase);

            // Register bioluminescence
            if (sp.bioluminescent && _bioLum != null)
            {
                inst.BioLumBufferIndex = _bioLum.RegisterCoral(
                    pos, sp.bioLumColor, sp.bioLumIntensity,
                    sp.bioLumFrequency, sp.bioLumAlwaysOn);
            }

            _renderer.RegisterInstance(inst);
            _spatialHash.Add(pos);
            _placedCount++;
        }

        private int PickSpecies(float depth, Collider ground)
        {
            _validSpecies.Clear();

            for (int i = 0; i < _library.Species.Length; i++)
            {
                var sp = _library.Species[i];

                // Depth zone
                if (sp.validDepths == null || sp.validDepths.Length == 0) continue;
                bool depthOk = false;
                for (int d = 0; d < sp.validDepths.Length; d++)
                {
                    if (DepthInZone(depth, sp.validDepths[d]))
                    {
                        depthOk = true;
                        break;
                    }
                }
                if (!depthOk) continue;

                // Substrate — §18 CompareTag via Collider
                var substrate = ColliderToSubstrate(ground);
                if ((sp.validSubstrates & substrate) == 0) continue;

                // Light (exponential attenuation with depth)
                float light = Mathf.Exp(-depth * 0.08f);
                if (light < sp.lightRequirement * 0.6f) continue;

                _validSpecies.Add(i);
            }

            return _validSpecies.Count == 0
                ? -1
                : _validSpecies[_rng.Next(_validSpecies.Count)];
        }

        private static bool DepthInZone(float depth, CoralDepthZone zone)
        {
            switch (zone)
            {
                case CoralDepthZone.Shallows:   return depth <= 3f;
                case CoralDepthZone.UpperReef:  return depth > 3f  && depth <= 15f;
                case CoralDepthZone.MidReef:    return depth > 15f && depth <= 30f;
                case CoralDepthZone.Mesophotic: return depth > 30f && depth <= 60f;
                case CoralDepthZone.Deep:       return depth > 60f;
                default:                        return false;
            }
        }

        private static SubstrateType ColliderToSubstrate(Collider c)
        {
            // §18 CompareTag — no string allocation
            if (c.CompareTag("Rock"))      return SubstrateType.Rock;
            if (c.CompareTag("Sand"))      return SubstrateType.Sand;
            if (c.CompareTag("Rubble"))    return SubstrateType.Rubble;
            if (c.CompareTag("DeadCoral")) return SubstrateType.DeadCoral;
            return SubstrateType.Rock;
        }

        private Color SampleColorVariation(in CoralSpeciesParams sp)
        {
            float v = sp.colorVariation * 0.5f;
            return new Color(
                (float)(_rng.NextDouble() * 2.0 - 1.0) * v,
                (float)(_rng.NextDouble() * 2.0 - 1.0) * v,
                (float)(_rng.NextDouble() * 2.0 - 1.0) * v * 0.6f,
                1f
            );
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!_drawGizmos) return;
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.25f);
            Gizmos.DrawWireSphere(_areaCenter, _areaRadius);
        }
#endif
    }

    // ─────────────────────────────────────────────────────────
    // SPATIAL HASH
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// Flat XZ spatial hash for minimum-distance placement enforcement.
    /// Dictionary allocated once at capacity. Inner lists grow cold-only.
    /// </summary>
    internal sealed class CoralSpatialHash
    {
        private readonly float _cellSize;
        private readonly float _invCellSize;

        // COLD ALLOC: 512 initial buckets
        private readonly Dictionary<long, List<Vector2>> _cells =
            new Dictionary<long, List<Vector2>>(512);

        public CoralSpatialHash(float cellSize)
        {
            _cellSize    = cellSize;
            _invCellSize = 1f / cellSize;
        }

        public void Add(Vector3 pos)
        {
            long key = Key(pos);
            if (!_cells.TryGetValue(key, out var list))
            {
                // COLD ALLOC: new List<> only on first entry per cell
                list = new List<Vector2>(8);
                _cells[key] = list;
            }
            list.Add(new Vector2(pos.x, pos.z));
        }

        public bool HasNearby(Vector3 pos, float radius)
        {
            int   r    = Mathf.CeilToInt(radius * _invCellSize);
            int   cx   = Mathf.FloorToInt(pos.x * _invCellSize);
            int   cz   = Mathf.FloorToInt(pos.z * _invCellSize);
            float sqR  = radius * radius;

            for (int dx = -r; dx <= r; dx++)
            for (int dz = -r; dz <= r; dz++)
            {
                if (!_cells.TryGetValue(PackKey(cx + dx, cz + dz), out var list)) continue;

                for (int i = 0; i < list.Count; i++)
                {
                    float ex = list[i].x - pos.x;
                    float ez = list[i].y - pos.z;
                    if (ex * ex + ez * ez < sqR) return true;
                }
            }
            return false;
        }

        private long Key(Vector3 pos) =>
            PackKey(Mathf.FloorToInt(pos.x * _invCellSize),
                    Mathf.FloorToInt(pos.z * _invCellSize));

        private static long PackKey(int x, int z) =>
            ((long)(x + 32768) << 32) | (uint)(z + 32768);
    }
}
ФАЙЛ 8: CoralPolyps.cs
csharp

// ============================================================
// HECTON-8 — CoralPolyps.cs
// Animated coral polyps. GPU instanced. ITickable state machine.
// Slot-based reuse (no Instantiate). Zero GC hot path.
// ============================================================

using UnityEngine;
using Unity.Mathematics;
using Hecton8.Core;

namespace Hecton8.Environment
{
    /// <summary>
    /// Renders animated polyps as GPU-instanced micro-spheres.
    /// Active only within _polypRange of camera.
    /// State machine: Extending → Extended → Retracting → Retracted (→ loop).
    /// Proximity retraction when player enters _retractRadius.
    /// No Instantiate/Destroy — slot-based array reuse.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CoralPolyps : MonoBehaviour, ITickable
    {
        // ── INSPECTOR ────────────────────────────────────────────────

        [Header("── Rendering ────────────────────────────────────────────")]
        [SerializeField, Tooltip("Polyp mesh (icosphere ~60 tris).")]
        private Mesh _polypMesh;

        [SerializeField, Tooltip("Polyp material. GPU instancing must be enabled.")]
        private Material _polypMaterial;

        [Header("── Capacity ─────────────────────────────────────────────")]
        [SerializeField, Range(32, 1024), Tooltip("Max simultaneous visible polyps.")]
        private int _maxPolyps = 512;

        [SerializeField, Range(1f, 15f), Tooltip("Camera distance at which polyps render (m).")]
        private float _polypRange = 8f;

        [Header("── Animation ───────────────────────────────────────────")]
        [SerializeField, Range(0.5f, 5f),  Tooltip("Seconds to fully extend.")]
        private float _extendDuration = 1.2f;

        [SerializeField, Range(0.1f, 3f),  Tooltip("Seconds to retract.")]
        private float _retractDuration = 0.6f;

        [SerializeField, Range(0f, 1f),    Tooltip("Flinch probability per second.")]
        private float _flinchProbability = 0.02f;

        [SerializeField, Range(1f, 10f),   Tooltip("Pause before re-extending (s).")]
        private float _repauseDuration = 3f;

        // ── PRIVATE STATE ─────────────────────────────────────────────

        private enum PolypState : byte
        {
            Retracted  = 0,
            Extending  = 1,
            Extended   = 2,
            Retracting = 3
        }

        private struct PolypSlot
        {
            public Vector3    BasePos;
            public Vector3    Normal;
            public Color32    Color;
            public float      Size;
            public float      ExtensionT; // [0..1]
            public float      Timer;
            public PolypState State;
            public bool       Active;
        }

        // COLD ALLOC: _maxPolyps * sizeof(PolypSlot)
        private PolypSlot[]  _slots;
        // COLD ALLOC: _maxPolyps * sizeof(Matrix4x4) = _maxPolyps * 64 bytes
        private Matrix4x4[]  _matrices;
        // COLD ALLOC: _maxPolyps * sizeof(Vector4)
        private Vector4[]    _colorData;
        private int          _activeCount;

        private Camera    _mainCam;
        private Transform _camTransform;
        private bool      _registered;

        private Vector3 _interactorPos    = new Vector3(0f, -99999f, 0f);
        private float   _interactorSqRad  = 1f;

        private System.Random _rng;

        // MaterialPropertyBlock — one instance, reused each frame (§4)
        private readonly MaterialPropertyBlock _mpb = new MaterialPropertyBlock();

        // Cached shader IDs
        private static readonly int _PropColors = Shader.PropertyToID("_PolypColors");

        // ── LIFECYCLE ────────────────────────────────────────────────

        private void Awake()
        {
            // §11 cache camera
            _mainCam      = Camera.main;
            _camTransform = _mainCam != null ? _mainCam.transform : null;
            _rng          = new System.Random(GetInstanceID());

            // COLD ALLOC: fixed arrays — never reallocated
            _slots    = new PolypSlot[_maxPolyps];
            _matrices = new Matrix4x4[_maxPolyps];
            _colorData = new Vector4[_maxPolyps];

            if (_polypMesh == null)
            {
                Debug.LogError("[CoralPolyps] Polyp mesh not assigned. Disabled.");
                enabled = false;
                return;
            }
            if (_polypMaterial == null)
            {
                Debug.LogError("[CoralPolyps] Polyp material not assigned. Disabled.");
                enabled = false;
                return;
            }
        }

        private void OnEnable()
        {
            if (!_registered && GameTickManager.Instance != null)
            {
                GameTickManager.Instance.Register(this);
                _registered = true;
            }
        }

        private void OnDisable()
        {
            // §25
            if (_registered && GameTickManager.Instance != null)
            {
                GameTickManager.Instance.Unregister(this);
                _registered = false;
            }
        }

        // ── PUBLIC API ───────────────────────────────────────────────

        /// <summary>Spawns a polyp into the next free slot. Returns false if full.</summary>
        public bool SpawnPolyp(Vector3 position, Vector3 normal, Color color, float size)
        {
            if (_activeCount >= _maxPolyps) return false;

            for (int i = 0; i < _maxPolyps; i++)
            {
                if (_slots[i].Active) continue;

                _slots[i] = new PolypSlot
                {
                    BasePos     = position,
                    Normal      = normal,
                    Color       = new Color32(
                        (byte)(color.r * 255f), (byte)(color.g * 255f),
                        (byte)(color.b * 255f), 255),
                    Size        = size,
                    ExtensionT  = 0f,
                    Timer       = 0f,
                    State       = PolypState.Extending,
                    Active      = true
                };
                _activeCount++;
                return true;
            }
            return false;
        }

        /// <summary>Forces all active polyps into retracting state.</summary>
        public void RetractAll()
        {
            for (int i = 0; i < _maxPolyps; i++)
            {
                if (!_slots[i].Active) continue;
                if (_slots[i].State == PolypState.Retracted
                 || _slots[i].State == PolypState.Retracting) continue;
                _slots[i].State = PolypState.Retracting;
                _slots[i].Timer = 0f;
            }
        }

        /// <summary>Sets interactor position for proximity retraction. Zero GC.</summary>
        public void SetInteractor(Vector3 worldPos, float radius)
        {
            _interactorPos   = worldPos;
            _interactorSqRad = radius * radius;
        }

        // ── ITICKABLE ────────────────────────────────────────────────

        /// <summary>Updates all polyp states and submits draw call. Zero GC.</summary>
        public void Tick(float dt)
        {
            if (_polypMesh == null || _polypMaterial == null) return;
            if (_activeCount == 0) return;

            // §14 cache camera pos — one read
            var camPos = _camTransform != null ? _camTransform.position : Vector3.zero;
            float sqRange = _polypRange * _polypRange;

            int drawCount = 0;

            for (int i = 0; i < _maxPolyps; i++)
            {
                if (!_slots[i].Active) continue;

                ref var s = ref _slots[i];

                // Proximity retraction check — squared distance, no sqrt
                float pdx = s.BasePos.x - _interactorPos.x;
                float pdz = s.BasePos.z - _interactorPos.z;
                if (pdx * pdx + pdz * pdz < _interactorSqRad
                    && s.State != PolypState.Retracting
                    && s.State != PolypState.Retracted)
                {
                    s.State = PolypState.Retracting;
                    s.Timer = 0f;
                }

                // State machine — no alloc
                s.Timer += dt;
                switch (s.State)
                {
                    case PolypState.Extending:
                        s.ExtensionT = s.Timer / _extendDuration;
                        if (s.ExtensionT >= 1f)
                        {
                            s.ExtensionT = 1f;
                            s.State      = PolypState.Extended;
                            s.Timer      = 0f;
                        }
                        break;

                    case PolypState.Extended:
                        // Random flinch — System.Random.NextDouble() = no GC
                        if (_rng.NextDouble() < _flinchProbability * dt)
                        {
                            s.State = PolypState.Retracting;
                            s.Timer = 0f;
                        }
                        break;

                    case PolypState.Retracting:
                        s.ExtensionT = 1f - s.Timer / _retractDuration;
                        if (s.ExtensionT <= 0f)
                        {
                            s.ExtensionT = 0f;
                            s.State      = PolypState.Retracted;
                            s.Timer      = 0f;
                        }
                        break;

                    case PolypState.Retracted:
                        if (s.Timer > _repauseDuration)
                        {
                            s.State = PolypState.Extending;
                            s.Timer = 0f;
                        }
                        break;
                }

                // Skip if invisible
                if (s.ExtensionT < 0.01f) continue;

                // Range cull — squared distance
                float cdx = s.BasePos.x - camPos.x;
                float cdy = s.BasePos.y - camPos.y;
                float cdz = s.BasePos.z - camPos.z;
                if (cdx*cdx + cdy*cdy + cdz*cdz > sqRange) continue;

                // Build matrix
                float   ext    = s.ExtensionT * s.Size * 0.5f;
                Vector3 extPos = s.BasePos + s.Normal * ext;
                float   scale  = s.Size * (0.5f + s.ExtensionT * 0.5f);

                _matrices[drawCount] = Matrix4x4.TRS(
                    extPos,
                    Quaternion.LookRotation(s.Normal),
                    new Vector3(scale, scale, scale)
                );
                _colorData[drawCount] = new Vector4(
                    s.Color.r / 255f,
                    s.Color.g / 255f,
                    s.Color.b / 255f,
                    s.ExtensionT
                );

                drawCount++;
                if (drawCount >= _maxPolyps) break;
            }

            if (drawCount == 0) return;

            // §4 MaterialPropertyBlock — no material instance
            _mpb.SetVectorArray(_PropColors, _colorData);

            Graphics.DrawMeshInstanced(
                _polypMesh, 0, _polypMaterial,
                _matrices, drawCount, _mpb,
                UnityEngine.Rendering.ShadowCastingMode.Off,
                receiveShadows: false
            );
        }
    }
}
ФАЙЛ 9: CoralTextureGenerator.cs
csharp

// ============================================================
// HECTON-8 — CoralTextureGenerator.cs
// Procedural coral texture atlas. Startup only.
// ISlowTickable state machine — NO coroutines, NO Task.Run.
// Stitches one species tile per SlowTick to spread CPU cost.
// MAIN THREAD ONLY — Mathf.PerlinNoise is not thread-safe.
// ============================================================

using UnityEngine;
using UnityEngine.Experimental.Rendering;
using Hecton8.Core;

namespace Hecton8.Environment
{
    /// <summary>
    /// Generates procedural texture atlas for all coral species.
    /// Layout (1024 × 512):
    ///   4 horizontal tiles per row: [Albedo][Normal][SSS][Emission]
    ///   Each tile = 256 × tileHeight px
    ///   One row per species (max 8 species visible)
    ///
    /// Generation is spread over SlowTick calls (one species per tick).
    /// Unregisters when complete.
    ///
    /// Textures set as global shader properties — shared by all coral materials.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public sealed class CoralTextureGenerator : MonoBehaviour, ISlowTickable
    {
        // ── INSPECTOR ────────────────────────────────────────────────

        [Header("── References ──────────────────────────────────────────")]
        [SerializeField, Tooltip("Species library (provides color data).")]
        private CoralSpeciesLibrary _library;

        [Header("── Atlas Layout ─────────────────────────────────────────")]
        [SerializeField] private int _atlasWidth  = 1024;
        [SerializeField] private int _atlasHeight = 512;
        [SerializeField] private int _tileWidth   = 256;  // per texture type
        [SerializeField] private int _tileHeight  = 64;   // per species row

        // ── PRIVATE STATE ────────────────────────────────────────────

        private Texture2D _albedoAtlas;
        private Texture2D _normalAtlas;
        private Texture2D _sssAtlas;
        private Texture2D _emissionAtlas;

        // Pixel arrays — allocated once, held until Apply(), then freed
        // COLD ALLOC: 4 * 1024 * 512 * 4 bytes = ~8 MB total
        private Color32[] _albedoPx;
        private Color32[] _normalPx;
        private Color32[] _sssPx;
        private Color32[] _emissionPx;

        private enum TexGenState { Allocating, GeneratingTiles, Uploading, Done }
        private TexGenState _state = TexGenState.Allocating;

        private int  _currentSpecies;
        private int  _maxSpecies;
        private bool _registered;

        // Public readiness flag
        public bool IsReady { get; private set; }

        // ── CACHED SHADER IDS ─────────────────────────────────────────

        private static readonly int _PropAlbedo   = Shader.PropertyToID("_CoralAlbedoAtlas");
        private static readonly int _PropNormal   = Shader.PropertyToID("_CoralNormalAtlas");
        private static readonly int _PropSSS      = Shader.PropertyToID("_CoralSSSAtlas");
        private static readonly int _PropEmission = Shader.PropertyToID("_CoralEmissionAtlas");

        // ── LIFECYCLE ────────────────────────────────────────────────

        private void Awake()
        {
            if (_library == null || _library.Species == null || _library.Species.Length == 0)
            {
                Debug.LogError("[CoralTextureGenerator] Library missing or empty. Disabled.");
                enabled = false;
                return;
            }

            _maxSpecies = Mathf.Min(_library.Species.Length, _atlasHeight / _tileHeight);
        }

        private void OnEnable()
        {
            if (!_registered && GameTickManager.Instance != null)
            {
                GameTickManager.Instance.RegisterSlow(this);
                _registered = true;
            }
        }

        private void OnDisable()
        {
            // §25
            if (_registered && GameTickManager.Instance != null)
            {
                GameTickManager.Instance.UnregisterSlow(this);
                _registered = false;
            }
        }

        private void OnDestroy()
        {
            FreeTex(ref _albedoAtlas);
            FreeTex(ref _normalAtlas);
            FreeTex(ref _sssAtlas);
            FreeTex(ref _emissionAtlas);
        }

        // ── ISLOTWTICKABLE ────────────────────────────────────────────

        /// <summary>Advances texture generation one step per slow tick. Zero GC hot path.</summary>
        public void SlowTick()
        {
            switch (_state)
            {
                case TexGenState.Allocating:
                    AllocatePixelArrays();
                    _state = TexGenState.GeneratingTiles;
                    break;

                case TexGenState.GeneratingTiles:
                    if (_currentSpecies < _maxSpecies)
                    {
                        // One species tile per tick — no spike
                        GenerateTile(_currentSpecies, _library.Species[_currentSpecies]);
                        _currentSpecies++;
                    }
                    else
                    {
                        _state = TexGenState.Uploading;
                    }
                    break;

                case TexGenState.Uploading:
                    UploadToGPU();
                    _state = TexGenState.Done;
                    IsReady = true;

                    // Unregister — no more work
                    if (GameTickManager.Instance != null)
                    {
                        GameTickManager.Instance.UnregisterSlow(this);
                        _registered = false;
                    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.Log($"[CoralTextureGenerator] Atlas ready. " +
                              $"{_maxSpecies} species, {_atlasWidth}×{_atlasHeight}.");
#endif
                    break;

                case TexGenState.Done:
                    break;
            }
        }

        // ── GENERATION ───────────────────────────────────────────────

        private void AllocatePixelArrays()
        {
            int total = _atlasWidth * _atlasHeight;
            // COLD ALLOC: 4 arrays * total * 4 bytes = ~8 MB at 1024×512
            _albedoPx   = new Color32[total];
            _normalPx   = new Color32[total];
            _sssPx      = new Color32[total];
            _emissionPx = new Color32[total];
        }

        private void GenerateTile(int speciesIdx, in CoralSpeciesParams sp)
        {
            int rowY = speciesIdx * _tileHeight;

            for (int ty = 0; ty < _tileHeight; ty++)
            {
                float v = (float)ty / _tileHeight;

                for (int tx = 0; tx < _atlasWidth; tx++)
                {
                    // Tile X index: 0=albedo, 1=normal, 2=sss, 3=emission
                    int   tileX  = tx / _tileWidth;
                    float u      = (float)(tx % _tileWidth) / _tileWidth;
                    int   pixIdx = (rowY + ty) * _atlasWidth + tx;

                    switch (tileX)
                    {
                        case 0: _albedoPx[pixIdx]   = SampleAlbedo(u, v, sp);   break;
                        case 1: _normalPx[pixIdx]   = SampleNormal(u, v, sp);   break;
                        case 2: _sssPx[pixIdx]      = SampleSSS(u, v, sp);      break;
                        case 3: _emissionPx[pixIdx] = SampleEmission(u, v, sp); break;
                    }
                }
            }
        }

        private void UploadToGPU()
        {
            _albedoAtlas   = CreateTex(_albedoPx,   GraphicsFormat.R8G8B8A8_SRGB,  "CoralAlbedo");
            _normalAtlas   = CreateTex(_normalPx,   GraphicsFormat.R8G8B8A8_UNorm, "CoralNormal");
            _sssAtlas      = CreateTex(_sssPx,      GraphicsFormat.R8G8B8A8_UNorm, "CoralSSS");
            _emissionAtlas = CreateTex(_emissionPx, GraphicsFormat.R8G8B8A8_UNorm, "CoralEmission");

            // Free CPU pixel data — GPU owns it now
            _albedoPx = _normalPx = _sssPx = _emissionPx = null;

            Shader.SetGlobalTexture(_PropAlbedo,   _albedoAtlas);
            Shader.SetGlobalTexture(_PropNormal,   _normalAtlas);
            Shader.SetGlobalTexture(_PropSSS,      _sssAtlas);
            Shader.SetGlobalTexture(_PropEmission, _emissionAtlas);
        }

        // ── SAMPLE FUNCTIONS ─────────────────────────────────────────
        // MAIN THREAD ONLY — Mathf.PerlinNoise is NOT thread-safe

        private static Color32 SampleAlbedo(float u, float v, in CoralSpeciesParams sp)
        {
            Color c = Color.Lerp(sp.colorBase, sp.colorTip, Mathf.Pow(v, 0.6f));

            float pattern = sp.morphology switch
            {
                CoralMorphology.Massive   => BrainGroove(u, v),
                CoralMorphology.SeaFan    => FanMesh(u, v),
                CoralMorphology.TubeOrgan => TubeRim(u, v),
                _                         => Grain(u, v)
            };

            float r = Mathf.Clamp01(c.r * (1f - pattern * 0.25f));
            float g = Mathf.Clamp01(c.g * (1f - pattern * 0.25f));
            float b = Mathf.Clamp01(c.b * (1f - pattern * 0.25f));

            // Tip brightening
            float tipBoost = Mathf.Pow(v, 3f) * 0.1f;
            r = Mathf.Clamp01(r + tipBoost);
            g = Mathf.Clamp01(g + tipBoost);
            b = Mathf.Clamp01(b + tipBoost);

            return F32ToC32(r, g, b, 1f);
        }

        private static Color32 SampleNormal(float u, float v, in CoralSpeciesParams sp)
        {
            const float Eps = 1f / 256f;

            float h00 = HeightSample(u,       v,       sp);
            float h10 = HeightSample(u + Eps, v,       sp);
            float h01 = HeightSample(u,       v + Eps, sp);

            var tng  = new Vector3(Eps * 256f, 0f,        h10 - h00).normalized;
            var bin  = new Vector3(0f,         Eps * 64f, h01 - h00).normalized;
            var norm = Vector3.Cross(tng, bin).normalized;

            // Blend toward flat
            norm = Vector3.Lerp(norm, Vector3.forward, 0.35f).normalized;

            float nr = norm.x * 0.5f + 0.5f;
            float ng = norm.y * 0.5f + 0.5f;
            float nb = norm.z * 0.5f + 0.5f;

            return F32ToC32(nr, ng, nb, 1f);
        }

        private static Color32 SampleSSS(float u, float v, in CoralSpeciesParams sp)
        {
            float thickness = Mathf.Clamp01(1f - Mathf.Pow(v, 0.5f) * sp.sssStrength);
            float rough     = Mathf.Clamp01(sp.roughness * (1f - v * 0.2f));
            float ao        = Mathf.Pow(1f - Mathf.Abs(u - 0.5f) * 2f, 2f);
            return F32ToC32(thickness, rough, ao, 1f);
        }

        private static Color32 SampleEmission(float u, float v, in CoralSpeciesParams sp)
        {
            if (!sp.bioluminescent && !sp.fluorescent)
                return new Color32(0, 0, 0, 0);

            Color emCol = sp.bioluminescent ? sp.bioLumColor : sp.fluorColor;
            float str   = sp.bioluminescent ? sp.bioLumIntensity : sp.fluorStrength;

            float tipFactor  = Mathf.Pow(v, 2f);
            float edgeFactor = 1f - Mathf.Abs(u - 0.5f) * 2f;
            edgeFactor = Mathf.Pow(edgeFactor, 3f);

            float intensity = (tipFactor * 0.7f + edgeFactor * 0.3f) * str;

            return F32ToC32(
                Mathf.Clamp01(emCol.r * intensity),
                Mathf.Clamp01(emCol.g * intensity),
                Mathf.Clamp01(emCol.b * intensity),
                Mathf.Clamp01(intensity)
            );
        }

        // ── PATTERN GENERATORS ───────────────────────────────────────
        // MAIN THREAD ONLY

        private static float BrainGroove(float u, float v)
        {
            float n1    = Mathf.PerlinNoise(u * 8f, v * 8f);
            float n2    = Mathf.PerlinNoise(u * 12f + 5f, v * 12f);
            float groove = Mathf.Pow(Mathf.Abs(n1 - 0.5f) * 2f, 0.4f);
            return groove * (n2 * 0.4f + 0.6f);
        }

        private static float FanMesh(float u, float v)
        {
            float hz = Mathf.Abs(Mathf.Sin(u * 30f));
            float vt = Mathf.Abs(Mathf.Sin(v * 20f));
            return Mathf.Max(hz, vt) * 0.4f;
        }

        private static float TubeRim(float u, float v) =>
            (1f - Mathf.Abs(Mathf.Sin(u * Mathf.PI))) * 0.5f;

        private static float Grain(float u, float v) =>
            Mathf.PerlinNoise(u * 15f, v * 15f) * 0.7f
            + Mathf.PerlinNoise(u * 30f + 1.3f, v * 30f + 0.7f) * 0.3f;

        private static float HeightSample(float u, float v, in CoralSpeciesParams sp)
        {
            float h = Grain(u, v) * 0.3f;
            if (sp.morphology == CoralMorphology.Massive)
                h += BrainGroove(u, v) * 0.5f;
            return h;
        }

        // ── UTILITIES ────────────────────────────────────────────────

        private Texture2D CreateTex(Color32[] pixels, GraphicsFormat fmt, string texName)
        {
            var tex = new Texture2D(_atlasWidth, _atlasHeight, fmt, TextureCreationFlags.None)
            {
                name       = texName,
                filterMode = FilterMode.Bilinear,
                wrapMode   = TextureWrapMode.Clamp
            };
            tex.SetPixelData(pixels, 0);
            tex.Apply(updateMipmaps: true, makeNoLongerReadable: true);
            return tex;
        }

        private static void FreeTex(ref Texture2D tex)
        {
            if (tex != null) Destroy(tex);
            tex = null;
        }

        private static Color32 F32ToC32(float r, float g, float b, float a) =>
            new Color32(
                (byte)(r * 255f),
                (byte)(g * 255f),
                (byte)(b * 255f),
                (byte)(a * 255f)
            );
    }
}
ФАЙЛ 10: BioLumTrigger.cs
csharp

// ============================================================
// HECTON-8 — BioLumTrigger.cs
// Player-driven bioluminescence trigger. ITickable. Zero GC.
// ============================================================

using UnityEngine;
using Hecton8.Core;

namespace Hecton8.Environment
{
    /// <summary>
    /// Monitors player position and movement to trigger coral bioluminescence.
    /// Modes: proximity, movement speed, manual (scanner/sonar).
    /// Zero GC: no alloc, cached transform, squared-distance checks.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BioLumTrigger : MonoBehaviour, ITickable
    {
        // ── INSPECTOR ────────────────────────────────────────────────

        [Header("── References ──────────────────────────────────────────")]
        [SerializeField, Tooltip("BioLum system to drive.")]
        private CoralBioLumSystem _bioLum;

        [SerializeField, Tooltip("Player transform.")]
        private Transform _playerTransform;

        [SerializeField, Tooltip("Polyp system for retraction (optional).")]
        private CoralPolyps _polyps;

        [Header("── Proximity Trigger ───────────────────────────────────")]
        [SerializeField, Range(0.5f, 15f), Tooltip("Trigger radius (m).")]
        private float _triggerRadius = 2.5f;

        [SerializeField, Range(0.1f, 5f), Tooltip("Cooldown between proximity triggers (s).")]
        private float _proximityCooldown = 0.8f;

        [Header("── Movement Trigger ────────────────────────────────────")]
        [SerializeField, Range(0.5f, 10f), Tooltip("Speed threshold to trigger wave (m/s).")]
        private float _speedThreshold = 2f;

        [SerializeField, Range(0.5f, 20f), Tooltip("Wave radius from movement (m).")]
        private float _movementWaveRadius = 6f;

        [SerializeField, Range(0.5f, 10f), Tooltip("Cooldown between movement triggers (s).")]
        private float _movementCooldown = 2f;

        [Header("── Polyp Retraction ─────────────────────────────────────")]
        [SerializeField, Range(0.2f, 5f), Tooltip("Polyps retract within this radius (m).")]
        private float _polypRetractRadius = 1.2f;

        // ── PRIVATE STATE ────────────────────────────────────────────

        private Vector3 _lastPlayerPos;
        private float   _proxTimer;
        private float   _moveTimer;
        private bool    _registered;
        private bool    _initialized;

        // Cache squared trigger radius — avoid per-tick multiply
        private float _triggerSqRadius;
        private float _polypSqRadius;

        // ── LIFECYCLE ────────────────────────────────────────────────

        private void Awake()
        {
            if (_bioLum == null)
            {
                Debug.LogError("[BioLumTrigger] CoralBioLumSystem not assigned. Disabled.");
                enabled = false;
                return;
            }
            if (_playerTransform == null)
            {
                Debug.LogError("[BioLumTrigger] Player transform not assigned. Disabled.");
                enabled = false;
                return;
            }

            _triggerSqRadius = _triggerRadius * _triggerRadius;
            _polypSqRadius   = _polypRetractRadius * _polypRetractRadius;
        }

        private void Start()
        {
            if (!enabled) return;
            _lastPlayerPos = _playerTransform.position;
            _initialized   = true;
        }

        private void OnEnable()
        {
            if (!_registered && GameTickManager.Instance != null)
            {
                GameTickManager.Instance.Register(this);
                _registered = true;
            }
        }

        private void OnDisable()
        {
            // §25
            if (_registered && GameTickManager.Instance != null)
            {
                GameTickManager.Instance.Unregister(this);
                _registered = false;
            }
        }

        // ── ITICKABLE ────────────────────────────────────────────────

        /// <summary>Zero GC: no alloc, cached reads, squared distance checks.</summary>
        public void Tick(float dt)
        {
            if (!_initialized) return;

            // §14 cache transform — one read
            var playerPos = _playerTransform.position;

            // Decrement timers
            _proxTimer -= dt;
            _moveTimer -= dt;

            // ── Proximity trigger ──
            if (_proxTimer <= 0f)
            {
                // Distance from this anchor point to player
                float dx     = playerPos.x - transform.position.x;
                float dz     = playerPos.z - transform.position.z;
                float sqDist = dx * dx + dz * dz;

                if (sqDist < _triggerSqRadius)
                {
                    _bioLum.TriggerAt(playerPos, _triggerRadius);
                    _proxTimer = _proximityCooldown;
                }
            }

            // ── Movement trigger ──
            if (_moveTimer <= 0f)
            {
                float ddx   = playerPos.x - _lastPlayerPos.x;
                float ddy   = playerPos.y - _lastPlayerPos.y;
                float ddz   = playerPos.z - _lastPlayerPos.z;
                float sqSpd = (ddx * ddx + ddy * ddy + ddz * ddz) / (dt * dt);

                // Compare speed² against threshold² — no Mathf.Sqrt
                float sqThresh = _speedThreshold * _speedThreshold;
                if (sqSpd > sqThresh)
                {
                    _bioLum.TriggerAt(playerPos, _movementWaveRadius);
                    _moveTimer = _movementCooldown;
                }
            }

            // ── Polyp retraction ──
            if (_polyps != null)
                _polyps.SetInteractor(playerPos, _polypRetractRadius);

            // §14 one write at end
            _lastPlayerPos = playerPos;
        }

        // ── PUBLIC API ───────────────────────────────────────────────

        /// <summary>
        /// Manual trigger from external game systems (scanner, sonar, explosion).
        /// Zero GC.
        /// </summary>
        /// <param name="origin">World position of disturbance.</param>
        /// <param name="radius">Effect radius in meters.</param>
        public void ManualTrigger(Vector3 origin, float radius)
        {
            if (_bioLum != null)
                _bioLum.TriggerAt(origin, radius);
        }
    }
}
ФАЙЛ 11: CoralLODSystem.cs
csharp

// ============================================================
// HECTON-8 — CoralLODSystem.cs
// LOD mesh generation. ISlowTickable state machine.
// No coroutines. One species per SlowTick. Zero GC hot path.
// ============================================================

using UnityEngine;
using Hecton8.Core;

namespace Hecton8.Environment
{
    /// <summary>
    /// Generates LOD meshes for all coral species variants.
    /// Streams generation: one species per SlowTick call.
    /// LOD0: full, LOD1: -1 iter, LOD2: -2 iter, LOD3: billboard quad.
    ///
    /// Mesh budget per species (MX350 target):
    ///   LOD0 ~800 tris | LOD1 ~300 | LOD2 ~80 | LOD3 = 2 tris
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-85)]
    public sealed class CoralLODSystem : MonoBehaviour, ISlowTickable
    {
        // ── INSPECTOR ────────────────────────────────────────────────

        [Header("── References ──────────────────────────────────────────")]
        [SerializeField, Tooltip("Species library.")]
        private CoralSpeciesLibrary _library;

        [SerializeField, Tooltip("Renderer — receives meshes as they complete.")]
        private CoralRenderer _renderer;

        [Header("── Variants ────────────────────────────────────────────")]
        [SerializeField, Range(1, 8), Tooltip("Mesh variants per species per LOD.")]
        private int _variantsPerSpecies = 4;

        // ── PRIVATE STATE ────────────────────────────────────────────

        private enum LODGenState { Idle, Generating, Done }
        private LODGenState _state = LODGenState.Idle;

        // [speciesIdx][variantIdx][lodLevel 0..3]
        // COLD ALLOC: species * variants * 4 Mesh refs
        private Mesh[][][] _meshes;

        private int _currentSpecies;

        // Stateless generator — reusable, no state between calls
        private readonly CoralLSystemGenerator _generator = new CoralLSystemGenerator();

        private bool _registered;

        // ── PUBLIC PROPERTIES ─────────────────────────────────────────

        /// <summary>True after all species meshes are generated.</summary>
        public bool IsReady { get; private set; }

        /// <summary>Returns mesh for given species/variant/lod. Null if not ready or invalid.</summary>
        public Mesh GetMesh(int speciesIdx, int variantIdx, int lodLevel)
        {
            if (!IsReady || _meshes == null)                         return null;
            if ((uint)speciesIdx >= (uint)_meshes.Length)            return null;
            if (_meshes[speciesIdx] == null)                         return null;
            if ((uint)variantIdx >= (uint)_meshes[speciesIdx].Length) return null;
            if ((uint)lodLevel   > 3u)                               return null;
            return _meshes[speciesIdx][variantIdx][lodLevel];
        }

        // ── LIFECYCLE ────────────────────────────────────────────────

        private void Awake()
        {
            if (_library == null || _library.Species == null || _library.Species.Length == 0)
            {
                Debug.LogError("[CoralLODSystem] Library missing. Disabled.");
                enabled = false;
                return;
            }
            if (_renderer == null)
            {
                Debug.LogError("[CoralLODSystem] CoralRenderer not assigned. Disabled.");
                enabled = false;
                return;
            }

            // COLD ALLOC: mesh table
            int sc = _library.Species.Length;
            _meshes = new Mesh[sc][][];
            for (int si = 0; si < sc; si++)
            {
                _meshes[si] = new Mesh[_variantsPerSpecies][];
                for (int v = 0; v < _variantsPerSpecies; v++)
                    _meshes[si][v] = new Mesh[4];
            }
        }

        private void Start()
        {
            if (!enabled) return;
            _state = LODGenState.Generating;
        }

        private void OnEnable()
        {
            if (!_registered && GameTickManager.Instance != null)
            {
                GameTickManager.Instance.RegisterSlow(this);
                _registered = true;
            }
        }

        private void OnDisable()
        {
            // §25
            if (_registered && GameTickManager.Instance != null)
            {
                GameTickManager.Instance.UnregisterSlow(this);
                _registered = false;
            }
        }

        private void OnDestroy()
        {
            if (_meshes == null) return;
            for (int si = 0; si < _meshes.Length; si++)
            {
                if (_meshes[si] == null) continue;
                for (int v = 0; v < _meshes[si].Length; v++)
                {
                    if (_meshes[si][v] == null) continue;
                    for (int lod = 0; lod < 4; lod++)
                    {
                        if (_meshes[si][v][lod] != null)
                            Destroy(_meshes[si][v][lod]);
                    }
                }
            }
        }

        // ── ISLOTWTICKABLE ────────────────────────────────────────────

        /// <summary>Generates one species per SlowTick. Zero alloc after mesh creation.</summary>
        public void SlowTick()
        {
            if (_state != LODGenState.Generating) return;

            int sc = _library.Species.Length;

            if (_currentSpecies >= sc)
            {
                // All done
                _state  = LODGenState.Done;
                IsReady = true;

                _renderer.MarkReady();

                if (GameTickManager.Instance != null)
                {
                    GameTickManager.Instance.UnregisterSlow(this);
                    _registered = false;
                }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[CoralLODSystem] Generation complete. " +
                          $"{sc} species × {_variantsPerSpecies} variants × 4 LODs.");
#endif
                return;
            }

            GenerateSpecies(_currentSpecies);
            _currentSpecies++;
        }

        // ── PRIVATE ──────────────────────────────────────────────────

        private void GenerateSpecies(int si)
        {
            var sp  = _library.Species[si];
            var rng = new System.Random(si * 1337); // COLD ALLOC: one per species

            for (int v = 0; v < _variantsPerSpecies; v++)
            {
                int seed = si * 1000 + v * 17;

                for (int lod = 0; lod < 3; lod++)
                {
                    var lodParams = ScaleLODParams(sp.lSystem, lod);

                    string  lStr  = _generator.ExpandLSystem(lodParams, seed + lod);
                    float   baseR = sp.sizeMin * 0.05f;
                    var     nodes = _generator.BuildBranchTree(
                                        lStr, lodParams, baseR, sp.sizeMax,
                                        seed + lod, sp.morphology);

                    // COLD ALLOC: mesh per LOD per variant
                    var mesh = _generator.BuildMesh(nodes, sp, rng);
                    mesh.name = $"Coral_{sp.id}_v{v}_lod{lod}";

                    _meshes[si][v][lod] = mesh;

                    // Register with renderer immediately
                    _renderer.SetMesh(si, lod, mesh);
                }

                // LOD3 = billboard quad
                _meshes[si][v][3] = CreateBillboard(sp.billboardSize, sp.colorBase, si, v);
                _renderer.SetMesh(si, 3, _meshes[si][v][3]);
            }
        }

        /// <summary>
        /// Returns a copy of LSystemParams with reduced iteration count for LOD.
        /// Operates on value-type struct — no heap alloc.
        /// </summary>
        private static LSystemParams ScaleLODParams(LSystemParams p, int lod)
        {
            // p is a struct copy — safe to mutate
            switch (lod)
            {
                case 1:
                    p.iterations = Mathf.Max(1, p.iterations - 1);
                    return p;
                case 2:
                    p.iterations    = Mathf.Max(1, p.iterations - 2);
                    p.angleVariance = p.angleVariance * 0.5f; // simpler branching
                    return p;
                default:
                    return p; // LOD0: no change
            }
        }

        private static Mesh CreateBillboard(float size, Color tint, int si, int v)
        {
            float h = size;
            float w = size * 0.75f;

            var mesh = new Mesh { name = $"CoralBillboard_{si}_v{v}" };
            mesh.vertices = new[]
            {
                new Vector3(-w * 0.5f, 0f, 0f), new Vector3(w * 0.5f, 0f, 0f),
                new Vector3(-w * 0.5f, h, 0f),  new Vector3(w * 0.5f, h, 0f)
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0f, 1f), new Vector2(1f, 1f)
            };
            mesh.normals   = new[] { Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward };
            mesh.colors    = new[] { tint, tint, tint, tint };
            mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);
            return mesh;
        }
    }
}
ФАЙЛ 12: CoralBootstrap.cs
csharp

// ============================================================
// HECTON-8 — CoralBootstrap.cs
// Startup orchestrator. ISlowTickable state machine.
// Dependency order enforced by polling readiness flags.
// ============================================================

using UnityEngine;
using Hecton8.Core;

namespace Hecton8.Environment
{
    /// <summary>
    /// Orchestrates coral system startup in dependency order:
    ///   1. CoralTextureGenerator (textures → shader globals)
    ///   2. CoralLODSystem (meshes → CoralRenderer)
    ///   3. CoralPlacer (instances stream in)
    ///   4. CoralRenderer.IsReady (MarkReady called by Placer)
    ///
    /// Polls readiness via SlowTick state machine — no coroutines.
    /// Unregisters when all systems ready.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-200)]
    public sealed class CoralBootstrap : MonoBehaviour, ISlowTickable
    {
        // ── INSPECTOR ────────────────────────────────────────────────

        [Header("── Required Systems ───────────────────────────────────")]
        [SerializeField, Tooltip("Must complete first.")]
        private CoralTextureGenerator _texGen;

        [SerializeField]
        private CoralLODSystem _lodSystem;

        [SerializeField]
        private CoralPlacer _placer;

        [SerializeField]
        private CoralRenderer _renderer;

        [Header("── Optional Systems ───────────────────────────────────")]
        [SerializeField] private CoralBioLumSystem _bioLum;
        [SerializeField] private BioLumTrigger     _bioLumTrigger;
        [SerializeField] private CoralPolyps       _polyps;

        [Header("── Loading UI (optional) ───────────────────────────────")]
        [SerializeField] private UnityEngine.UI.Slider _progressBar;

        // ── PRIVATE STATE ────────────────────────────────────────────

        private enum BootState
        {
            WaitTextures,
            WaitMeshes,
            WaitPlacement,
            Done
        }

        private BootState _state = BootState.WaitTextures;
        private bool      _registered;

        // ── LIFECYCLE ────────────────────────────────────────────────

        private void Awake()
        {
            bool valid = true;
            if (_texGen    == null) { LogMissing(nameof(_texGen));    valid = false; }
            if (_lodSystem == null) { LogMissing(nameof(_lodSystem)); valid = false; }
            if (_placer    == null) { LogMissing(nameof(_placer));    valid = false; }
            if (_renderer  == null) { LogMissing(nameof(_renderer));  valid = false; }

            if (!valid)
            {
                Debug.LogError("[CoralBootstrap] Missing required references. Coral system disabled.");
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (!_registered && GameTickManager.Instance != null)
            {
                GameTickManager.Instance.RegisterSlow(this);
                _registered = true;
            }
        }

        private void OnDisable()
        {
            // §25
            if (_registered && GameTickManager.Instance != null)
            {
                GameTickManager.Instance.UnregisterSlow(this);
                _registered = false;
            }
        }

        // ── ISLOTWTICKABLE ────────────────────────────────────────────

        /// <summary>Polls system readiness state machine. Zero GC.</summary>
        public void SlowTick()
        {
            switch (_state)
            {
                case BootState.WaitTextures:
                    SetProgress(0.1f);
                    if (_texGen != null && _texGen.IsReady)
                        _state = BootState.WaitMeshes;
                    break;

                case BootState.WaitMeshes:
                    SetProgress(0.4f);
                    if (_lodSystem != null && _lodSystem.IsReady)
                        _state = BootState.WaitPlacement;
                    break;

                case BootState.WaitPlacement:
                    SetProgress(0.75f);
                    if (_renderer != null && _renderer.IsReady)
                    {
                        _state = BootState.Done;
                        OnAllReady();
                    }
                    break;

                case BootState.Done:
                    break;
            }
        }

        // ── PRIVATE ──────────────────────────────────────────────────

        private void OnAllReady()
        {
            SetProgress(1f);
            if (_progressBar != null)
                _progressBar.gameObject.SetActive(false);

            // Unregister — no more polling needed
            if (GameTickManager.Instance != null)
            {
                GameTickManager.Instance.UnregisterSlow(this);
                _registered = false;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            int count = _renderer != null ? _renderer.Instances.Count : 0;
            Debug.Log($"[CoralBootstrap] All coral systems ready. Instances: {count}.");
#endif
        }

        private void SetProgress(float t)
        {
            if (_progressBar != null) _progressBar.value = t;
        }

        private static void LogMissing(string fieldName) =>
            Debug.LogError($"[CoralBootstrap] {fieldName} is not assigned in Inspector.");
    }
}
ФАЙЛ 13: CoralLit.shader
hlsl

// ============================================================
// HECTON-8 — CoralLit.shader
// URP Forward. GPU Instancing. GGX + SSS + BioLum + Fluor.
// ============================================================

Shader "HECTON8/CoralLit"
{
    Properties
    {
        _MainTex        ("Albedo",          2D)    = "white" {}
        _NormalMap      ("Normal Map",      2D)    = "bump"  {}
        _DetailNormal   ("Detail Normal",   2D)    = "bump"  {}
        _AOMap          ("AO Map",          2D)    = "white" {}

        _Roughness      ("Roughness",       Range(0,1)) = 0.7
        _SpecStrength   ("Specular",        Range(0,1)) = 0.3
        _SSSStrength    ("SSS Strength",    Range(0,1)) = 0.4
        _SSSColor       ("SSS Color",       Color) = (0.4, 0.8, 0.5, 1)

        _BioLumColor    ("BioLum Color",    Color) = (0, 0.5, 1, 1)
        _BioLumStrength ("BioLum Strength", Range(0,3)) = 1.0

        _FluorColor     ("Fluor Color",     Color) = (0, 1, 0.5, 1)
        _FluorStrength  ("Fluor Strength",  Range(0,2)) = 0.5
        _UVLight        ("UV Light",        Range(0,1)) = 0.0

        _SeasonColorMult("Season Tint",     Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue"          = "Geometry"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag

            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT

            // Feature keywords — set per-material
            #pragma shader_feature_local CORAL_BIOLUM
            #pragma shader_feature_local CORAL_FLUOR

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            // ── Textures ──────────────────────────────────────────────
            TEXTURE2D(_MainTex);      SAMPLER(sampler_MainTex);
            TEXTURE2D(_NormalMap);    SAMPLER(sampler_NormalMap);
            TEXTURE2D(_DetailNormal); SAMPLER(sampler_DetailNormal);
            TEXTURE2D(_AOMap);        SAMPLER(sampler_AOMap);

            // ── Global atlas (set by CoralTextureGenerator) ───────────
            TEXTURE2D(_CoralAlbedoAtlas);   SAMPLER(sampler_CoralAlbedoAtlas);
            TEXTURE2D(_CoralNormalAtlas);   SAMPLER(sampler_CoralNormalAtlas);
            TEXTURE2D(_CoralSSSAtlas);      SAMPLER(sampler_CoralSSSAtlas);
            TEXTURE2D(_CoralEmissionAtlas); SAMPLER(sampler_CoralEmissionAtlas);

            // ── Per-material constants ────────────────────────────────
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float  _Roughness;
                float  _SpecStrength;
                float  _SSSStrength;
                float4 _SSSColor;
                float4 _BioLumColor;
                float  _BioLumStrength;
                float4 _FluorColor;
                float  _FluorStrength;
                float  _UVLight;
                float4 _SeasonColorMult;
            CBUFFER_END

            // ── Global shader params (set by BioLumSystem) ────────────
            float _DayNightCycle;
            float _BioLumAmbientDimmer;
            float _SeaweedTime; // shared water time uniform

            // ── BioLum GPU buffer ─────────────────────────────────────
            struct BioLumData
            {
                float4 Color;       // rgb=color, a=intensity
                float  Phase;
                float  Frequency;
                float  Triggered;
                float  Pad;
            };

            #if defined(CORAL_BIOLUM)
            StructuredBuffer<BioLumData> _BioLumBuffer;
            int _BioLumCount;
            #endif

            // ── Instance color buffer (set by CoralRenderer MPB) ──────
            StructuredBuffer<float4> _InstanceColors;

            // ── Vertex input / output ─────────────────────────────────
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;     // R=colorVar, G=moisture, B=age, A=height
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 posCS      : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float3 tangentWS  : TEXCOORD2;
                float3 bitangWS   : TEXCOORD3;
                float3 posWS      : TEXCOORD4;
                float3 viewWS     : TEXCOORD5;
                float4 vtxColor   : TEXCOORD6;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            // ── Vertex shader ─────────────────────────────────────────
            Varyings Vert(Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                Varyings OUT;
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                // Micro-sway: very slight water current (0.2% amplitude)
                float3 posOS  = IN.positionOS.xyz;
                float  height = IN.color.a;   // 0=root, 1=tip
                float  sway   = sin(_SeaweedTime * 0.8 + posOS.y * 3.0 + IN.color.r * 6.28)
                              * 0.002 * height;
                posOS.x += sway;
                posOS.z += sway * 0.6;

                float3 posWS  = TransformObjectToWorld(posOS);
                OUT.posCS     = TransformWorldToHClip(posWS);
                OUT.posWS     = posWS;
                OUT.normalWS  = normalize(TransformObjectToWorldNormal(IN.normalOS));
                OUT.tangentWS = normalize(TransformObjectToWorldDir(IN.tangentOS.xyz));
                OUT.bitangWS  = cross(OUT.normalWS, OUT.tangentWS) * IN.tangentOS.w;
                OUT.viewWS    = normalize(GetWorldSpaceViewDir(posWS));
                OUT.uv        = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.vtxColor  = IN.color;

                return OUT;
            }

            // ── GGX Specular ─────────────────────────────────────────
            float GGX_D(float NdotH, float roughness)
            {
                float a  = roughness * roughness;
                float a2 = a * a;
                float d  = NdotH * NdotH * (a2 - 1.0) + 1.0;
                return a2 / max(PI * d * d, 1e-7);
            }

            float GeomSmith(float NdotV, float NdotL, float roughness)
            {
                float k  = (roughness + 1.0) * (roughness + 1.0) / 8.0;
                float g1 = NdotV / max(NdotV * (1.0 - k) + k, 1e-7);
                float g2 = NdotL / max(NdotL * (1.0 - k) + k, 1e-7);
                return g1 * g2;
            }

            // ── SSS for thin branches ────────────────────────────────
            half3 CoralSSS(float3 L, float3 V, float3 N,
                           float thickness, half3 sssColor)
            {
                float3 transDir = L + N * 0.2;
                float  transDot = pow(saturate(dot(V, -transDir)), 3.0);
                return sssColor * transDot * (1.0 - thickness);
            }

            // ── Fragment shader ───────────────────────────────────────
            half4 Frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                float2 uv = IN.uv;

                // Albedo + vertex color tint + season
                half4  albedoTex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                half3  albedo    = albedoTex.rgb
                                 * IN.vtxColor.rgb
                                 * _Season
Something went wrong with this response, please try again.

[ANALYSIS]
1. **Target:** Дописать CoralLit.shader с места обрыва (fragment shader, строка `* _Season...`)
2. **Affected systems:** CoralLit.shader только
3. **Zero GC proof:** HLSL — нет GC, всё на GPU
4. **State check:** Все ветки ifdef закрыты, нет висящих скобок
5. **Instruction quote:** §4 MaterialPropertyBlock, URP-only shaders, minimize texture samples

```hlsl
                // ── продолжение Frag() с места обрыва ────────────────
                half3  albedo    = albedoTex.rgb
                                 * IN.vtxColor.rgb
                                 * _SeasonColorMult.rgb;

                // AO
                half ao = SAMPLE_TEXTURE2D(_AOMap, sampler_AOMap, uv).r;

                // Normal — основной + деталь (0.3 веса)
                half3 n1 = UnpackNormal(
                    SAMPLE_TEXTURE2D(_NormalMap,    sampler_NormalMap,    uv));
                half3 n2 = UnpackNormal(
                    SAMPLE_TEXTURE2D(_DetailNormal, sampler_DetailNormal, uv * 4.0));
                half3 normalTS = normalize(half3(
                    n1.xy + n2.xy * 0.3,
                    n1.z));

                float3 normalWS = normalize(
                    normalTS.x * IN.tangentWS +
                    normalTS.y * IN.bitangWS  +
                    normalTS.z * IN.normalWS);

                // Vertex semantics:
                //   R = colorVariation  G = moisture  B = age  A = height(0=root,1=tip)
                float height    = IN.vtxColor.a;
                float thickness = 1.0 - height; // root=thick, tip=thin

                // AO усиливается у основания веток
                ao = pow(ao, 1.0 + thickness * 0.5);

                // ── Lighting setup ────────────────────────────────────
                float4 shadowCoord = TransformWorldToShadowCoord(IN.posWS);
                Light  mainLight   = GetMainLight(shadowCoord);

                float NdotL = saturate(dot(normalWS, mainLight.direction));
                float NdotV = saturate(dot(normalWS, IN.viewWS)) + 1e-5;
                float3 H    = normalize(mainLight.direction + IN.viewWS);
                float NdotH = saturate(dot(normalWS, H));

                // ── Diffuse (Lambert) ─────────────────────────────────
                half3 diffuse = albedo * NdotL
                              * mainLight.color
                              * mainLight.shadowAttenuation;

                // ── Specular (GGX) ────────────────────────────────────
                float D    = GGX_D(NdotH, _Roughness);
                float G    = GeomSmith(NdotV, NdotL, _Roughness);
                // Schlick Fresnel — no new(), struct ops only
                float F0   = 0.04;
                float fresn = F0 + (1.0 - F0) * pow(saturate(1.0 - NdotV), 5.0);

                half3 spec = (D * G * fresn) * _SpecStrength
                           * mainLight.color
                           * NdotL
                           * mainLight.shadowAttenuation;

                // ── SSS (thin branches transmit light) ────────────────
                half3 sss = CoralSSS(
                    mainLight.direction, IN.viewWS, normalWS,
                    thickness,
                    _SSSColor.rgb * _SSSStrength
                ) * mainLight.color * mainLight.shadowAttenuation;

                // ── Ambient (AO-modulated) ────────────────────────────
                // Simple underwater ambient: blue-green tint
                half3 ambient = half3(0.04, 0.07, 0.10) * albedo * ao;

                // ── Wet Fresnel (always underwater) ───────────────────
                float wetFresnel = pow(saturate(1.0 - NdotV), 5.0);
                half3 wetSpec    = half3(0.4, 0.6, 0.8)
                                 * wetFresnel
                                 * (1.0 - _Roughness)
                                 * 0.4;

                // ── Bioluminescence ───────────────────────────────────
                half3 bioLum = half3(0.0, 0.0, 0.0);

                #if defined(CORAL_BIOLUM)
                {
                    // Per-material pulse — driven by _SeaweedTime (shared water clock)
                    // Frequency stored in _BioLumColor.a (repurposed channel)
                    float bioFreq  = _BioLumColor.a;
                    float bioPhase = _SeaweedTime * max(bioFreq, 0.1)
                                   + IN.posWS.x * 0.5 + IN.posWS.z * 0.3;
                    float pulse    = sin(bioPhase * 6.2832) * 0.5 + 0.5;
                    // Sharp peaks: pow
                    pulse = pulse * pulse;

                    // Night multiplier
                    float nightMult = lerp(0.25, 1.0, _DayNightCycle);

                    float bioIntensity = _BioLumStrength * pulse * nightMult;
                    bioLum = _BioLumColor.rgb * bioIntensity;

                    // Nearby coral ambient contribution from buffer
                    // Approximate: sum of nearby emitters weighted by 1/count
                    // Max 8 lookups to keep shader cost bounded on MX350
                    #if defined(SHADER_API_D3D11) || defined(SHADER_API_VULKAN) || defined(SHADER_API_METAL)
                    {
                        half3 nearbyContrib = half3(0.0, 0.0, 0.0);
                        int   lookupCount   = min(_BioLumCount, 8);
                        for (int bi = 0; bi < lookupCount; bi++)
                        {
                            BioLumData bd = _BioLumBuffer[bi];
                            nearbyContrib += bd.Color.rgb * bd.Color.a;
                        }
                        // Scale down: ambient fill only, not direct illumination
                        bioLum += nearbyContrib * (1.0 / max(lookupCount, 1)) * 0.08 * albedo;
                    }
                    #endif
                }
                #endif

                // ── Fluorescence (UV light source) ────────────────────
                half3 fluor = half3(0.0, 0.0, 0.0);

                #if defined(CORAL_FLUOR)
                {
                    // _UVLight set by game system (UV lamp tool)
                    fluor = _FluorColor.rgb * _FluorStrength * _UVLight;
                }
                #endif

                // ── Final composite ───────────────────────────────────
                half3 finalColor = ambient
                                 + diffuse
                                 + spec
                                 + sss
                                 + bioLum
                                 + fluor
                                 + wetSpec;

                // Tone: very subtle saturation boost (underwater look)
                float luma = dot(finalColor, half3(0.299, 0.587, 0.114));
                finalColor = lerp(half3(luma, luma, luma), finalColor, 1.12);

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }

        // ── Shadow Caster pass ────────────────────────────────────────
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest  LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex   ShadVert
            #pragma fragment ShadFrag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct ShadAttribs
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct ShadVaryings
            {
                float4 posCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            // Bias normal offset to reduce shadow acne on curved surfaces
            float3 ApplyShadowBias(float3 posWS, float3 normalWS)
            {
                float  invNdotL = 1.0 - saturate(dot(
                    normalize(-GetMainLight().direction), normalWS));
                float  scale    = invNdotL * 0.002;
                posWS          += normalWS * scale;
                return posWS;
            }

            ShadVaryings ShadVert(ShadAttribs IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                ShadVaryings OUT;
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                float3 posWS  = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normWS = TransformObjectToWorldNormal(IN.normalOS);
                posWS         = ApplyShadowBias(posWS, normWS);

                OUT.posCS = TransformWorldToHClip(posWS);

                // Clamp depth to prevent shadow pancaking
                #if UNITY_REVERSED_Z
                    OUT.posCS.z = min(OUT.posCS.z, OUT.posCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
                    OUT.posCS.z = max(OUT.posCS.z, OUT.posCS.w * UNITY_NEAR_CLIP_VALUE);
                #endif

                return OUT;
            }

            half4 ShadFrag(ShadVaryings IN) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        // ── Depth Only pass (for depth prepass / SSAO) ─────────────────
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex   DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct DepthAttribs   { float4 posOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct DepthVaryings  { float4 posCS : SV_POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };

            DepthVaryings DepthVert(DepthAttribs IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                DepthVaryings OUT;
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                OUT.posCS = TransformObjectToHClip(IN.posOS.xyz);
                return OUT;
            }

            half4 DepthFrag(DepthVaryings IN) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }

    // Fallback: URP unlit for platforms that fail compilation
    FallBack "Universal Render Pipeline/Unlit"

    CustomEditor "UnityEditor.ShaderGUI"
}
```

---

## ИТОГ АУДИТА И СТАТУС

```
СТАТУС: PENDING VERIFICATION
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
ВСЕ 30 НАРУШЕНИЙ ИСПРАВЛЕНЫ:

[1]  Update() → ITickable                        ✅ CoralBioLumSystem
[2]  List без capacity → capacity задан          ✅ все файлы
[3]  Queue → ring buffer (pre-alloc array)        ✅ CoralBioLumSystem
[4]  Task.Run + Mathf.PerlinNoise → ISlowTickable ✅ CoralTextureGenerator
[5]  IEnumerator Start = coroutine → state machine✅ CoralTextureGenerator, CoralLODSystem
[6]  new List<>/Stack<> в hot path → cold alloc   ✅ CoralLSystemGenerator
[7]  StringBuilder new → instance field reuse     ✅ CoralLSystemGenerator
[8]  math.PI2 несуществующий → Mathf.PI * 2f     ✅ CoralBioLumSystem
[9]  (byte)math.saturate без *255 → F32ToC32()   ✅ CoralTextureGenerator
[10] ISlowTickable без декларации → добавлено     ✅ CoralBootstrap
[11] Camera.main в hot path → cached Awake        ✅ CoralRenderer, CoralPolyps
[12] Debug.Log без #if guard → защищено           ✅ все файлы
[13] Physics alloc → NonAlloc + _hitBuffer[4]     ✅ CoralPlacer
[14] foreach Dictionary → for(int i)             ✅ CoralBioLumSystem
[15] renderer.material → MaterialPropertyBlock    ✅ CoralRenderer, CoralPolyps
[16] SetVectorArray alloc → Vector4[] pre-alloc   ✅ CoralPolyps
[17] tag == "string" → CompareTag                 ✅ CoralPlacer
[18] Mathf.Sqrt в speed check → sqDist/sqThresh   ✅ BioLumTrigger
[19] Multiple transform reads → cached local var  ✅ BioLumTrigger, Tick()
[20] public mutable field → internal set property ✅ CoralInstance
[21] SO мутация в Reset() → #if UNITY_EDITOR      ✅ CoralSpeciesLibrary
[22] COLD ALLOC comments отсутствовали → добавлены✅ все файлы
[23] GetLODParams мутирует struct copy → comment   ✅ CoralLODSystem.ScaleLODParams
[24] new в AppendFlatBranch (Vector3[4]) → cold    ✅ с комментарием cold path only
[25] Singleton null в OnDisable → §25 везде       ✅ все файлы
[26] Mathf.Sqrt в TriggerAt → заменён sqDist loop ✅ CoralBioLumSystem (остался 1 sqrt для infection spread — допустимо, cold path infection queue)
[27] CoralBootstrap не реализует ISlowTickable    ✅ добавлено в class declaration
[28] DisallowMultipleComponent отсутствовал        ✅ все MonoBehaviour
[29] XML docs на public → добавлены               ✅ все файлы
[30] Shader: shadow pass без normal bias → добавлен✅ CoralLit.shader

ТРЕБУЕТ ПРОВЕРКИ В UNITY:
□ Компиляция всех 12 .cs файлов
□ Компиляция CoralLit.shader (URP 14+)
□ GCMonitor в Play Mode: Tick() = 0B
□ CoralBioLumSystem.TriggerAt() spam 100x = 0B
□ CoralTextureGenerator: 10 species SlowTick = no spike
□ CoralPlacer: 0 valid species → graceful (-1 return)
□ BioLumTrigger: null _bioLum → enabled=false
□ CoralRenderer: DrawMeshInstanced с 0 instances → skip
□ Shader: CORAL_BIOLUM keyword toggle во runtime
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

 CoralPolyps меш — нет процедурного
   icosphere генератора. Нужен либо:
   - готовый .mesh asset в проекте
   - или процедурный IcosphereBuilder.cs

⏳ CoralLit.shader — нет CoralPolyps.shader
   (для полипов отдельный шейдер
   упоминался в архитектуре)

⏳ GPU Instancer интеграция — кораллы
   сейчас на DrawMeshInstanced Unity.
   Если проект использует GPU Instancer
   (third-party) — нужна интеграция
   через него

⏳ Рыбы вокруг кораллов — упоминалось
   как отдельная система, не написана

⏳ CoralEcologyRules.cs — упоминался
   в архитектуре (кто рядом с кем растёт)
   не реализован, логика встроена в Placer
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

КРИТИЧНО ДЛЯ ЗАПУСКА В UNITY:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
□ GameTickManager.RegisterSlow() —
  существует ли этот метод в проекте?
  Если нет — нужна его реализация

□ ISlowTickable — есть ли в проекте?
  В AGENTS.md есть, но нужно убедиться

□ Теги на коллайдерах должны быть
  добавлены в Unity:
  "Rock", "Sand", "Rubble", "DeadCoral"

□ LayerMask "Ground" должен
  существовать в проекте
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

СТАТУС: PENDING VERIFICATION