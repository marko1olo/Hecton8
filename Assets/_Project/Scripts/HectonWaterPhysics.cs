using UnityEngine;
using Unity.Mathematics;

/// <summary>
/// HectonWaterPhysics — CPU-side Gerstner Wave system that mirrors
/// the HectonOcean.shader exactly. Singleton pattern, manages global
/// shader keywords, and provides GetWaveHeight() for gameplay physics.
/// </summary>
[DefaultExecutionOrder(-100)] // Run before boats/players
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
                _instance = FindObjectOfType<HectonWaterPhysics>();
                if (_instance == null)
                {
                    Debug.LogError("[HectonWaterPhysics] No instance found in scene!");
                }
            }
            return _instance;
        }
    }

    // ================================================================
    // WAVE PARAMETERS (must match shader properties exactly)
    // ================================================================
    [Header("Global Wave Parameters")]
    [Range(0f, 5f)]  public float waveHeight     = 1.0f;
    [Range(0f, 5f)]  public float waveSpeed      = 1.0f;
    [Range(0f, 2f)]  public float waveChoppiness  = 0.6f;

    [Header("Wave Octave 0")]
    public Vector2 wave0Direction  = new Vector2(1f, 0f);
    public float   wave0Amplitude  = 1.0f;
    public float   wave0Wavelength = 8.0f;
    [Range(0f, 1f)]
    public float   wave0Steepness  = 0.5f;

    [Header("Wave Octave 1")]
    public Vector2 wave1Direction  = new Vector2(0.7f, 0.7f);
    public float   wave1Amplitude  = 0.5f;
    public float   wave1Wavelength = 4.0f;
    [Range(0f, 1f)]
    public float   wave1Steepness  = 0.4f;

    [Header("Wave Octave 2")]
    public Vector2 wave2Direction  = new Vector2(-0.3f, 0.9f);
    public float   wave2Amplitude  = 0.25f;
    public float   wave2Wavelength = 2.5f;
    [Range(0f, 1f)]
    public float   wave2Steepness  = 0.35f;

    [Header("Target Material (optional — auto-sync)")]
    public Material oceanMaterial;

    // Cached shader property IDs
    private static readonly int ID_WaveHeight     = Shader.PropertyToID("_WaveHeight");
    private static readonly int ID_WaveSpeed      = Shader.PropertyToID("_WaveSpeed");
    private static readonly int ID_WaveChoppiness = Shader.PropertyToID("_WaveChoppiness");
    private static readonly int ID_Wave0Dir       = Shader.PropertyToID("_Wave0Dir");
    private static readonly int ID_Wave0Params    = Shader.PropertyToID("_Wave0Params");
    private static readonly int ID_Wave1Dir       = Shader.PropertyToID("_Wave1Dir");
    private static readonly int ID_Wave1Params    = Shader.PropertyToID("_Wave1Params");
    private static readonly int ID_Wave2Dir       = Shader.PropertyToID("_Wave2Dir");
    private static readonly int ID_Wave2Params    = Shader.PropertyToID("_Wave2Params");

    // Internal time tracking (matches _Time.y in shader)
    private float _shaderTime;

    // ================================================================
    // LIFECYCLE
    // ================================================================
    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Debug.LogWarning("[HectonWaterPhysics] Duplicate instance destroyed.");
            DestroyImmediate(this.gameObject);
            return;
        }
        _instance = this;
    }

    private void OnEnable()
    {
        _instance = this;
        SyncShaderGlobals();
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    private void Update()
    {
        // Track Unity's shader time (_Time.y)
        _shaderTime = Time.timeSinceLevelLoad;

        // Push parameters to GPU every frame (cheap — just uniforms)
        SyncShaderGlobals();
    }

    // ================================================================
    // SYNC SHADER GLOBALS
    // ================================================================
    private void SyncShaderGlobals()
    {
        // Use global shader properties so ALL ocean materials receive them.
        // If you want per-material, use oceanMaterial.SetFloat instead.
        Shader.SetGlobalFloat(ID_WaveHeight,     waveHeight);
        Shader.SetGlobalFloat(ID_WaveSpeed,      waveSpeed);
        Shader.SetGlobalFloat(ID_WaveChoppiness, waveChoppiness);

        Shader.SetGlobalVector(ID_Wave0Dir,    new Vector4(wave0Direction.x, wave0Direction.y, 0, 0));
        Shader.SetGlobalVector(ID_Wave0Params, new Vector4(wave0Amplitude, wave0Wavelength, wave0Steepness, 0));

        Shader.SetGlobalVector(ID_Wave1Dir,    new Vector4(wave1Direction.x, wave1Direction.y, 0, 0));
        Shader.SetGlobalVector(ID_Wave1Params, new Vector4(wave1Amplitude, wave1Wavelength, wave1Steepness, 0));

        Shader.SetGlobalVector(ID_Wave2Dir,    new Vector4(wave2Direction.x, wave2Direction.y, 0, 0));
        Shader.SetGlobalVector(ID_Wave2Params, new Vector4(wave2Amplitude, wave2Wavelength, wave2Steepness, 0));

        // Also push to the specific material if assigned (for Inspector preview)
        if (oceanMaterial != null)
        {
            oceanMaterial.SetFloat(ID_WaveHeight,     waveHeight);
            oceanMaterial.SetFloat(ID_WaveSpeed,      waveSpeed);
            oceanMaterial.SetFloat(ID_WaveChoppiness, waveChoppiness);
            oceanMaterial.SetVector(ID_Wave0Dir,    new Vector4(wave0Direction.x, wave0Direction.y, 0, 0));
            oceanMaterial.SetVector(ID_Wave0Params, new Vector4(wave0Amplitude, wave0Wavelength, wave0Steepness, 0));
            oceanMaterial.SetVector(ID_Wave1Dir,    new Vector4(wave1Direction.x, wave1Direction.y, 0, 0));
            oceanMaterial.SetVector(ID_Wave1Params, new Vector4(wave1Amplitude, wave1Wavelength, wave1Steepness, 0));
            oceanMaterial.SetVector(ID_Wave2Dir,    new Vector4(wave2Direction.x, wave2Direction.y, 0, 0));
            oceanMaterial.SetVector(ID_Wave2Params, new Vector4(wave2Amplitude, wave2Wavelength, wave2Steepness, 0));
        }
    }

    // ================================================================
    // GERSTNER WAVE — CPU (matches shader EXACTLY)
    // ================================================================

    /// <summary>
    /// Compute a single Gerstner wave octave displacement.
    /// Returns the full float3 displacement (x = horizontal X, y = vertical, z = horizontal Z).
    /// </summary>
    private static float3 GerstnerWaveCPU(
        float2 worldXZ,
        float2 direction,
        float  amplitude,
        float  wavelength,
        float  steepness,
        float  globalHeight,
        float  globalSpeed,
        float  globalChop,
        float  time)
    {
        float2 D = math.normalize(direction);
        float  k = (2.0f * math.PI) / math.max(wavelength, 0.001f);
        float  c = math.sqrt(9.81f / k); // deep water dispersion
        float  A = amplitude * globalHeight;
        float  Q = steepness * globalChop;

        float phase = k * math.dot(D, worldXZ) - c * k * time * globalSpeed;
        float S = math.sin(phase);
        float C = math.cos(phase);

        float3 displacement;
        displacement.x = -D.x * (Q * A * S);
        displacement.z = -D.y * (Q * A * S);
        displacement.y = A * C;

        return displacement;
    }

    /// <summary>
    /// Compute total displacement from all 3 Gerstner octaves.
    /// </summary>
    private float3 ComputeTotalDisplacement(float2 worldXZ, float time)
    {
        float3 total = float3.zero;

        // Octave 0
        total += GerstnerWaveCPU(
            worldXZ, new float2(wave0Direction.x, wave0Direction.y),
            wave0Amplitude, wave0Wavelength, wave0Steepness,
            waveHeight, waveSpeed, waveChoppiness, time);

        // Octave 1
        total += GerstnerWaveCPU(
            worldXZ, new float2(wave1Direction.x, wave1Direction.y),
            wave1Amplitude, wave1Wavelength, wave1Steepness,
            waveHeight, waveSpeed, waveChoppiness, time);

        // Octave 2
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
    /// This accounts for the water plane's Y position (transform.position.y) plus
    /// Gerstner wave displacement. Use this for buoyancy / floating.
    /// </summary>
    /// <param name="worldPosition">The world-space position to sample (only X and Z are used).</param>
    /// <returns>The Y height of the ocean surface at that XZ coordinate.</returns>
    public float GetWaveHeight(Vector3 worldPosition)
    {
        float2 xz = new float2(worldPosition.x, worldPosition.z);
        float  time = _shaderTime; // matches _Time.y in shader

        // The Gerstner wave displaces horizontally too, which means
        // the sample point shifts. For accurate physics, we iterate
        // to converge on the correct displaced position (2 iterations is plenty).
        float3 disp = ComputeTotalDisplacement(xz, time);

        // First correction pass: sample at displaced position
        float2 correctedXZ = xz - new float2(disp.x, disp.z);
        disp = ComputeTotalDisplacement(correctedXZ, time);

        // Second correction pass (high precision)
        correctedXZ = xz - new float2(disp.x, disp.z);
        disp = ComputeTotalDisplacement(correctedXZ, time);

        // Water plane base height + wave vertical displacement
        return transform.position.y + disp.y;
    }

    /// <summary>
    /// Get the full 3D displacement vector at a world position.
    /// Useful for aligning objects to the wave surface including horizontal shift.
    /// </summary>
    public Vector3 GetWaveDisplacement(Vector3 worldPosition)
    {
        float2 xz = new float2(worldPosition.x, worldPosition.z);
        float  time = _shaderTime;

        float3 disp = ComputeTotalDisplacement(xz, time);
        float2 correctedXZ = xz - new float2(disp.x, disp.z);
        disp = ComputeTotalDisplacement(correctedXZ, time);
        correctedXZ = xz - new float2(disp.x, disp.z);
        disp = ComputeTotalDisplacement(correctedXZ, time);

        return new Vector3(disp.x, disp.y, disp.z);
    }

    /// <summary>
    /// Get the approximate surface normal at a world position using finite differences.
    /// Useful for tilting boats / players to match wave slope.
    /// </summary>
    /// <param name="worldPosition">Sample point.</param>
    /// <param name="sampleOffset">Finite difference step size (default 0.2m).</param>
    public Vector3 GetWaveNormal(Vector3 worldPosition, float sampleOffset = 0.2f)
    {
        float hC = GetWaveHeight(worldPosition);
        float hR = GetWaveHeight(worldPosition + new Vector3(sampleOffset, 0, 0));
        float hF = GetWaveHeight(worldPosition + new Vector3(0, 0, sampleOffset));

        // Tangent vectors
        Vector3 tangentX = new Vector3(sampleOffset, hR - hC, 0f);
        Vector3 tangentZ = new Vector3(0f, hF - hC, sampleOffset);

        Vector3 normal = Vector3.Cross(tangentZ, tangentX).normalized;
        return normal;
    }

    /// <summary>
    /// Multi-point buoyancy helper. Returns an averaged height from N sample points
    /// around a center position. Useful for large boats.
    /// </summary>
    /// <param name="center">Center world position.</param>
    /// <param name="sampleRadius">Radius of sample ring.</param>
    /// <param name="sampleCount">Number of samples (4-8 typical).</param>
    public float GetAveragedWaveHeight(Vector3 center, float sampleRadius, int sampleCount = 4)
    {
        float totalHeight = GetWaveHeight(center);
        float angleStep = 360f / sampleCount;

        for (int i = 0; i < sampleCount; i++)
        {
            float angle = angleStep * i * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(
                Mathf.Cos(angle) * sampleRadius,
                0f,
                Mathf.Sin(angle) * sampleRadius);
            totalHeight += GetWaveHeight(center + offset);
        }

        return totalHeight / (sampleCount + 1);
    }

    /// <summary>
    /// Check if a world position is submerged (below the water surface).
    /// </summary>
    public bool IsSubmerged(Vector3 worldPosition)
    {
        return worldPosition.y < GetWaveHeight(worldPosition);
    }

    /// <summary>
    /// Get submersion depth (positive = below water, negative = above).
    /// Useful for drag/buoyancy force calculations.
    /// </summary>
    public float GetSubmersionDepth(Vector3 worldPosition)
    {
        return GetWaveHeight(worldPosition) - worldPosition.y;
    }

    // ================================================================
    // GIZMOS (Editor visualization)
    // ================================================================
    #if UNITY_EDITOR
    [Header("Debug")]
    public bool showDebugGizmos = false;
    [Range(5, 50)] public int debugGridSize = 20;
    [Range(0.5f, 5f)] public float debugGridSpacing = 2f;

    private void OnDrawGizmosSelected()
    {
        if (!showDebugGizmos) return;

        float time = Application.isPlaying ? _shaderTime : (float)UnityEditor.EditorApplication.timeSinceStartup;

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

    // ================================================================
    // VALIDATION
    // ================================================================
    private void OnValidate()
    {
        // Clamp wavelengths to prevent division by zero
        wave0Wavelength = Mathf.Max(wave0Wavelength, 0.1f);
        wave1Wavelength = Mathf.Max(wave1Wavelength, 0.1f);
        wave2Wavelength = Mathf.Max(wave2Wavelength, 0.1f);

        // Ensure directions aren't zero
        if (wave0Direction.sqrMagnitude < 0.001f) wave0Direction = new Vector2(1, 0);
        if (wave1Direction.sqrMagnitude < 0.001f) wave1Direction = new Vector2(0.7f, 0.7f);
        if (wave2Direction.sqrMagnitude < 0.001f) wave2Direction = new Vector2(-0.3f, 0.9f);

        // Re-sync on Inspector change
        SyncShaderGlobals();
    }
}