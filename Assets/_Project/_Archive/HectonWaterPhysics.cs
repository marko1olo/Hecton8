using UnityEngine;
using Unity.Mathematics;

/// <summary>
/// HectonWaterPhysics — SINGLE SOURCE OF TRUTH for the Hydro-X 2.0 ocean system.
/// Exposes ALL wave, color, foam, SSS, normal-map, and PBR parameters.
/// Pushes every value to the assigned ocean material each frame.
/// Provides CPU-side Gerstner evaluation for buoyancy / physics.
/// </summary>
[DefaultExecutionOrder(-100)]
public class HectonWaterPhysics : MonoBehaviour
{
    // ================================================================
    // SINGLETON
    // ================================================================
    private static HectonWaterPhysics _instance;
    public static HectonWaterPhysics Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<HectonWaterPhysics>();
                if (_instance == null)
                    Debug.LogError("[HectonWaterPhysics] No instance found in scene!");
            }
            return _instance;
        }
    }

    // ================================================================
    // MATERIAL REFERENCE
    // ================================================================
    [Header("Target Material")]
    [Tooltip("The material using Hecton/HectonOcean_v2 shader. This script is the single source of truth — all parameters are pushed here.")]
    [SerializeField] private Material oceanMaterial;

    public Material OceanMaterial
    {
        get => oceanMaterial;
        set => oceanMaterial = value;
    }

    // ================================================================
    // WAVE GLOBALS
    // ================================================================
    [Header("Global Wave Parameters")]
    [SerializeField, Range(0f, 5f)]
    private float waveHeight = 1.0f;
    public float WaveHeight { get => waveHeight; set => waveHeight = value; }

    [SerializeField, Range(0f, 5f)]
    private float waveSpeed = 1.0f;
    public float WaveSpeed { get => waveSpeed; set => waveSpeed = value; }

    [SerializeField, Range(0f, 2f)]
    private float waveChoppiness = 0.6f;
    public float WaveChoppiness { get => waveChoppiness; set => waveChoppiness = value; }

    // ================================================================
    // WAVE OCTAVE 0
    // ================================================================
    [Header("Wave Octave 0")]
    [SerializeField] private Vector2 wave0Direction = new Vector2(1f, 0f);
    public Vector2 Wave0Direction { get => wave0Direction; set => wave0Direction = value; }

    [SerializeField] private float wave0Amplitude = 1.0f;
    public float Wave0Amplitude { get => wave0Amplitude; set => wave0Amplitude = value; }

    [SerializeField] private float wave0Wavelength = 8.0f;
    public float Wave0Wavelength { get => wave0Wavelength; set => wave0Wavelength = value; }

    [SerializeField, Range(0f, 1f)]
    private float wave0Steepness = 0.5f;
    public float Wave0Steepness { get => wave0Steepness; set => wave0Steepness = value; }

    // ================================================================
    // WAVE OCTAVE 1
    // ================================================================
    [Header("Wave Octave 1")]
    [SerializeField] private Vector2 wave1Direction = new Vector2(0.7f, 0.7f);
    public Vector2 Wave1Direction { get => wave1Direction; set => wave1Direction = value; }

    [SerializeField] private float wave1Amplitude = 0.5f;
    public float Wave1Amplitude { get => wave1Amplitude; set => wave1Amplitude = value; }

    [SerializeField] private float wave1Wavelength = 4.0f;
    public float Wave1Wavelength { get => wave1Wavelength; set => wave1Wavelength = value; }

    [SerializeField, Range(0f, 1f)]
    private float wave1Steepness = 0.4f;
    public float Wave1Steepness { get => wave1Steepness; set => wave1Steepness = value; }

    // ================================================================
    // WAVE OCTAVE 2
    // ================================================================
    [Header("Wave Octave 2")]
    [SerializeField] private Vector2 wave2Direction = new Vector2(-0.3f, 0.9f);
    public Vector2 Wave2Direction { get => wave2Direction; set => wave2Direction = value; }

    [SerializeField] private float wave2Amplitude = 0.25f;
    public float Wave2Amplitude { get => wave2Amplitude; set => wave2Amplitude = value; }

    [SerializeField] private float wave2Wavelength = 2.5f;
    public float Wave2Wavelength { get => wave2Wavelength; set => wave2Wavelength = value; }

    [SerializeField, Range(0f, 1f)]
    private float wave2Steepness = 0.35f;
    public float Wave2Steepness { get => wave2Steepness; set => wave2Steepness = value; }

    // ================================================================
    // COLOR & DEPTH
    // ================================================================
    [Header("Color & Depth")]
    [SerializeField] private Color shallowColor = new Color(0.2f, 0.75f, 0.7f, 0.6f);
    public Color ShallowColor { get => shallowColor; set => shallowColor = value; }

    [SerializeField] private Color deepColor = new Color(0.02f, 0.07f, 0.15f, 0.95f);
    public Color DeepColor { get => deepColor; set => deepColor = value; }

    [SerializeField, Range(0.01f, 2.0f)]
    private float absorptionCoeff = 0.45f;
    public float AbsorptionCoeff { get => absorptionCoeff; set => absorptionCoeff = value; }

    [SerializeField, Range(0.1f, 50f)]
    private float depthMaxDistance = 15.0f;
    public float DepthMaxDistance { get => depthMaxDistance; set => depthMaxDistance = value; }

    [SerializeField, Range(0.01f, 5.0f)]
    private float depthFadeDistance = 1.5f;
    public float DepthFadeDistance { get => depthFadeDistance; set => depthFadeDistance = value; }

    // ================================================================
    // FOAM
    // ================================================================
    [Header("Foam")]
    [SerializeField] private Color foamColor = new Color(0.85f, 0.9f, 0.92f, 1f);
    public Color FoamColor { get => foamColor; set => foamColor = value; }

    [SerializeField, Range(0f, 3f)]
    private float foamDepthThreshold = 0.8f;
    public float FoamDepthThreshold { get => foamDepthThreshold; set => foamDepthThreshold = value; }

    [SerializeField, Range(0f, 2f)]
    private float foamCrestThreshold = 0.55f;
    public float FoamCrestThreshold { get => foamCrestThreshold; set => foamCrestThreshold = value; }

    [SerializeField, Range(0f, 3f)]
    private float foamIntensity = 1.2f;
    public float FoamIntensity { get => foamIntensity; set => foamIntensity = value; }

    [SerializeField, Range(0.1f, 20f)]
    private float foamScale = 5.0f;
    public float FoamScale { get => foamScale; set => foamScale = value; }

    // ================================================================
    // SUBSURFACE SCATTERING
    // ================================================================
    [Header("Subsurface Scattering")]
    [SerializeField] private Color sssColor = new Color(0.1f, 0.6f, 0.4f, 1f);
    public Color SSSColor { get => sssColor; set => sssColor = value; }

    [SerializeField, Range(0f, 5f)]
    private float sssIntensity = 1.5f;
    public float SSSIntensity { get => sssIntensity; set => sssIntensity = value; }

    [SerializeField, Range(1f, 16f)]
    private float sssPower = 4.0f;
    public float SSSPower { get => sssPower; set => sssPower = value; }

    [SerializeField, Range(0f, 1f)]
    private float sssDistortion = 0.3f;
    public float SSSDistortion { get => sssDistortion; set => sssDistortion = value; }

    // ================================================================
    // NORMAL MAPS (Anti-Tiling, 3 Layers)
    // ================================================================
    [Header("Normal Maps — Anti-Tiling")]
    [SerializeField, Range(0f, 2f)]
    private float normalStrength = 1.0f;
    public float NormalStrength { get => normalStrength; set => normalStrength = value; }

    [SerializeField] private float normalLayer0Scale = 0.04f;
    public float NormalLayer0Scale { get => normalLayer0Scale; set => normalLayer0Scale = value; }

    [SerializeField] private float normalLayer0SpeedX = 0.01f;
    public float NormalLayer0SpeedX { get => normalLayer0SpeedX; set => normalLayer0SpeedX = value; }

    [SerializeField] private float normalLayer0SpeedY = 0.008f;
    public float NormalLayer0SpeedY { get => normalLayer0SpeedY; set => normalLayer0SpeedY = value; }

    [SerializeField] private float normalLayer0Rotation = 0f;
    public float NormalLayer0Rotation { get => normalLayer0Rotation; set => normalLayer0Rotation = value; }

    [SerializeField] private float normalLayer1Scale = 0.1f;
    public float NormalLayer1Scale { get => normalLayer1Scale; set => normalLayer1Scale = value; }

    [SerializeField] private float normalLayer1SpeedX = -0.018f;
    public float NormalLayer1SpeedX { get => normalLayer1SpeedX; set => normalLayer1SpeedX = value; }

    [SerializeField] private float normalLayer1SpeedY = 0.012f;
    public float NormalLayer1SpeedY { get => normalLayer1SpeedY; set => normalLayer1SpeedY = value; }

    [SerializeField] private float normalLayer1Rotation = 37f;
    public float NormalLayer1Rotation { get => normalLayer1Rotation; set => normalLayer1Rotation = value; }

    [SerializeField] private float normalLayer2Scale = 0.35f;
    public float NormalLayer2Scale { get => normalLayer2Scale; set => normalLayer2Scale = value; }

    [SerializeField] private float normalLayer2SpeedX = 0.03f;
    public float NormalLayer2SpeedX { get => normalLayer2SpeedX; set => normalLayer2SpeedX = value; }

    [SerializeField] private float normalLayer2SpeedY = -0.025f;
    public float NormalLayer2SpeedY { get => normalLayer2SpeedY; set => normalLayer2SpeedY = value; }

    [SerializeField] private float normalLayer2Rotation = 72f;
    public float NormalLayer2Rotation { get => normalLayer2Rotation; set => normalLayer2Rotation = value; }

    // ================================================================
    // PBR SURFACE
    // ================================================================
    [Header("PBR Surface")]
    [SerializeField, Range(0f, 1f)]
    private float smoothness = 0.92f;
    public float Smoothness { get => smoothness; set => smoothness = value; }

    [SerializeField, Range(0f, 1f)]
    private float metallic = 0.02f;
    public float Metallic { get => metallic; set => metallic = value; }

    [SerializeField, Range(1f, 10f)]
    private float fresnelPower = 5.0f;
    public float FresnelPower { get => fresnelPower; set => fresnelPower = value; }

    // ================================================================
    // DEBUG
    // ================================================================
    [Header("Debug")]
    [SerializeField] private bool showDebugGizmos = false;
    public bool ShowDebugGizmos { get => showDebugGizmos; set => showDebugGizmos = value; }

    [SerializeField, Range(5, 50)]
    private int debugGridSize = 20;
    public int DebugGridSize { get => debugGridSize; set => debugGridSize = value; }

    [SerializeField, Range(0.5f, 5f)]
    private float debugGridSpacing = 2f;
    public float DebugGridSpacing { get => debugGridSpacing; set => debugGridSpacing = value; }

    // ================================================================
    // SHADER PROPERTY ID CACHE
    // ================================================================
    private static readonly int ID_WaveHeight        = Shader.PropertyToID("_WaveHeight");
    private static readonly int ID_WaveSpeed         = Shader.PropertyToID("_WaveSpeed");
    private static readonly int ID_WaveChoppiness    = Shader.PropertyToID("_WaveChoppiness");
    private static readonly int ID_Wave0Dir          = Shader.PropertyToID("_Wave0Dir");
    private static readonly int ID_Wave0Params       = Shader.PropertyToID("_Wave0Params");
    private static readonly int ID_Wave1Dir          = Shader.PropertyToID("_Wave1Dir");
    private static readonly int ID_Wave1Params       = Shader.PropertyToID("_Wave1Params");
    private static readonly int ID_Wave2Dir          = Shader.PropertyToID("_Wave2Dir");
    private static readonly int ID_Wave2Params       = Shader.PropertyToID("_Wave2Params");
    private static readonly int ID_ShallowColor      = Shader.PropertyToID("_ShallowColor");
    private static readonly int ID_DeepColor         = Shader.PropertyToID("_DeepColor");
    private static readonly int ID_AbsorptionCoeff   = Shader.PropertyToID("_AbsorptionCoeff");
    private static readonly int ID_DepthMaxDistance   = Shader.PropertyToID("_DepthMaxDistance");
    private static readonly int ID_DepthFadeDistance  = Shader.PropertyToID("_DepthFadeDistance");
    private static readonly int ID_FoamColor         = Shader.PropertyToID("_FoamColor");
    private static readonly int ID_FoamDepthThreshold = Shader.PropertyToID("_FoamDepthThreshold");
    private static readonly int ID_FoamCrestThreshold = Shader.PropertyToID("_FoamCrestThreshold");
    private static readonly int ID_FoamIntensity     = Shader.PropertyToID("_FoamIntensity");
    private static readonly int ID_FoamScale         = Shader.PropertyToID("_FoamScale");
    private static readonly int ID_SSSColor          = Shader.PropertyToID("_SSSColor");
    private static readonly int ID_SSSIntensity      = Shader.PropertyToID("_SSSIntensity");
    private static readonly int ID_SSSPower          = Shader.PropertyToID("_SSSPower");
    private static readonly int ID_SSSDistortion     = Shader.PropertyToID("_SSSDistortion");
    private static readonly int ID_NormalStrength    = Shader.PropertyToID("_NormalStrength");
    private static readonly int ID_NormalLayer0      = Shader.PropertyToID("_NormalLayer0");
    private static readonly int ID_NormalLayer1      = Shader.PropertyToID("_NormalLayer1");
    private static readonly int ID_NormalLayer2      = Shader.PropertyToID("_NormalLayer2");
    private static readonly int ID_Smoothness        = Shader.PropertyToID("_Smoothness");
    private static readonly int ID_Metallic          = Shader.PropertyToID("_Metallic");
    private static readonly int ID_FresnelPower      = Shader.PropertyToID("_FresnelPower");

    // Internal time
    private float _shaderTime;

    // ================================================================
    // LIFECYCLE
    // ================================================================
    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Debug.LogWarning("[HectonWaterPhysics] Duplicate instance destroyed.");
            DestroyImmediate(gameObject);
            return;
        }
        _instance = this;
    }

    private void OnEnable()
    {
        _instance = this;
        SyncAllToMaterial();
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    private void Update()
    {
        _shaderTime = Time.timeSinceLevelLoad;
        SyncAllToMaterial();
    }

    // ================================================================
    // SYNC ALL PARAMETERS TO MATERIAL
    // ================================================================
    public void SyncAllToMaterial()
    {
        if (oceanMaterial == null) return;

        // Wave globals
        oceanMaterial.SetFloat(ID_WaveHeight, waveHeight);
        oceanMaterial.SetFloat(ID_WaveSpeed, waveSpeed);
        oceanMaterial.SetFloat(ID_WaveChoppiness, waveChoppiness);

        // Wave octaves
        oceanMaterial.SetVector(ID_Wave0Dir, new Vector4(wave0Direction.x, wave0Direction.y, 0, 0));
        oceanMaterial.SetVector(ID_Wave0Params, new Vector4(wave0Amplitude, wave0Wavelength, wave0Steepness, 0));
        oceanMaterial.SetVector(ID_Wave1Dir, new Vector4(wave1Direction.x, wave1Direction.y, 0, 0));
        oceanMaterial.SetVector(ID_Wave1Params, new Vector4(wave1Amplitude, wave1Wavelength, wave1Steepness, 0));
        oceanMaterial.SetVector(ID_Wave2Dir, new Vector4(wave2Direction.x, wave2Direction.y, 0, 0));
        oceanMaterial.SetVector(ID_Wave2Params, new Vector4(wave2Amplitude, wave2Wavelength, wave2Steepness, 0));

        // Color & depth
        oceanMaterial.SetColor(ID_ShallowColor, shallowColor);
        oceanMaterial.SetColor(ID_DeepColor, deepColor);
        oceanMaterial.SetFloat(ID_AbsorptionCoeff, absorptionCoeff);
        oceanMaterial.SetFloat(ID_DepthMaxDistance, depthMaxDistance);
        oceanMaterial.SetFloat(ID_DepthFadeDistance, depthFadeDistance);

        // Foam
        oceanMaterial.SetColor(ID_FoamColor, foamColor);
        oceanMaterial.SetFloat(ID_FoamDepthThreshold, foamDepthThreshold);
        oceanMaterial.SetFloat(ID_FoamCrestThreshold, foamCrestThreshold);
        oceanMaterial.SetFloat(ID_FoamIntensity, foamIntensity);
        oceanMaterial.SetFloat(ID_FoamScale, foamScale);

        // SSS
        oceanMaterial.SetColor(ID_SSSColor, sssColor);
        oceanMaterial.SetFloat(ID_SSSIntensity, sssIntensity);
        oceanMaterial.SetFloat(ID_SSSPower, sssPower);
        oceanMaterial.SetFloat(ID_SSSDistortion, sssDistortion);

        // Normal maps
        oceanMaterial.SetFloat(ID_NormalStrength, normalStrength);
        oceanMaterial.SetVector(ID_NormalLayer0, new Vector4(normalLayer0Scale, normalLayer0SpeedX, normalLayer0SpeedY, normalLayer0Rotation));
        oceanMaterial.SetVector(ID_NormalLayer1, new Vector4(normalLayer1Scale, normalLayer1SpeedX, normalLayer1SpeedY, normalLayer1Rotation));
        oceanMaterial.SetVector(ID_NormalLayer2, new Vector4(normalLayer2Scale, normalLayer2SpeedX, normalLayer2SpeedY, normalLayer2Rotation));

        // PBR
        oceanMaterial.SetFloat(ID_Smoothness, smoothness);
        oceanMaterial.SetFloat(ID_Metallic, metallic);
        oceanMaterial.SetFloat(ID_FresnelPower, fresnelPower);
    }

    // ================================================================
    // CPU GERSTNER — matches shader EXACTLY
    // ================================================================
    private static float3 GerstnerWaveCPU(
        float2 worldXZ,
        float2 direction,
        float amplitude,
        float wavelength,
        float steepness,
        float globalHeight,
        float globalSpeed,
        float globalChop,
        float time)
    {
        float2 D = math.normalize(direction);
        float k = (2.0f * math.PI) / math.max(wavelength, 0.001f);
        float c = math.sqrt(9.81f / k);
        float A = amplitude * globalHeight;
        float Q = steepness * globalChop;

        float phase = k * math.dot(D, worldXZ) - c * k * time * globalSpeed;
        float S = math.sin(phase);
        float C = math.cos(phase);

        float3 displacement;
        displacement.x = -D.x * (Q * A * S);
        displacement.z = -D.y * (Q * A * S);
        displacement.y = A * C;

        return displacement;
    }

    private float3 ComputeTotalDisplacement(float2 worldXZ, float time)
    {
        float3 total = float3.zero;

        total += GerstnerWaveCPU(
            worldXZ, new float2(wave0Direction.x, wave0Direction.y),
            wave0Amplitude, wave0Wavelength, wave0Steepness,
            waveHeight, waveSpeed, waveChoppiness, time);

        total += GerstnerWaveCPU(
            worldXZ, new float2(wave1Direction.x, wave1Direction.y),
            wave1Amplitude, wave1Wavelength, wave1Steepness,
            waveHeight, waveSpeed, waveChoppiness, time);

        total += GerstnerWaveCPU(
            worldXZ, new float2(wave2Direction.x, wave2Direction.y),
            wave2Amplitude, wave2Wavelength, wave2Steepness,
            waveHeight, waveSpeed, waveChoppiness, time);

        return total;
    }

    // ================================================================
    // PUBLIC API
    // ================================================================

    /// <summary>
    /// Get the world-space Y height of the water surface at the given world position.
    /// Uses iterative correction to account for horizontal Gerstner displacement.
    /// </summary>
    public float GetWaveHeight(Vector3 worldPosition)
    {
        float2 xz = new float2(worldPosition.x, worldPosition.z);
        float time = _shaderTime;

        float3 disp = ComputeTotalDisplacement(xz, time);

        // Two correction iterations for accuracy
        float2 correctedXZ = xz - new float2(disp.x, disp.z);
        disp = ComputeTotalDisplacement(correctedXZ, time);

        correctedXZ = xz - new float2(disp.x, disp.z);
        disp = ComputeTotalDisplacement(correctedXZ, time);

        return transform.position.y + disp.y;
    }

    /// <summary>
    /// Get the full 3D displacement vector at a world position.
    /// </summary>
    public Vector3 GetWaveDisplacement(Vector3 worldPosition)
    {
        float2 xz = new float2(worldPosition.x, worldPosition.z);
        float time = _shaderTime;

        float3 disp = ComputeTotalDisplacement(xz, time);
        float2 correctedXZ = xz - new float2(disp.x, disp.z);
        disp = ComputeTotalDisplacement(correctedXZ, time);
        correctedXZ = xz - new float2(disp.x, disp.z);
        disp = ComputeTotalDisplacement(correctedXZ, time);

        return new Vector3(disp.x, disp.y, disp.z);
    }

    /// <summary>
    /// Get the approximate surface normal via finite differences.
    /// </summary>
    public Vector3 GetWaveNormal(Vector3 worldPosition, float sampleOffset = 0.2f)
    {
        float hC = GetWaveHeight(worldPosition);
        float hR = GetWaveHeight(worldPosition + new Vector3(sampleOffset, 0, 0));
        float hF = GetWaveHeight(worldPosition + new Vector3(0, 0, sampleOffset));

        Vector3 tangentX = new Vector3(sampleOffset, hR - hC, 0f);
        Vector3 tangentZ = new Vector3(0f, hF - hC, sampleOffset);

        return Vector3.Cross(tangentZ, tangentX).normalized;
    }

    /// <summary>
    /// Multi-point averaged height for large vessels.
    /// </summary>
    public float GetAveragedWaveHeight(Vector3 center, float sampleRadius, int sampleCount = 4)
    {
        float totalHeight = GetWaveHeight(center);
        float angleStep = 360f / sampleCount;

        for (int i = 0; i < sampleCount; i++)
        {
            float angle = angleStep * i * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(
                Mathf.Cos(angle) * sampleRadius, 0f,
                Mathf.Sin(angle) * sampleRadius);
            totalHeight += GetWaveHeight(center + offset);
        }

        return totalHeight / (sampleCount + 1);
    }

    /// <summary>
    /// Check if a world position is below the water surface.
    /// </summary>
    public bool IsSubmerged(Vector3 worldPosition)
    {
        return worldPosition.y < GetWaveHeight(worldPosition);
    }

    /// <summary>
    /// Get submersion depth (positive = below water).
    /// </summary>
    public float GetSubmersionDepth(Vector3 worldPosition)
    {
        return GetWaveHeight(worldPosition) - worldPosition.y;
    }

    // ================================================================
    // VALIDATION
    // ================================================================
    private void OnValidate()
    {
        wave0Wavelength = Mathf.Max(wave0Wavelength, 0.1f);
        wave1Wavelength = Mathf.Max(wave1Wavelength, 0.1f);
        wave2Wavelength = Mathf.Max(wave2Wavelength, 0.1f);

        if (wave0Direction.sqrMagnitude < 0.001f) wave0Direction = new Vector2(1, 0);
        if (wave1Direction.sqrMagnitude < 0.001f) wave1Direction = new Vector2(0.7f, 0.7f);
        if (wave2Direction.sqrMagnitude < 0.001f) wave2Direction = new Vector2(-0.3f, 0.9f);

        SyncAllToMaterial();
    }

    // ================================================================
    // GIZMOS
    // ================================================================
#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!showDebugGizmos) return;

        float time = Application.isPlaying
            ? _shaderTime
            : (float)UnityEditor.EditorApplication.timeSinceStartup;

        Vector3 center = transform.position;
        int half = debugGridSize / 2;

        Gizmos.color = new Color(0.1f, 0.5f, 0.9f, 0.6f);

        for (int x = -half; x <= half; x++)
        {
            for (int z = -half; z <= half; z++)
            {
                Vector3 worldPos = center + new Vector3(x * debugGridSpacing, 0, z * debugGridSpacing);
                float2 xz = new float2(worldPos.x, worldPos.z);
                float3 disp = ComputeTotalDisplacement(xz, time);

                Vector3 displaced = new Vector3(
                    worldPos.x + disp.x,
                    center.y + disp.y,
                    worldPos.z + disp.z);

                Gizmos.DrawSphere(displaced, 0.08f);
            }
        }
    }
#endif
}
