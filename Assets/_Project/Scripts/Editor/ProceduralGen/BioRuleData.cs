using System;
using UnityEngine;

namespace Hecton8.Editor.ProceduralGen
{
    /// <summary>
    /// SDF primitive profile used by the editor-only Bio-Forge baker.
    /// </summary>
    public enum BioForgeSdfProfile
    {
        BranchCapsules = 0,
        RibbonFlora = 1,
        SolidRock = 2,
        PorousRock = 3
    }

    /// <summary>
    /// Editor-only authoring data for Bio-Forge offline L-system mesh generation.
    /// </summary>
    [CreateAssetMenu(fileName = "BioRuleData_New", menuName = "HECTON-8/Bio-Forge/Bio Rule Data")]
    public sealed class BioRuleData : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField, Tooltip("Prefix used for generated mesh and prefab assets.")]
        private string _assetPrefix = "GEN_BioForge_Kelp";

        [SerializeField, Tooltip("Optional material assigned to all generated LOD renderers. One material slot only.")]
        private Material _material;

        [Header("L-System")]
        [SerializeField, TextArea(2, 4), Tooltip("Initial L-system string.")]
        private string _axiom = "F[+F]F[-F]F";

        [SerializeField, Tooltip("Replacement rules. The first character of Symbol is used.")]
        private BioRuleReplacement[] _rules =
        {
            new BioRuleReplacement("F", "FF[+F][-F]")
        };

        [SerializeField, Range(0, 6), Tooltip("Offline expansion iterations. Higher values grow exponentially.")]
        private int _iterations = 3;

        [SerializeField, Range(32, 24000), Tooltip("Hard cap on emitted branch segments to keep editor batches bounded.")]
        private int _maxBranches = 6000;

        [SerializeField, Range(1f, 85f), Tooltip("Yaw/pitch/roll angle in degrees for + - & ^ / \\ commands.")]
        private float _angleDegrees = 24f;

        [SerializeField, Range(0.02f, 4f), Tooltip("Base branch step length in meters.")]
        private float _stepLength = 0.35f;

        [SerializeField, Range(0.2f, 0.98f), Tooltip("Length multiplier applied after each pushed branch depth.")]
        private float _lengthTaper = 0.82f;

        [SerializeField, Range(0.01f, 1f), Tooltip("Root branch radius in meters.")]
        private float _rootRadius = 0.13f;

        [SerializeField, Range(0.2f, 0.98f), Tooltip("Radius multiplier applied by branch depth.")]
        private float _radiusTaper = 0.72f;

        [SerializeField, Range(0.005f, 0.25f), Tooltip("Minimum branch radius in meters.")]
        private float _minimumRadius = 0.025f;

        [Header("SDF / Marching Cubes")]
        [SerializeField, Range(12, 96), Tooltip("SDF cells per axis for LOD0 raw extraction.")]
        private int _sdfResolution = 44;

        [SerializeField, Range(0.05f, 3f), Tooltip("Extra bounds padding around generated branches.")]
        private float _boundsPadding = 0.4f;

        [SerializeField, Range(0.5f, 32f), Tooltip("Exponential smooth-min k. Higher values make sharper joins.")]
        private float _smoothMinK = 8f;

        [SerializeField, Tooltip("SDF primitive profile used by the offline baker.")]
        private BioForgeSdfProfile _sdfProfile = BioForgeSdfProfile.BranchCapsules;

        [SerializeField, Range(0.05f, 1f), Tooltip("Ribbon flora thickness multiplier relative to branch radius.")]
        private float _ribbonThicknessScale = 0.18f;

        [SerializeField, Range(0.5f, 4f), Tooltip("Ribbon flora width multiplier relative to branch radius.")]
        private float _ribbonWidthScale = 2.4f;

        [Header("LOD Budgets")]
        [SerializeField, Range(200, 20000), Tooltip("LOD0 triangle budget after decimation.")]
        private int _lod0TriangleBudget = 5000;

        [SerializeField, Range(100, 8000), Tooltip("LOD1 triangle budget after decimation.")]
        private int _lod1TriangleBudget = 1000;

        [SerializeField, Range(32, 2000), Tooltip("LOD2 triangle budget after decimation.")]
        private int _lod2TriangleBudget = 200;

        [Header("Rock Variant")]
        [SerializeField, Range(0.2f, 6f), Tooltip("Base radius for generated rock variants.")]
        private float _rockRadius = 1.4f;

        [SerializeField, Range(0f, 0.8f), Tooltip("Noise displacement amplitude for generated rock SDFs.")]
        private float _rockNoiseAmplitude = 0.22f;

        [SerializeField, Range(0.2f, 12f), Tooltip("Noise frequency for generated rock SDFs.")]
        private float _rockNoiseFrequency = 3.5f;

        [SerializeField, Range(0, 32), Tooltip("Deterministic subtractive pore count for porous rock mode.")]
        private int _rockPoreCount = 0;

        [SerializeField, Range(0.05f, 1.5f), Tooltip("Average subtractive pore sphere radius for porous rock mode.")]
        private float _rockPoreRadius = 0.35f;

        [SerializeField, Range(0f, 1f), Tooltip("Pore-center bias toward rock surface. Higher values open more visible holes.")]
        private float _rockPoreSurfaceBias = 0.72f;

        [Header("Output")]
        [SerializeField, Tooltip("Mesh asset output folder.")]
        private string _meshOutputFolder = "Assets/_Project/Art/Generated/Flora";

        [SerializeField, Tooltip("Prefab output folder.")]
        private string _prefabOutputFolder = "Assets/_Project/Prefabs/Nature/Flora/BioForge";

        /// <summary>Prefix used for generated asset names.</summary>
        public string AssetPrefix => string.IsNullOrWhiteSpace(_assetPrefix) ? "GEN_BioForge" : _assetPrefix;

        /// <summary>Optional generated prefab material.</summary>
        public Material Material => _material;

        /// <summary>L-system axiom.</summary>
        public string Axiom => string.IsNullOrEmpty(_axiom) ? "F" : _axiom;

        /// <summary>Expansion iteration count.</summary>
        public int Iterations => Mathf.Clamp(_iterations, 0, 6);

        /// <summary>Maximum emitted branch count.</summary>
        public int MaxBranches => Mathf.Max(1, _maxBranches);

        /// <summary>Branch command angle in degrees.</summary>
        public float AngleDegrees => Mathf.Clamp(_angleDegrees, 1f, 85f);

        /// <summary>Root branch segment length.</summary>
        public float StepLength => Mathf.Max(0.02f, _stepLength);

        /// <summary>Branch length depth taper.</summary>
        public float LengthTaper => Mathf.Clamp(_lengthTaper, 0.2f, 0.98f);

        /// <summary>Root SDF branch radius.</summary>
        public float RootRadius => Mathf.Max(0.01f, _rootRadius);

        /// <summary>Branch radius depth taper.</summary>
        public float RadiusTaper => Mathf.Clamp(_radiusTaper, 0.2f, 0.98f);

        /// <summary>Minimum SDF branch radius.</summary>
        public float MinimumRadius => Mathf.Max(0.005f, _minimumRadius);

        /// <summary>SDF cells per axis.</summary>
        public int SdfResolution => Mathf.Clamp(_sdfResolution, 12, 96);

        /// <summary>Additional AABB padding in meters.</summary>
        public float BoundsPadding => Mathf.Max(0.05f, _boundsPadding);

        /// <summary>Exponential smooth-min k value.</summary>
        public float SmoothMinK => Mathf.Clamp(_smoothMinK, 0.5f, 32f);

        /// <summary>SDF primitive profile used by the offline baker.</summary>
        public BioForgeSdfProfile SdfProfile => _sdfProfile;

        /// <summary>Ribbon flora thickness multiplier relative to branch radius.</summary>
        public float RibbonThicknessScale => Mathf.Clamp(_ribbonThicknessScale, 0.05f, 1f);

        /// <summary>Ribbon flora width multiplier relative to branch radius.</summary>
        public float RibbonWidthScale => Mathf.Clamp(_ribbonWidthScale, 0.5f, 4f);

        /// <summary>LOD0 triangle budget.</summary>
        public int Lod0TriangleBudget => Mathf.Max(32, _lod0TriangleBudget);

        /// <summary>LOD1 triangle budget.</summary>
        public int Lod1TriangleBudget => Mathf.Max(32, _lod1TriangleBudget);

        /// <summary>LOD2 triangle budget.</summary>
        public int Lod2TriangleBudget => Mathf.Max(16, _lod2TriangleBudget);

        /// <summary>Rock SDF base radius.</summary>
        public float RockRadius => Mathf.Max(0.2f, _rockRadius);

        /// <summary>Rock SDF noise amplitude.</summary>
        public float RockNoiseAmplitude => Mathf.Max(0f, _rockNoiseAmplitude);

        /// <summary>Rock SDF noise frequency.</summary>
        public float RockNoiseFrequency => Mathf.Max(0.2f, _rockNoiseFrequency);

        /// <summary>Deterministic subtractive pore count for porous rock mode.</summary>
        public int RockPoreCount => Mathf.Clamp(_rockPoreCount, 0, 32);

        /// <summary>Average subtractive pore sphere radius for porous rock mode.</summary>
        public float RockPoreRadius => Mathf.Clamp(_rockPoreRadius, 0.05f, 1.5f);

        /// <summary>Pore-center bias toward rock surface.</summary>
        public float RockPoreSurfaceBias => Mathf.Clamp01(_rockPoreSurfaceBias);

        /// <summary>Mesh asset output folder.</summary>
        public string MeshOutputFolder => string.IsNullOrWhiteSpace(_meshOutputFolder) ? "Assets/_Project/Art/Generated/Flora" : _meshOutputFolder;

        /// <summary>Prefab output folder.</summary>
        public string PrefabOutputFolder => string.IsNullOrWhiteSpace(_prefabOutputFolder) ? "Assets/_Project/Prefabs/Nature/Flora/BioForge" : _prefabOutputFolder;

        /// <summary>
        /// Resolves an L-system replacement for one symbol.
        /// </summary>
        /// <param name="symbol">Source symbol.</param>
        /// <param name="replacement">Resolved replacement text.</param>
        /// <returns>True if a rule matched.</returns>
        public bool TryGetReplacement(char symbol, out string replacement)
        {
            if (_rules != null)
            {
                for (int i = 0; i < _rules.Length; i++)
                {
                    if (_rules[i].Matches(symbol))
                    {
                        replacement = _rules[i].Replacement;
                        return true;
                    }
                }
            }

            replacement = null;
            return false;
        }
    }

    /// <summary>
    /// One authoring L-system replacement rule.
    /// </summary>
    [Serializable]
    public struct BioRuleReplacement
    {
        [SerializeField, Tooltip("First character is used as the source symbol.")]
        private string _symbol;

        [SerializeField, TextArea(1, 3), Tooltip("Replacement string.")]
        private string _replacement;

        /// <summary>
        /// Creates a replacement rule.
        /// </summary>
        /// <param name="symbol">Source symbol string. First character is used.</param>
        /// <param name="replacement">Replacement string.</param>
        public BioRuleReplacement(string symbol, string replacement)
        {
            _symbol = symbol;
            _replacement = replacement;
        }

        /// <summary>Replacement string.</summary>
        public string Replacement => string.IsNullOrEmpty(_replacement) ? _symbol : _replacement;

        /// <summary>Returns true when this rule matches the supplied symbol.</summary>
        public bool Matches(char symbol)
        {
            return !string.IsNullOrEmpty(_symbol) && _symbol[0] == symbol;
        }
    }
}
