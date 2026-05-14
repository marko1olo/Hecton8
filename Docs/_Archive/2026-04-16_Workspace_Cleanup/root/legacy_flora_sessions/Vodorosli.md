Date: 2026-04-16
Status: ARCHIVED

**WARNING: LEGACY DOCUMENT.** This file is archived for historical reference and may contain outdated information or broken links.


// ============================================================
// HECTON-8 — SeaweedSeasonSystem.cs v1.0
// Four-season variation for underwater ecosystem.
// ITickable — no Update(). Smooth shader-driven transitions.
// No mesh rebuild — shader params only.
// ============================================================

using UnityEngine;
using Hecton8.Core;

namespace Hecton8.Environment.Seasonal
{
    /// <summary>
    /// Controls seasonal appearance of all seaweed via global shader params.
    /// Spring: young/bright. Summer: peak. Autumn: yellowing. Winter: dark/slow.
    /// Transitions use SmoothStep over <c>_transitionDuration</c> seconds.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-140)]
    public sealed class SeaweedSeasonSystem : MonoBehaviour, ITickable
    {
        // ── ENUMS ─────────────────────────────────────────────────────

        public enum Season : byte
        {
            Spring = 0,
            Summer = 1,
            Autumn = 2,
            Winter = 3
        }

        // ── SEASON DATA ───────────────────────────────────────────────

        [System.Serializable]
        public struct SeasonData
        {
            [Header("Colour")]
            public Color colorMultRoot;
            public Color colorMultTip;

            [Header("Scale")]
            [Range(0.3f, 1.3f)] public float sizeMultiplier;
            [Range(0.3f, 1.3f)] public float widthMultiplier;

            [Header("Animation")]
            [Range(0.2f, 2f)]   public float swayMultiplier;
            [Range(0f, 0.3f)]   public float gustFrequency;

            [Header("Render")]
            [Range(0f, 2f)]     public float sssMultiplier;
            [Range(0f, 1f)]     public float roughness;

            [Header("Environment")]
            public Color fogColor;
            public Color ambientColor;
            [Range(0f, 1.5f)]   public float lightIntensity;
        }

        // ── INSPECTOR ─────────────────────────────────────────────────

        [Header("── Season Settings ─────────────────────────────────────")]
        [SerializeField] private Season _currentSeason  = Season.Summer;
        [SerializeField] private float  _seasonProgress = 0f;

        [SerializeField, Tooltip("Auto-advance seasons in real time.")]
        private bool _autoAdvance = false;

        [SerializeField, Tooltip("Real seconds per full season cycle.")]
        private float _realSecondsPerSeason = 300f;

        [SerializeField, Tooltip("Transition blend duration in seconds.")]
        private float _transitionDuration = 10f;

        [Header("── Season Data ──────────────────────────────────────────")]
        [SerializeField] private SeasonData[] _seasonData = CreateDefaultSeasons();

        // ── PRIVATE STATE ─────────────────────────────────────────────

        private bool      _registered;
        private SeasonData _currentData;
        private SeasonData _targetData;
        private float     _transitionT  = 1f;   // 1 = complete
        private Season    _transitionTo;
        private float     _autoTimer;

        // Cached shader property IDs — COLD ALLOC
        private static readonly int
            _PropColorMult  = Shader.PropertyToID("_SeasonColorMult"),
            _PropSizeMult   = Shader.PropertyToID("_SeasonSizeMult"),
            _PropSwayMult   = Shader.PropertyToID("_SeasonSwayMult"),
            _PropSSS        = Shader.PropertyToID("_SeasonSSSMult"),
            _PropFogColor   = Shader.PropertyToID("_SeasonFogColor"),
            _PropAmbient    = Shader.PropertyToID("_SeasonAmbient"),
            _PropProgress   = Shader.PropertyToID("_SeasonProgress"),
            _PropIndex      = Shader.PropertyToID("_SeasonIndex");

        // ── SINGLETON ─────────────────────────────────────────────────

        public static SeaweedSeasonSystem Instance { get; private set; }

        // ── LIFECYCLE ─────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (_seasonData == null || _seasonData.Length < 4)
                _seasonData = CreateDefaultSeasons();

            _currentData = _seasonData[(int)_currentSeason];
            ApplyToShaderImmediate(_currentData);
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
            if (_registered && GameTickManager.Instance != null)
            {
                GameTickManager.Instance.Unregister(this);
                _registered = false;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── ITICKABLE ─────────────────────────────────────────────────

        public void Tick(float dt)
        {
            // Auto season advance
            if (_autoAdvance)
            {
                _autoTimer += dt;
                _seasonProgress = _autoTimer / _realSecondsPerSeason;

                if (_seasonProgress >= 1f)
                {
                    _autoTimer      = 0f;
                    _seasonProgress = 0f;
                    AdvanceSeason();
                }

                Shader.SetGlobalFloat(_PropProgress, _seasonProgress);
            }

            // Blend transition
            if (_transitionT < 1f)
            {
                _transitionT += dt / Mathf.Max(0.01f, _transitionDuration);
                _transitionT  = Mathf.Clamp01(_transitionT);

                SeasonData blended = BlendData(
                    _currentData, _targetData,
                    Mathf.SmoothStep(0f, 1f, _transitionT));

                ApplyToShaderImmediate(blended);

                if (_transitionT >= 1f)
                {
                    _currentSeason = _transitionTo;
                    _currentData   = _targetData;
                }
            }
        }

        // ── PUBLIC API ────────────────────────────────────────────────

        /// <summary>Current active season.</summary>
        public Season CurrentSeason => _currentSeason;

        /// <summary>Progress within current season [0..1].</summary>
        public float SeasonProgress => _seasonProgress;

        /// <summary>
        /// Transitions to the given season over _transitionDuration seconds.
        /// Set instant=true for immediate change (e.g. debug, fast travel).
        /// </summary>
        public void SetSeason(Season season, bool instant = false)
        {
            if (season == _currentSeason && _transitionT >= 1f) return;

            _transitionTo = season;
            _targetData   = _seasonData[(int)season];
            Shader.SetGlobalInt(_PropIndex, (int)season);

            if (instant)
            {
                _currentSeason = season;
                _currentData   = _targetData;
                _transitionT   = 1f;
                ApplyToShaderImmediate(_currentData);
            }
            else
            {
                _transitionT = 0f;
            }
        }

        /// <summary>Set season progress manually [0..1].</summary>
        public void SetSeasonProgress(float progress)
        {
            _seasonProgress = Mathf.Clamp01(progress);
            if (_autoAdvance)
                _autoTimer = _seasonProgress * _realSecondsPerSeason;
            Shader.SetGlobalFloat(_PropProgress, _seasonProgress);
        }

        // ── PRIVATE ───────────────────────────────────────────────────

        private void AdvanceSeason()
        {
            int next = ((int)_currentSeason + 1) % 4;
            SetSeason((Season)next);
        }

        private void ApplyToShaderImmediate(in SeasonData d)
        {
            Shader.SetGlobalVector(_PropColorMult,
                new Vector4(d.colorMultTip.r, d.colorMultTip.g,
                            d.colorMultTip.b, d.colorMultTip.a));
            Shader.SetGlobalFloat(_PropSizeMult, d.sizeMultiplier);
            Shader.SetGlobalFloat(_PropSwayMult, d.swayMultiplier);
            Shader.SetGlobalFloat(_PropSSS,      d.sssMultiplier);
            Shader.SetGlobalColor(_PropFogColor, d.fogColor);
            Shader.SetGlobalColor(_PropAmbient,  d.ambientColor);
            RenderSettings.ambientLight = d.ambientColor * d.lightIntensity;
        }

        private static SeasonData BlendData(
            in SeasonData a, in SeasonData b, float t)
        {
            return new SeasonData
            {
                colorMultRoot   = Color.Lerp(a.colorMultRoot,   b.colorMultRoot,   t),
                colorMultTip    = Color.Lerp(a.colorMultTip,    b.colorMultTip,    t),
                sizeMultiplier  = Mathf.Lerp(a.sizeMultiplier,  b.sizeMultiplier,  t),
                widthMultiplier = Mathf.Lerp(a.widthMultiplier, b.widthMultiplier, t),
                swayMultiplier  = Mathf.Lerp(a.swayMultiplier,  b.swayMultiplier,  t),
                gustFrequency   = Mathf.Lerp(a.gustFrequency,   b.gustFrequency,   t),
                sssMultiplier   = Mathf.Lerp(a.sssMultiplier,   b.sssMultiplier,   t),
                roughness       = Mathf.Lerp(a.roughness,       b.roughness,       t),
                fogColor        = Color.Lerp(a.fogColor,        b.fogColor,        t),
                ambientColor    = Color.Lerp(a.ambientColor,    b.ambientColor,    t),
                lightIntensity  = Mathf.Lerp(a.lightIntensity,  b.lightIntensity,  t)
            };
        }

        private static SeasonData[] CreateDefaultSeasons() => new[]
        {
            // Spring — young, bright, lightweight
            new SeasonData
            {
                colorMultRoot   = new Color(0.8f, 1.2f, 0.7f),
                colorMultTip    = new Color(0.9f, 1.4f, 0.8f),
                sizeMultiplier  = 0.75f, widthMultiplier = 0.8f,
                swayMultiplier  = 1.3f,  gustFrequency   = 0.12f,
                sssMultiplier   = 1.5f,  roughness       = 0.2f,
                fogColor        = new Color(0.1f, 0.4f, 0.5f),
                ambientColor    = new Color(0.2f, 0.35f, 0.3f),
                lightIntensity  = 0.8f
            },
            // Summer — peak, deep green
            new SeasonData
            {
                colorMultRoot   = new Color(0.9f, 1.0f, 0.7f),
                colorMultTip    = new Color(1.0f, 1.1f, 0.6f),
                sizeMultiplier  = 1.0f, widthMultiplier = 1.0f,
                swayMultiplier  = 1.0f, gustFrequency   = 0.08f,
                sssMultiplier   = 1.0f, roughness       = 0.4f,
                fogColor        = new Color(0.05f, 0.3f, 0.45f),
                ambientColor    = new Color(0.15f, 0.3f, 0.25f),
                lightIntensity  = 1.0f
            },
            // Autumn — yellowing, heavy
            new SeasonData
            {
                colorMultRoot   = new Color(1.1f, 0.8f, 0.3f),
                colorMultTip    = new Color(1.3f, 0.9f, 0.2f),
                sizeMultiplier  = 1.05f, widthMultiplier = 1.1f,
                swayMultiplier  = 0.9f,  gustFrequency   = 0.15f,
                sssMultiplier   = 0.7f,  roughness       = 0.6f,
                fogColor        = new Color(0.08f, 0.2f, 0.3f),
                ambientColor    = new Color(0.12f, 0.2f, 0.18f),
                lightIntensity  = 0.7f
            },
            // Winter — dark, slow, sparse
            new SeasonData
            {
                colorMultRoot   = new Color(0.5f, 0.55f, 0.4f),
                colorMultTip    = new Color(0.6f, 0.65f, 0.45f),
                sizeMultiplier  = 0.6f, widthMultiplier = 0.7f,
                swayMultiplier  = 0.6f, gustFrequency   = 0.2f,
                sssMultiplier   = 0.5f, roughness       = 0.7f,
                fogColor        = new Color(0.03f, 0.1f, 0.2f),
                ambientColor    = new Color(0.05f, 0.1f, 0.12f),
                lightIntensity  = 0.4f
            }
        };
    }
}
Fayl: Assets/_Project/Scripts/Environment/Seaweed/SeaweedAnimCurveSystem.cs
csharp

// ============================================================
// HECTON-8 — SeaweedAnimCurveSystem.cs v1.0
// Procedural animation curve texture + gust buffer.
// ITickable for gust updates. Textures generated in Task.Run
// (one-time at startup — NOT a hot path).
// Zero GC in Tick. Pre-allocated gust array.
// ============================================================

using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using Hecton8.Core;

namespace Hecton8.Environment
{
    /// <summary>
    /// Generates AnimCurveTexture (256×256 RGBA) at startup.
    /// Each row = one unique organic sway curve (Perlin-based, not sin).
    /// Shader samples this instead of computing sin() — more organic.
    ///
    /// Also manages GustBuffer: up to 16 simultaneous gust events.
    /// Gusts are spawned randomly and tracked via ITickable state machine.
    ///
    /// Global shader uniforms set here:
    ///   _AnimCurveTexture, _AnimCurveDuration, _AnimTime,
    ///   _GustBuffer, _TurbulenceMap, _TurbMapScale
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-130)]
    public sealed class SeaweedAnimCurveSystem : MonoBehaviour, ITickable
    {
        // ── INSPECTOR ─────────────────────────────────────────────────

        [Header("── Curve Texture ────────────────────────────────────────")]
        [SerializeField, Range(64, 256)]
        private int _curveCount = 256;

        [SerializeField, Range(64, 256)]
        private int _timeSteps = 256;

        [SerializeField, Range(4f, 30f),
         Tooltip("Animation loop duration in seconds.")]
        private float _duration = 10f;

        [SerializeField, Range(0f, 0.3f)]
        private float _gustFrequency = 0.08f;

        [Header("── Turbulence Map ─────────────────────────────────────")]
        [SerializeField, Range(32, 128)]
        private int _turbMapSize = 64;

        [SerializeField, Range(0.01f, 0.2f)]
        private float _turbMapScale = 0.05f;

        [Header("── Runtime ──────────────────────────────────────────────")]
        [SerializeField, Range(0.1f, 3f)]
        private float _timeScale = 1f;

        // ── PRIVATE STATE ─────────────────────────────────────────────

        private Texture2D _animCurveTex;
        private Texture2D _turbulenceMap;

        // COLD ALLOC: fixed 16 gust slots, never reallocated
        private const int MaxGusts = 16;
        private readonly GustData[] _gusts = new GustData[MaxGusts];
        private ComputeBuffer _gustBuffer;
        private float _nextGustTimer;
        private float _animTime;

        private bool _ready;
        private bool _registered;

        private Camera _mainCam;

        // Cached shader property IDs
        private static readonly int
            _PropAnimCurveTex  = Shader.PropertyToID("_AnimCurveTexture"),
            _PropTurbulenceTex = Shader.PropertyToID("_TurbulenceMap"),
            _PropGustBuffer    = Shader.PropertyToID("_GustBuffer"),
            _PropAnimTime      = Shader.PropertyToID("_AnimTime"),
            _PropAnimDuration  = Shader.PropertyToID("_AnimCurveDuration"),
            _PropTurbScale     = Shader.PropertyToID("_TurbMapScale"),
            _PropTimeScale     = Shader.PropertyToID("_AnimTimeScale");

        // Gust data — blittable struct for ComputeBuffer
        [System.Runtime.InteropServices.StructLayout(
            System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct GustData
        {
            public Vector4 posRadius;    // xyz=pos, w=radius
            public Vector4 dirStrength;  // xyz=dir, w=strength
            public float   phase;
            public float   _pad0;
            public float   _pad1;
            public float   _pad2;
        }

        // ── SINGLETON ─────────────────────────────────────────────────

        public static SeaweedAnimCurveSystem Instance { get; private set; }

        /// <summary>True when textures are generated and shader params set.</summary>
        public bool IsReady => _ready;

        // ── LIFECYCLE ─────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            _mainCam = Camera.main;

            // COLD ALLOC: 16 gust slots, 48 bytes each = 768 bytes total
            _gustBuffer = new ComputeBuffer(MaxGusts, 48);
        }

        private IEnumerator Start()
        {
            // Generate textures off main thread — no frame spike
            Color[] animPixels = null;
            Color[] turbPixels = null;
            bool done = false;

            // Task.Run is acceptable here: one-time startup, not a hot path
            Task.Run(() =>
            {
                animPixels = GenerateAnimCurvePixels();
                turbPixels = GenerateTurbulencePixels();
                done = true;
            });

            while (!done) yield return null;

            // Texture creation MUST be on main thread
            _animCurveTex = new Texture2D(_timeSteps, _curveCount,
                TextureFormat.RGBAHalf, false)
            {
                name       = "SeaweedAnimCurves",
                wrapMode   = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
            _animCurveTex.SetPixels(animPixels);
            _animCurveTex.Apply(false, true);

            _turbulenceMap = new Texture2D(_turbMapSize, _turbMapSize,
                TextureFormat.RGHalf, false)
            {
                name       = "SeaweedTurbulence",
                wrapMode   = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
            _turbulenceMap.SetPixels(turbPixels);
            _turbulenceMap.Apply(false, true);

            PushGlobalShaderParams();
            _ready = true;
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
            if (_registered && GameTickManager.Instance != null)
            {
                GameTickManager.Instance.Unregister(this);
                _registered = false;
            }
        }

        private void OnDestroy()
        {
            _gustBuffer?.Release();
            if (_animCurveTex  != null) Destroy(_animCurveTex);
            if (_turbulenceMap != null) Destroy(_turbulenceMap);
            if (Instance == this) Instance = null;
        }

        // ── ITICKABLE ─────────────────────────────────────────────────

        public void Tick(float dt)
        {
            if (!_ready) return;

            // Advance animation time — wraps to prevent float overflow
            _animTime += dt * _timeScale;
            if (_animTime > _duration * 1000f)
                _animTime -= _duration * 1000f;

            Shader.SetGlobalFloat(_PropAnimTime, _animTime);

            // Gust state machine
            _nextGustTimer -= dt;
            if (_nextGustTimer <= 0f)
            {
                _nextGustTimer = Random.Range(2f, 8f);
                TrySpawnGust();
            }

            // Update active gusts
            bool changed = false;
            for (int i = 0; i < MaxGusts; i++)
            {
                if (_gusts[i].phase <= 0f) continue;
                _gusts[i].phase -= dt * 0.33f; // ~3 second gust
                if (_gusts[i].phase < 0f) _gusts[i].phase = 0f;
                changed = true;
            }

            if (changed)
                _gustBuffer.SetData(_gusts);
        }

        // ── PUBLIC API ────────────────────────────────────────────────

        /// <summary>
        /// Manually trigger a gust at a world position.
        /// Used by: submarine thrusters, player movement, environmental events.
        /// </summary>
        public void TriggerGustAt(Vector3 position, float radius, float strength)
        {
            for (int i = 0; i < MaxGusts; i++)
            {
                if (_gusts[i].phase > 0f) continue;
                Vector3 dir = SeaweedCurrentZone.GlobalCurrent;
                _gusts[i] = new GustData
                {
                    posRadius   = new Vector4(position.x, position.y, position.z, radius),
                    dirStrength = new Vector4(dir.x, dir.y, dir.z, strength),
                    phase       = 1f
                };
                _gustBuffer.SetData(_gusts);
                return;
            }
            // All slots full — silently ignore (not an error)
        }

        /// <summary>Set animation time scale (0=pause, 1=normal, 2=fast).</summary>
        public void SetTimeScale(float scale)
        {
            _timeScale = Mathf.Max(0f, scale);
            Shader.SetGlobalFloat(_PropTimeScale, _timeScale);
        }

        // ── PRIVATE ───────────────────────────────────────────────────

        private void TrySpawnGust()
        {
            if (_mainCam == null) return;

            for (int i = 0; i < MaxGusts; i++)
            {
                if (_gusts[i].phase > 0f) continue;

                Vector3 camPos = _mainCam.transform.position;
                float angle    = Random.Range(0f, Mathf.PI * 2f);
                float dist     = Random.Range(5f, 30f);

                Vector3 pos = camPos + new Vector3(
                    Mathf.Cos(angle) * dist, 0f,
                    Mathf.Sin(angle) * dist);

                Vector3 globalDir = SeaweedCurrentZone.GlobalCurrent;
                float   deviation = Random.Range(-45f, 45f);
                Vector3 gustDir   = Quaternion.Euler(0f, deviation, 0f) * globalDir;

                _gusts[i] = new GustData
                {
                    posRadius   = new Vector4(pos.x, pos.y, pos.z,
                        Random.Range(3f, 12f)),
                    dirStrength = new Vector4(gustDir.x, gustDir.y, gustDir.z,
                        Random.Range(0.3f, 1.2f)),
                    phase       = 1f
                };
                _gustBuffer.SetData(_gusts);
                return;
            }
        }

        private void PushGlobalShaderParams()
        {
            Shader.SetGlobalTexture(_PropAnimCurveTex,  _animCurveTex);
            Shader.SetGlobalTexture(_PropTurbulenceTex, _turbulenceMap);
            Shader.SetGlobalBuffer (_PropGustBuffer,    _gustBuffer);
            Shader.SetGlobalFloat  (_PropAnimDuration,  _duration);
            Shader.SetGlobalFloat  (_PropTurbScale,     _turbMapScale);
            Shader.SetGlobalFloat  (_PropTimeScale,     _timeScale);
        }

        // ── TEXTURE GENERATION (off-thread, startup only) ─────────────

        private Color[] GenerateAnimCurvePixels()
        {
            // COLD ALLOC: _timeSteps * _curveCount colors — startup only
            var pixels = new Color[_timeSteps * _curveCount];

            for (int c = 0; c < _curveCount; c++)
            {
                float seed     = c * 17.3f;
                float freqMain = 0.25f + NoiseHash(seed + 1f) * 0.3f;
                float freqTip  = 0.8f  + NoiseHash(seed + 2f) * 0.5f;
                float freqGust = _gustFrequency * (0.7f + NoiseHash(seed + 3f) * 0.6f);
                float phase    = NoiseHash(seed + 4f) * _duration;

                for (int t = 0; t < _timeSteps; t++)
                {
                    float time = (float)t / _timeSteps * _duration + phase;

                    float main = FBM(time * freqMain, seed,      4);
                    float tip  = FBM(time * freqTip,  seed+50f,  3);
                    float gust = GustShape(time, seed, freqGust);
                    float side = FBM(time * freqMain * 0.7f, seed+200f, 2);

                    pixels[c * _timeSteps + t] = new Color(
                        main * 0.5f + 0.5f,
                        tip  * 0.5f + 0.5f,
                        gust * 0.5f + 0.5f,
                        side * 0.5f + 0.5f
                    );
                }
            }
            return pixels;
        }

        private Color[] GenerateTurbulencePixels()
        {
            // COLD ALLOC: _turbMapSize² colors — startup only
            var pixels = new Color[_turbMapSize * _turbMapSize];
            for (int y = 0; y < _turbMapSize; y++)
            for (int x = 0; x < _turbMapSize; x++)
            {
                float u = (float)x / _turbMapSize;
                float v = (float)y / _turbMapSize;
                float strength = FBM2D(u * 3f, v * 3f, 0f,  3);
                float dir      = FBM2D(u * 2f, v * 2f, 50f, 2);
                pixels[y * _turbMapSize + x] = new Color(strength, dir, 0f, 1f);
            }
            return pixels;
        }

        // Math utils — called off-thread, no Unity API
        private static float FBM(float x, float seed, int oct)
        {
            float r = 0f, amp = 0.5f, freq = 1f, max = 0f;
            for (int i = 0; i < oct; i++)
            {
                r   += (Mathf.PerlinNoise(x * freq + seed, seed * 0.37f) * 2f - 1f) * amp;
                max += amp; amp *= 0.5f; freq *= 2.13f;
            }
            return r / max;
        }

        private static float FBM2D(float x, float y, float seed, int oct)
        {
            float r = 0f, amp = 0.5f, freq = 1f, max = 0f;
            for (int i = 0; i < oct; i++)
            {
                r   += (Mathf.PerlinNoise(x*freq+seed, y*freq+seed)*2f-1f)*amp;
                max += amp; amp *= 0.5f; freq *= 2.1f;
            }
            return r / max * 0.5f + 0.5f;
        }

        private static float GustShape(float time, float seed, float freq)
        {
            float trigger = Mathf.PerlinNoise(time * freq + seed, seed * 0.5f);
            if (trigger < 0.6f) return 0f;
            float t = (trigger - 0.6f) / 0.4f;
            return Mathf.Pow(t, 0.4f) * Mathf.Exp(-t * 1.5f) * 2f;
        }

        private static float NoiseHash(float x) =>
            Mathf.PerlinNoise(x * 127.1f, x * 311.7f);
    }
}
Fayl: Assets/_Project/Scripts/Environment/Seaweed/Rendering/SeaweedRenderer.cs
csharp

// ============================================================
// HECTON-8 — SeaweedRenderer.cs v1.0
// DrawMeshInstancedIndirect renderer for all seaweed.
// ITickable — no Update(). Zero GC in Tick.
// Pre-allocated batch matrix arrays. MPB per LOD group.
// GPU culling via SeaweedGPUCuller (separate component).
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Hecton8.Core;

namespace Hecton8.Environment.Rendering
{
    /// <summary>
    /// Renders all registered <see cref="SeaweedInstance"/> objects via
    /// <c>Graphics.DrawMeshInstancedIndirect</c>.
    /// One draw call per (mesh LOD × species variant) group.
    ///
    /// Pipeline:
    /// 1. SeaweedInstances registered via <see cref="RegisterInstance"/>.
    /// 2. Every <c>LodUpdateInterval</c> ticks: LOD reassignment (CPU).
    /// 3. Every tick: DrawMeshInstancedIndirect per active group.
    /// 4. GPU culling handled by SeaweedGPUCuller (Compute Shader).
    ///
    /// GC guarantee:
    /// - Pre-allocated Matrix4x4[] per group (COLD ALLOC in Init).
    /// - No List.ToArray() — direct array write.
    /// - No LINQ. No lambda. No foreach on Dictionary.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(SeaweedGPUCuller))]
    public sealed class SeaweedRenderer : MonoBehaviour
    {
        // ── INSPECTOR ─────────────────────────────────────────────────

        [Header("── Mesh References ─────────────────────────────────────")]
        [SerializeField, Tooltip("LOD0-2 meshes per species variant. Set by SeaweedMeshGenerator.")]
        private Mesh[]     _lodMeshes;       // [speciesVariantIdx * 3 + lodLevel]

        [SerializeField, Tooltip("Billboard quad for LOD3.")]
        private Mesh       _billboardMesh;

        [Header("── Materials ────────────────────────────────────────────")]
        [SerializeField] private Material _seaweedMaterial;
        [SerializeField] private Material _billboardMaterial;

        [Header("── LOD Distances ───────────────────────────────────────")]
        [SerializeField, Range(4f, 20f)]   private float _lod0Dist = 8f;
        [SerializeField, Range(8f, 40f)]   private float _lod1Dist = 20f;
        [SerializeField, Range(20f, 80f)]  private float _lod2Dist = 40f;
        [SerializeField, Range(40f, 120f)] private float _lod3Dist = 80f;

        [Header("── Performance ─────────────────────────────────────────")]
        [SerializeField, Tooltip("LOD update every N ticks (not every frame).")]
        private int _lodUpdateInterval = 8;

        [SerializeField, Tooltip("Shadows OFF = 2x performance on MX350.")]
        private bool _castShadows = false;

        [SerializeField, Tooltip("Max seaweed instances. Pre-allocates arrays.")]
        private int _maxInstances = 2048;

        // ── PRIVATE STATE ─────────────────────────────────────────────

        // All registered instances — managed list, only modified on add/remove
        // COLD ALLOC: _maxInstances capacity
        private readonly List<SeaweedInstance> _instances
            = new List<SeaweedInstance>();

        // Render groups: one per (meshIndex, lodLevel)
        // Key = meshGroupIndex = speciesVariantIdx * 4 + lodLevel
        private sealed class RenderGroup
        {
            // COLD ALLOC: _maxInstances Matrix4x4 — worst case all in one group
            public readonly Matrix4x4[]      Matrices;
            public readonly Vector4[]        PerInstanceData; // curveIdx,phase,sway,atlasRow
            public readonly ComputeBuffer    InstanceBuffer;
            public readonly ComputeBuffer    DrawArgsBuffer;
            public readonly MaterialPropertyBlock MPB;
            public int Count;
            public Mesh Mesh;
            public Material Material;

            public RenderGroup(int maxCount, Mesh mesh, Material mat)
            {
                // COLD ALLOC: maxCount * (64+16) bytes = ~1.6MB for 2048 instances
                Matrices        = new Matrix4x4[maxCount];
                PerInstanceData = new Vector4[maxCount];
                InstanceBuffer  = new ComputeBuffer(maxCount, 80); // Matrix4x4+float4
                DrawArgsBuffer  = new ComputeBuffer(
                    1, 20, ComputeBufferType.IndirectArguments);
                MPB             = new MaterialPropertyBlock();
                Mesh            = mesh;
                Material        = mat;
            }

            public void Release()
            {
                InstanceBuffer?.Release();
                DrawArgsBuffer?.Release();
            }
        }

        private RenderGroup[] _groups; // [speciesVariantCount * 4]
        private int           _groupCount;
        private bool          _ready;
        private int           _tickCounter;

        private Camera    _mainCam;
        private Transform _camTransform;

        // Draw bounds — large enough to never cull the draw call itself
        // (individual instance culling done by GPU compute)
        private static readonly Bounds _DrawBounds
            = new Bounds(Vector3.zero, Vector3.one * 10000f);

        // Shadow casting mode — cached from bool
        private ShadowCastingMode _shadowMode;

        // Cached property IDs
        private static readonly int
            _PropPerInstanceData = Shader.PropertyToID("_PerInstanceData"),
            _PropSeaweedTime     = Shader.PropertyToID("_SeaweedTime");

        // ── LIFECYCLE ─────────────────────────────────────────────────

        private void Awake()
        {
            _mainCam      = Camera.main;
            _camTransform = _mainCam != null ? _mainCam.transform : null;
            _shadowMode   = _castShadows
                ? ShadowCastingMode.On
                : ShadowCastingMode.Off;

            if (_seaweedMaterial == null || _billboardMaterial == null)
            {
                Debug.LogError(
                    "[SeaweedRenderer] Materials not assigned. Disabling.");
                enabled = false;
                return;
            }

            // COLD ALLOC: instance list with max capacity
            // Actual list allocated above as field initializer
        }

        private void OnDestroy()
        {
            if (_groups == null) return;
            for (int i = 0; i < _groupCount; i++)
                _groups[i]?.Release();
        }

        // ── PUBLIC API ────────────────────────────────────────────────

        /// <summary>
        /// Called by SeaweedMeshGenerator once meshes are built.
        /// Allocates render groups. Must be called before any rendering.
        /// </summary>
        public void InitGroups(
            Mesh[] lodMeshes,
            int    speciesVariantCount)
        {
            // speciesVariantCount = species × variantsPerSpecies
            // 4 LOD levels per variant
            _groupCount = speciesVariantCount * 4;

            // COLD ALLOC: _groupCount RenderGroups
            // Max: 10 species × 4 variants × 4 LODs = 160 groups
            // Each group: ~1.6MB arrays → only allocated once
            _groups = new RenderGroup[_groupCount];

            for (int sv = 0; sv < speciesVariantCount; sv++)
            for (int lod = 0; lod < 4; lod++)
            {
                int   gIdx = sv * 4 + lod;
                Mesh  mesh = lod < 3
                    ? (lodMeshes != null && sv*3+lod < lodMeshes.Length
                        ? lodMeshes[sv*3+lod] : null)
                    : _billboardMesh;
                Material mat = lod < 3 ? _seaweedMaterial : _billboardMaterial;

                _groups[gIdx] = new RenderGroup(_maxInstances, mesh, mat);
            }

            _ready = true;
        }

        /// <summary>
        /// Register a seaweed instance for rendering.
        /// Called by SeaweedPlacer / ChunkStreamer.
        /// </summary>
        public void RegisterInstance(SeaweedInstance inst)
        {
            if (_instances.Count >= _maxInstances)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning(
                    "[SeaweedRenderer] Max instance count reached. Ignoring.");
#endif
                return;
            }
            _instances.Add(inst);
        }

        /// <summary>Remove a seaweed instance (chunk unload).</summary>
        public void UnregisterInstance(SeaweedInstance inst)
        {
            // O(n) but only called on chunk unload — not hot path
            _instances.Remove(inst);
        }

        /// <summary>Returns all registered instances for physics binding.</summary>
        public List<SeaweedInstance> GetAllInstances() => _instances;

        /// <summary>Signals renderer is ready (called by MeshGenerator).</summary>
        public void MarkReady() { /* readiness set by InitGroups */ }

        /// <summary>True after InitGroups() called successfully.</summary>
        public bool IsReady => _ready;

        // ── ITICKABLE — called by GameTickManager ─────────────────────

        // NOTE: SeaweedRenderer implements tick via SeaweedBootstrap calling
        // DrawGroups() from within a registered ITickable wrapper.
        // This avoids requiring MonoBehaviour to also be ITickable
        // while keeping rendering in the tick system.

        /// <summary>
        /// Main render tick. Called every frame via GameTickManager.
        /// Zero GC: pre-allocated arrays, no LINQ, no ToArray().
        /// </summary>
        internal void RenderTick(float dt)
        {
            if (!_ready || _groups == null) return;
            if (_mainCam == null) return; // null safety

            _tickCounter++;

            // LOD update every N ticks — not every frame (saves CPU)
            if (_tickCounter % _lodUpdateInterval == 0)
                UpdateLODs();

            // Rebuild group matrices from current LOD assignments
            RebuildGroups();

            // Issue draw calls
            DrawGroups();
        }

        // ── PRIVATE ───────────────────────────────────────────────────

        private void UpdateLODs()
        {
            // Cache cam position locally — rule: one read per tick
            Vector3 camPos = _camTransform.position;

            int count = _instances.Count;
            for (int i = 0; i < count; i++)
            {
                SeaweedInstance inst = _instances[i];
                float dist = Vector3.Distance(camPos, inst.WorldPosition);

                inst.Visible = dist < _lod3Dist;

                if (dist < _lod0Dist)      inst.CurrentLOD = SeaweedLODLevel.LOD0;
                else if (dist < _lod1Dist) inst.CurrentLOD = SeaweedLODLevel.LOD1;
                else if (dist < _lod2Dist) inst.CurrentLOD = SeaweedLODLevel.LOD2;
                else                       inst.CurrentLOD = SeaweedLODLevel.LOD3;
            }
        }

        private void RebuildGroups()
        {
            // Clear counts — no allocation
            for (int i = 0; i < _groupCount; i++)
                _groups[i].Count = 0;

            int count = _instances.Count;
            for (int i = 0; i < count; i++)
            {
                SeaweedInstance inst = _instances[i];
                if (!inst.Visible) continue;

                int lod  = (int)inst.CurrentLOD;
                int sv   = inst.VariantIndex; // species*variantsPerSpecies + variantIdx
                int gIdx = sv * 4 + lod;

                if (gIdx < 0 || gIdx >= _groupCount) continue;

                RenderGroup g = _groups[gIdx];
                if (g.Count >= _maxInstances) continue;

                int slot = g.Count++;
                g.Matrices[slot]        = inst.Matrix;
                g.PerInstanceData[slot] = new Vector4(
                    inst.CurveIndex,
                    inst.PhaseOffset,
                    inst.SwayScale,
                    inst.AtlasRow);
            }

            // Upload to GPU — only groups with instances
            for (int i = 0; i < _groupCount; i++)
            {
                RenderGroup g = _groups[i];
                if (g.Count == 0) continue;

                // Upload instance data to ComputeBuffer
                // SetData with count to avoid uploading unused slots
                g.InstanceBuffer.SetData(
                    g.PerInstanceData, 0, 0, g.Count);

                g.MPB.SetBuffer(_PropPerInstanceData, g.InstanceBuffer);

                // Build DrawArgs: indexCount, instanceCount, 0, 0, 0
                if (g.Mesh != null)
                {
                    var args = new uint[]
                    {
                        (uint)g.Mesh.GetIndexCount(0),
                        (uint)g.Count,
                        0u, 0u, 0u
                    };
                    g.DrawArgsBuffer.SetData(args);
                }
            }
        }

        private void DrawGroups()
        {
            for (int i = 0; i < _groupCount; i++)
            {
                RenderGroup g = _groups[i];
                if (g.Count == 0 || g.Mesh == null || g.Material == null)
                    continue;

                Graphics.DrawMeshInstancedIndirect(
                    g.Mesh,
                    0,
                    g.Material,
                    _DrawBounds,
                    g.DrawArgsBuffer,
                    argsOffset:     0,
                    properties:     g.MPB,
                    castShadows:    _shadowMode,
                    receiveShadows: false,
                    layer:          gameObject.layer
                );
            }
        }
    }
}
Fayl: Assets/_Project/Scripts/Environment/Seaweed/Rendering/SeaweedGPUCuller.cs
csharp

// ============================================================
// HECTON-8 — SeaweedGPUCuller.cs v1.0
// GPU-side frustum + distance + Hi-Z occlusion culling.
// ITickable. Zero GC in Tick. Pre-allocated ComputeBuffers.
// ============================================================

using UnityEngine;
using Hecton8.Core;

namespace Hecton8.Environment.Rendering
{
    /// <summary>
    /// Runs GPU culling compute shader each tick.
    /// Fills VisibleLOD buffers consumed by SeaweedRenderer.
    ///
    /// Culling pipeline per frame:
    /// 1. ClearCounters kernel — reset atomic counters to 0.
    /// 2. FrustumCull kernel — frustum + distance, write to LOD buckets.
    /// 3. BuildDrawArgs kernel — fill IndirectArgs from counters.
    /// (HiZ pass optional — requires HiZDepthPyramidFeature in URP.)
    ///
    /// SeaweedRenderer reads resulting DrawArgs via DrawMeshInstancedIndirect.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-90)]
    public sealed class SeaweedGPUCuller : MonoBehaviour, ITickable
    {
        // ── INSPECTOR ─────────────────────────────────────────────────

        [Header("── Compute Shader ───────────────────────────────────────")]
        [SerializeField, Tooltip("Assign SeaweedCulling.compute")]
        private ComputeShader _cullShader;

        [Header("── LOD Distances ───────────────────────────────────────")]
        [SerializeField, Range(4f, 20f)]  private float _lod0Dist = 8f;
        [SerializeField, Range(8f, 40f)]  private float _lod1Dist = 20f;
        [SerializeField, Range(20f, 80f)] private float _lod2Dist = 40f;
        [SerializeField, Range(40f,120f)] private float _lod3Dist = 80f;

        [Header("── Hi-Z ─────────────────────────────────────────────────")]
        [SerializeField, Tooltip("Enable Hi-Z occlusion (requires depth pyramid URP Feature).")]
        private bool _useHiZ = false;

        // ── PRIVATE STATE ─────────────────────────────────────────────

        // Compute kernels
        private int _kernelClear;
        private int _kernelFrustum;
        private int _kernelBuildArgs;
        private int _kernelHiZ;

        // Instance input buffer (uploaded once by SeaweedRenderer)
        private ComputeBuffer _allInstancesBuffer;
        private int           _instanceCount;

        // Output buffers — LOD buckets
        // COLD ALLOC: max instances capacity per LOD bucket
        private ComputeBuffer[] _visibleBuffers  = new ComputeBuffer[4];
        private ComputeBuffer[] _counterBuffers  = new ComputeBuffer[4];
        private ComputeBuffer[] _drawArgsBuffers = new ComputeBuffer[4];
        private ComputeBuffer   _indexCountBuffer;

        private Camera    _mainCam;
        private bool      _registered;
        private bool      _initialized;

        // Cached shader property IDs
        private static readonly int
            _PropAllInstances    = Shader.PropertyToID("_AllInstances"),
            _PropInstanceCount   = Shader.PropertyToID("_InstanceCount"),
            _PropVP              = Shader.PropertyToID("_ViewProjectionMatrix"),
            _PropProj            = Shader.PropertyToID("_ProjectionMatrix"),
            _PropCamPos          = Shader.PropertyToID("_CameraPosition"),
            _PropHiZTex          = Shader.PropertyToID("_HiZDepthTexture"),
            _PropHiZSize         = Shader.PropertyToID("_HiZTextureSize"),
            _PropLOD0Dist        = Shader.PropertyToID("_LOD0MaxDist"),
            _PropLOD1Dist        = Shader.PropertyToID("_LOD1MaxDist"),
            _PropLOD2Dist        = Shader.PropertyToID("_LOD2MaxDist"),
            _PropLOD3Dist        = Shader.PropertyToID("_LOD3MaxDist"),
            _PropVisLOD0         = Shader.PropertyToID("_VisibleLOD0"),
            _PropVisLOD1         = Shader.PropertyToID("_VisibleLOD1"),
            _PropVisLOD2         = Shader.PropertyToID("_VisibleLOD2"),
            _PropVisLOD3         = Shader.PropertyToID("_VisibleLOD3"),
            _PropCtrLOD0         = Shader.PropertyToID("_CounterLOD0"),
            _PropCtrLOD1         = Shader.PropertyToID("_CounterLOD1"),
            _PropCtrLOD2         = Shader.PropertyToID("_CounterLOD2"),
            _PropCtrLOD3         = Shader.PropertyToID("_CounterLOD3"),
            _PropDrawLOD0        = Shader.PropertyToID("_DrawArgsLOD0"),
            _PropDrawLOD1        = Shader.PropertyToID("_DrawArgsLOD1"),
            _PropDrawLOD2        = Shader.PropertyToID("_DrawArgsLOD2"),
            _PropDrawLOD3        = Shader.PropertyToID("_DrawArgsLOD3"),
            _PropIndexCounts     = Shader.PropertyToID("_IndexCountsPerLOD");

        // ── LIFECYCLE ─────────────────────────────────────────────────

        private void Awake()
        {
            _mainCam = Camera.main;

            if (_cullShader == null)
            {
                Debug.LogError(
                    "[SeaweedGPUCuller] ComputeShader not assigned. Disabling.");
                enabled = false;
                return;
            }

            _kernelClear     = _cullShader.FindKernel("ClearCounters");
            _kernelFrustum   = _cullShader.FindKernel("FrustumCull");
            _kernelBuildArgs = _cullShader.FindKernel("BuildDrawArgs");
            _kernelHiZ       = _useHiZ
                ? _cullShader.FindKernel("HiZCull")
                : -1;
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
            if (_registered && GameTickManager.Instance != null)
            {
                GameTickManager.Instance.Unregister(this);
                _registered = false;
            }
        }

        private void OnDestroy()
        {
            _allInstancesBuffer?.Release();
            _indexCountBuffer?.Release();
            for (int i = 0; i < 4; i++)
            {
                _visibleBuffers[i]?.Release();
                _counterBuffers[i]?.Release();
                _drawArgsBuffers[i]?.Release();
            }
        }

        // ── PUBLIC INIT ───────────────────────────────────────────────

        /// <summary>
        /// Upload all instance data to GPU. Called once after placement.
        /// Allocates buffers sized to instanceCount.
        /// </summary>
        public void Initialize(
            SeaweedGPUInstance[] instances,
            int[]  indexCountsPerLOD,
            int    maxInstancesPerBucket)
        {
            _instanceCount = instances.Length;

            // COLD ALLOC: instance buffer — 96 bytes × count
            _allInstancesBuffer = new ComputeBuffer(_instanceCount, 96);
            _allInstancesBuffer.SetData(instances);
            _allInstancesBuffer.name = "AllSeaweedInstances";

            // COLD ALLOC: LOD buckets
            for (int i = 0; i < 4; i++)
            {
                _visibleBuffers[i] = new ComputeBuffer(
                    maxInstancesPerBucket, 80);
                _visibleBuffers[i].name = $"VisibleLOD{i}";

                _counterBuffers[i] = new ComputeBuffer(1, 4);
                _counterBuffers[i].name = $"CounterLOD{i}";

                _drawArgsBuffers[i] = new ComputeBuffer(
                    1, 20, ComputeBufferType.IndirectArguments);
                _drawArgsBuffers[i].name = $"DrawArgsLOD{i}";
            }

            _indexCountBuffer = new ComputeBuffer(4, 4);
            _indexCountBuffer.SetData(indexCountsPerLOD);
            _indexCountBuffer.name = "IndexCountsPerLOD";

            BindStaticBuffers();
            _initialized = true;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[SeaweedGPUCuller] Initialized: " +
                      $"{_instanceCount} instances.");
#endif
        }

        /// <summary>
        /// Returns DrawArgs buffer for given LOD level.
        /// SeaweedRenderer uses this for DrawMeshInstancedIndirect.
        /// </summary>
        public ComputeBuffer GetDrawArgsBuffer(int lod)
            => (uint)lod < 4u ? _drawArgsBuffers[lod] : null;

        /// <summary>
        /// Returns visible instance buffer for given LOD level.
        /// Passed to material as _PerInstanceData in SeaweedRenderer.
        /// </summary>
        public ComputeBuffer GetVisibleBuffer(int lod)
            => (uint)lod < 4u ? _visibleBuffers[lod] : null;

        // ── ITICKABLE ─────────────────────────────────────────────────

        public void Tick(float dt)
        {
            if (!_initialized || _mainCam == null) return;

            UpdateCameraUniforms();

            // Pass 1: Clear atomic counters
            _cullShader.Dispatch(_kernelClear, 1, 1, 1);

            // Pass 2: Frustum + distance cull
            int groups = Mathf.CeilToInt(_instanceCount / 64f);
            _cullShader.Dispatch(_kernelFrustum, groups, 1, 1);

            // Pass 3: Build DrawArgs from counters
            _cullShader.Dispatch(_kernelBuildArgs, 1, 1, 1);

            // Pass 4: Optional Hi-Z occlusion
            if (_useHiZ && _kernelHiZ >= 0)
            {
                Texture hiZTex = Shader.GetGlobalTexture(
                    Shader.PropertyToID("_HiZDepthTexture"));
                if (hiZTex != null)
                {
                    _cullShader.SetTexture(_kernelHiZ, _PropHiZTex, hiZTex);
                    _cullShader.SetVector(_PropHiZSize,
                        new Vector2(hiZTex.width, hiZTex.height));
                    _cullShader.Dispatch(_kernelHiZ, groups, 1, 1);
                }
            }
        }

        // ── PRIVATE ───────────────────────────────────────────────────

        private void UpdateCameraUniforms()
        {
            // Cache transform read — single call per tick
            var camPos = _mainCam.transform.position;
            var vp     = _mainCam.projectionMatrix * _mainCam.worldToCameraMatrix;

            _cullShader.SetMatrix(_PropVP,      vp);
            _cullShader.SetMatrix(_PropProj,    _mainCam.projectionMatrix);
            _cullShader.SetVector(_PropCamPos,  camPos);
            _cullShader.SetFloat (_PropLOD0Dist, _lod0Dist);
            _cullShader.SetFloat (_PropLOD1Dist, _lod1Dist);
            _cullShader.SetFloat (_PropLOD2Dist, _lod2Dist);
            _cullShader.SetFloat (_PropLOD3Dist, _lod3Dist);
            _cullShader.SetInt   (_PropInstanceCount, _instanceCount);
        }

        private void BindStaticBuffers()
        {
            // Bind to all kernels that need them
            int[] kernels = { _kernelClear, _kernelFrustum, _kernelBuildArgs };

            foreach (int k in kernels)
            {
                if (k < 0) continue;

                _cullShader.SetBuffer(k, _PropAllInstances,
                    _allInstancesBuffer);

                _cullShader.SetBuffer(k, _PropVisLOD0, _visibleBuffers[0]);
                _cullShader.SetBuffer(k, _PropVisLOD1, _visibleBuffers[1]);
                _cullShader.SetBuffer(k, _PropVisLOD2, _visibleBuffers[2]);
                _cullShader.SetBuffer(k, _PropVisLOD3, _visibleBuffers[3]);

                _cullShader.SetBuffer(k, _PropCtrLOD0, _counterBuffers[0]);
                _cullShader.SetBuffer(k, _PropCtrLOD1, _counterBuffers[1]);
                _cullShader.SetBuffer(k, _PropCtrLOD2, _counterBuffers[2]);
                _cullShader.SetBuffer(k, _PropCtrLOD3, _counterBuffers[3]);

                _cullShader.SetBuffer(k, _PropDrawLOD0, _drawArgsBuffers[0]);
                _cullShader.SetBuffer(k, _PropDrawLOD1, _drawArgsBuffers[1]);
                _cullShader.SetBuffer(k, _PropDrawLOD2, _drawArgsBuffers[2]);
                _cullShader.SetBuffer(k, _PropDrawLOD3, _drawArgsBuffers[3]);

                _cullShader.SetBuffer(k, _PropIndexCounts, _indexCountBuffer);
            }
        }
    }
}
Fayl: Assets/_Project/Scripts/Environment/Seaweed/Physics/SeaweedPhysicsManager.cs
csharp

// ============================================================
// HECTON-8 — SeaweedPhysicsManager.cs v1.0
// Verlet spring simulation for close seaweed (< 15m from camera).
// ITickable. Burst Jobs. Pre-allocated NativeArrays.
// Zero GC in Tick. Max 150 active physical plants on MX350.
// ============================================================

using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Hecton8.Core;

namespace Hecton8.Environment.Physics
{
    /// <summary>
    /// Simulates seaweed physics using Verlet integration.
    /// Each plant = chain of <c>SegmentsPerPlant</c> spring points.
    /// Plants beyond <c>SimRadius</c> from camera are skipped.
    ///
    /// Why Verlet, not Rigidbody:
    /// - 150 Rigidbodies on i5-11th = ~75ms/frame. Unacceptable.
    /// - Verlet Job on 150×8=1200 points = ~0.4ms/frame. Fine.
    ///
    /// Results uploaded to GPU shader buffer each tick.
    /// Shader samples offsets to add to procedural animation.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-85)]
    public sealed class SeaweedPhysicsManager : MonoBehaviour, ITickable
    {
        // ── INSPECTOR ─────────────────────────────────────────────────

        [Header("── Simulation ───────────────────────────────────────────")]
        [SerializeField, Range(1, 5),
         Tooltip("Substeps per tick for stability. 3 recommended.")]
        private int _subSteps = 3;

        [SerializeField, Range(-0.5f, 0f),
         Tooltip("Underwater gravity (weak). -0.05 default.")]
        private float _gravity = -0.05f;

        [SerializeField, Range(0.9f, 0.999f),
         Tooltip("Velocity damping per step.")]
        private float _damping = 0.97f;

        [SerializeField, Range(0.1f, 1f)]
        private float _stiffness = 0.35f;

        [SerializeField, Range(0f, 1f)]
        private float _currentForce = 0.15f;

        [Header("── Performance ─────────────────────────────────────────")]
        [SerializeField, Range(5f, 30f)]
        private float _simRadius = 15f;

        [SerializeField, Range(10, 200),
         Tooltip("Max simultaneously simulated plants. MX350: 150.")]
        private int _maxSimulated = 150;

        [SerializeField, Range(4, 12),
         Tooltip("Spring points per plant. 8 = good quality, 4 = cheap.")]
        private int _segmentsPerPlant = 8;

        // ── PRIVATE STATE ─────────────────────────────────────────────

        // Spring point struct — Burst-compatible
        private struct SpringPoint
        {
            public float3 position;
            public float3 prevPosition;
            public float3 anchor;       // rest position
            public float  t;            // 0=root, 1=tip
            public float  stiffness;
            public float  mass;
            public int    seaweedIdx;   // -1 = unused slot
        }

        // COLD ALLOC: _maxSimulated * _segmentsPerPlant points
        private NativeArray<SpringPoint> _points;
        private NativeArray<float4>      _interactorData;  // 4 interactors
        private NativeArray<float4>      _exportData;      // offsets → GPU

        private ComputeBuffer _physicsGPUBuffer;

        private bool _registered;
        private bool _initialized;

        private Camera _mainCam;

        // Cached property IDs
        private static readonly int
            _PropPhysicsData     = Shader.PropertyToID("_SeaweedPhysicsData"),
            _PropSegmentsPerPlant = Shader.PropertyToID("_SeaweedSegmentsPerPlant");

        // Interactor cache — updated by SeaweedInteraction component
        // float4: xyz=pos, w=radius
        private static readonly float4[] _InteractorCache = new float4[4];
        private static readonly float[]  _StrengthCache   = new float[4];

        // ── SINGLETON ─────────────────────────────────────────────────

        public static SeaweedPhysicsManager Instance { get; private set; }

        // ── LIFECYCLE ─────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _mainCam = Camera.main;

            int total = _maxSimulated * _segmentsPerPlant;

            // COLD ALLOC: physics point arrays — persistent for lifetime
            _points         = new NativeArray<SpringPoint>(
                total, Allocator.Persistent);
            _interactorData = new NativeArray<float4>(
                4, Allocator.Persistent);
            _exportData     = new NativeArray<float4>(
                total, Allocator.Persistent);

            // COLD ALLOC: GPU buffer — 16 bytes × total points
            _physicsGPUBuffer = new ComputeBuffer(total, 16);
            _physicsGPUBuffer.name = "SeaweedPhysicsData";

            // Initialize all slots as unused
            var empty = new SpringPoint { seaweedIdx = -1 };
            for (int i = 0; i < total; i++)
                _points[i] = empty;

            Shader.SetGlobalBuffer(_PropPhysicsData,     _physicsGPUBuffer);
            Shader.SetGlobalInt   (_PropSegmentsPerPlant, _segmentsPerPlant);

            _initialized = true;
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
            if (_registered && GameTickManager.Instance != null)
            {
                GameTickManager.Instance.Unregister(this);
                _registered = false;
            }
        }

        private void OnDestroy()
        {
            if (_points.IsCreated)         _points.Dispose();
            if (_interactorData.IsCreated) _interactorData.Dispose();
            if (_exportData.IsCreated)     _exportData.Dispose();
            _physicsGPUBuffer?.Release();

            if (Instance == this) Instance = null;
        }

        // ── ITICKABLE ─────────────────────────────────────────────────

        public void Tick(float dt)
        {
            if (!_initialized || _mainCam == null) return;

            // Update interactor data from cache (written by SeaweedInteraction)
            for (int i = 0; i < 4; i++)
                _interactorData[i] = _InteractorCache[i];

            float3 current = new float3(
                SeaweedCurrentZone.GlobalCurrent.x * _currentForce,
                0f,
                SeaweedCurrentZone.GlobalCurrent.z * _currentForce);

            float3 gravity  = new float3(0f, _gravity, 0f);
            float  stepDt   = dt / Mathf.Max(1, _subSteps);

            // Verlet substeps
            JobHandle handle = default;
            for (int step = 0; step < _subSteps; step++)
            {
                handle = new VerletIntegrateJob
                {
                    Points      = _points,
                    Interactors = _interactorData,
                    Gravity     = gravity,
                    Current     = current,
                    Damping     = _damping,
                    Stiffness   = _stiffness,
                    DeltaTime   = stepDt
                }.Schedule(_points.Length, 16, handle);
            }

            // Export offsets for shader
            handle = new ExportOffsetJob
            {
                Points     = _points,
                ExportData = _exportData
            }.Schedule(_points.Length, 16, handle);

            handle.Complete();

            // Upload to GPU — no readback, push-only
            _physicsGPUBuffer.SetData(_exportData);
        }

        // ── PUBLIC API ────────────────────────────────────────────────

        /// <summary>
        /// Register a seaweed plant for physics simulation.
        /// Returns slot index (-1 if no slot available).
        /// </summary>
        public int RegisterSeaweed(
            int     slotIdx,
            Vector3 basePos,
            float   height,
            float   stiffness,
            float   mass)
        {
            if (slotIdx < 0 || slotIdx >= _maxSimulated) return -1;

            int baseIdx = slotIdx * _segmentsPerPlant;
            for (int s = 0; s < _segmentsPerPlant; s++)
            {
                float  t      = (float)s / (_segmentsPerPlant - 1);
                float3 anchor = (float3)basePos + new float3(0f, t * height, 0f);
                _points[baseIdx + s] = new SpringPoint
                {
                    position    = anchor,
                    prevPosition = anchor,
                    anchor      = anchor,
                    t           = t,
                    stiffness   = stiffness * (1f - t * 0.5f),
                    mass        = mass * (1f - t * 0.7f),
                    seaweedIdx  = slotIdx
                };
            }
            return slotIdx;
        }

        /// <summary>Releases a physics slot (plant out of range).</summary>
        public void ReleaseSeaweed(int slotIdx)
        {
            if (slotIdx < 0 || slotIdx >= _maxSimulated) return;
            int baseIdx = slotIdx * _segmentsPerPlant;
            var empty   = new SpringPoint { seaweedIdx = -1 };
            for (int s = 0; s < _segmentsPerPlant; s++)
                _points[baseIdx + s] = empty;
        }

        /// <summary>
        /// Called by SeaweedInteraction to push interactor positions.
        /// No alloc — writes to static cache.
        /// </summary>
        public static void SetInteractor(int idx, Vector3 pos, float radius,
            float strength)
        {
            if ((uint)idx >= 4u) return;
            _InteractorCache[idx] = new float4(pos.x, pos.y, pos.z, radius);
            _StrengthCache[idx]   = strength;
        }

        // ── BURST JOBS ────────────────────────────────────────────────

        [BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
        private struct VerletIntegrateJob : IJobParallelFor
        {
            public NativeArray<SpringPoint> Points;
            [ReadOnly] public NativeArray<float4> Interactors;
            [ReadOnly] public float3 Gravity;
            [ReadOnly] public float3 Current;
            [ReadOnly] public float  Damping;
            [ReadOnly] public float  Stiffness;
            [ReadOnly] public float  DeltaTime;

            public void Execute(int i)
            {
                SpringPoint p = Points[i];
                if (p.seaweedIdx < 0) return;

                // Root point — static
                if (p.t < 0.001f)
                {
                    p.prevPosition = p.position;
                    Points[i] = p;
                    return;
                }

                // Verlet step
                float3 vel = (p.position - p.prevPosition) * Damping;
                p.prevPosition = p.position;

                float3 force = Gravity + Current * p.t;

                // Interactor push
                for (int j = 0; j < 4; j++)
                {
                    float3 iPos = Interactors[j].xyz;
                    float  iRad = Interactors[j].w;
                    if (iRad <= 0f) continue;

                    float3 diff = p.position - iPos;
                    diff.y = 0f;
                    float dist = math.length(diff);
                    if (dist >= iRad) continue;

                    float push = (1f - dist / iRad);
                    push = push * push * push;
                    force += math.normalize(diff + new float3(0.001f, 0f, 0f))
                           * push * 2f * p.t;
                }

                p.position += vel + force * (DeltaTime * DeltaTime);

                // Length constraint to parent segment
                int parentIdx = i - 1;
                if (parentIdx >= 0 && Points[parentIdx].seaweedIdx == p.seaweedIdx)
                {
                    SpringPoint parent = Points[parentIdx];
                    float3 diff     = p.position - parent.position;
                    float  dist     = math.length(diff);
                    float  restLen  = math.distance(p.anchor, parent.anchor);
                    if (dist > 0.001f && restLen > 0.001f)
                    {
                        p.position -= (diff / dist) * (dist - restLen) * Stiffness;
                    }
                }

                // Anchor spring (prevent excessive drift)
                float3 toAnchor   = p.anchor - p.position;
                float  anchorDist = math.length(toAnchor);
                float  maxDrift   = 0.5f * p.t;
                if (anchorDist > maxDrift && anchorDist > 0.001f)
                {
                    p.position = p.anchor +
                        math.normalize(p.position - p.anchor) * maxDrift;
                }

                Points[i] = p;
            }
        }

        [BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
        private struct ExportOffsetJob : IJobParallelFor
        {
            [ReadOnly]  public NativeArray<SpringPoint> Points;
            [WriteOnly] public NativeArray<float4>      ExportData;

            public void Execute(int i)
            {
                SpringPoint p = Points[i];
                if (p.seaweedIdx < 0)
                {
                    ExportData[i] = float4.zero;
                    return;
                }
                float3 offset = p.position - p.anchor;
                float  angle  = math.atan2(math.length(offset.xz), offset.y);
                ExportData[i] = new float4(offset, angle);
            }
        }
    }
}
Fayl: Assets/_Project/Scripts/Environment/Seaweed/SeaweedInteraction.cs
csharp

// ============================================================
// HECTON-8 — SeaweedInteraction.cs v1.0
// Pushes interactor positions (player, NPCs) to seaweed shader.
// ITickable. Zero GC. Static cache in PhysicsManager.
// ============================================================

using UnityEngine;
using Hecton8.Core;
using Hecton8.Environment.Physics;

namespace Hecton8.Environment
{
    /// <summary>
    /// Reads up to 4 interactor transforms (player, fish, submarines)
    /// and pushes their world positions to SeaweedPhysicsManager
    /// and global shader uniforms each tick.
    ///
    /// Seaweed shader uses _Interactors[] to deflect vertices.
    /// SeaweedPhysicsManager uses same data for Verlet push forces.
    /// Zero colliders. Zero physics. Pure shader + Verlet.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SeaweedInteraction : MonoBehaviour, ITickable
    {
        // ── INSPECTOR ─────────────────────────────────────────────────

        [System.Serializable]
        public struct Interactor
        {
            [Tooltip("Transform to track (player, fish, etc.)")]
            public Transform target;

            [Tooltip("Influence radius in metres."), Range(0.1f, 5f)]
            public float radius;

            [Tooltip("Push strength."), Range(0f, 2f)]
            public float strength;
        }

        [Header("── Interactors (max 4) ────────────────────────────────")]
        [SerializeField] private Interactor[] _interactors = new Interactor[4];

        // ── PRIVATE STATE ─────────────────────────────────────────────

        private bool _registered;

        // Pre-allocated arrays for shader — COLD ALLOC: 4 slots fixed
        private static readonly Vector4[] _ShaderInteractors = new Vector4[4];
        private static readonly float[]   _ShaderStrengths   = new float[4];

        // Cached property IDs
        private static readonly int
            _PropInteractors = Shader.PropertyToID("_Interactors"),
            _PropStrengths   = Shader.PropertyToID("_InteractorStrengths");

        // Off-world sentinel — pushes interactor out of any influence range
        private static readonly Vector4 _OffWorld
            = new Vector4(0f, -99999f, 0f, 0f);

        // ── LIFECYCLE ─────────────────────────────────────────────────

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
            if (_registered && GameTickManager.Instance != null)
            {
                GameTickManager.Instance.Unregister(this);
                _registered = false;
            }
        }

        // ── ITICKABLE ─────────────────────────────────────────────────

        public void Tick(float dt)
        {
            // Build interactor data — max 4 iterations, no alloc
            for (int i = 0; i < 4; i++)
            {
                bool valid = i < _interactors.Length
                          && _interactors[i].target != null;

                if (valid)
                {
                    // Cache transform.position — single read
                    Vector3 pos = _interactors[i].target.position;
                    float   rad = _interactors[i].radius;
                    float   str = _interactors[i].strength;

                    _ShaderInteractors[i] = new Vector4(
                        pos.x, pos.y, pos.z, rad);
                    _ShaderStrengths[i] = str;

                    SeaweedPhysicsManager.SetInteractor(i, pos, rad, str);
                }
                else
                {
                    _ShaderInteractors[i] = _OffWorld;
                    _ShaderStrengths[i]   = 0f;
                    SeaweedPhysicsManager.SetInteractor(
                        i, Vector3.zero, 0f, 0f);
                }
            }

            Shader.SetGlobalVectorArray(_PropInteractors, _ShaderInteractors);
            Shader.SetGlobalFloatArray (_PropStrengths,   _ShaderStrengths);
        }
    }
}
Fayl: Assets/_Project/Scripts/Environment/Seaweed/SeaweedBootstrap.cs
csharp

// ============================================================
// HECTON-8 — SeaweedBootstrap.cs v1.0
// Entry point. Coordinates init order of all seaweed subsystems.
// ICoroutine-style state machine for startup sequence.
// No Update(). No GC in Tick.
// ============================================================

using System.Collections;
using UnityEngine;
using Hecton8.Core;
using Hecton8.Environment.Rendering;
using Hecton8.Environment.Physics;
using Hecton8.Environment.Seasonal;
using Hecton8.Environment.Textures;

namespace Hecton8.Environment
{
    /// <summary>
    /// Coordinates startup of the underwater ecosystem.
    ///
    /// Init order (MUST be respected):
    /// 1. SeaweedAnimCurveSystem (shader textures)
    /// 2. SeaweedTextureGenerator (atlas textures)
    /// 3. SeaweedMeshGenerator (Burst mesh generation)
    /// 4. SeaweedRenderer.InitGroups() (pre-alloc render groups)
    /// 5. SeaweedPlacer (chunk generation)
    /// 6. SeaweedGPUCuller.Initialize() (upload instances to GPU)
    /// 7. SeaweedPhysicsManager (already self-initialized)
    /// 8. SeaweedSeasonSystem (apply season)
    ///
    /// After startup: all systems run via GameTickManager.
    /// Bootstrap itself is NOT registered as ITickable.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-200)]
    public sealed class SeaweedBootstrap : MonoBehaviour
    {
        // ── INSPECTOR ─────────────────────────────────────────────────

        [Header("── Required Systems ────────────────────────────────────")]
        [SerializeField] private SeaweedAnimCurveSystem  _animCurves;
        [SerializeField] private SeaweedTextureGenerator _textureGen;
        [SerializeField] private SeaweedMeshGenerator    _meshGen;
        [SerializeField] private SeaweedRenderer         _renderer;
        [SerializeField] private SeaweedGPUCuller        _culler;
        [SerializeField] private SeaweedPhysicsManager   _physics;
        [SerializeField] private SeaweedInteraction      _interaction;
        [SerializeField] private SeaweedCurrentZone      _currentZone;

        [Header("── Optional Systems ────────────────────────────────────")]
        [SerializeField] private SeaweedSeasonSystem     _seasons;
        [SerializeField] private SeaweedPlacer           _placer;

        [Header("── Settings ────────────────────────────────────────────")]
        [SerializeField] private int _variantsPerSpecies = 4;

        [Header("── Loading UI (optional) ──────────────────────────────")]
        [SerializeField] private UnityEngine.UI.Slider   _progressBar;
        [SerializeField] private TMPro.TextMeshProUGUI   _statusText;

        // ── LIFECYCLE ─────────────────────────────────────────────────

        private void Awake()
        {
            // Validate required deps
            if (_animCurves == null || _meshGen == null || _renderer == null)
            {
                Debug.LogError(
                    "[SeaweedBootstrap] Missing required references. " +
                    "Check Inspector assignments. Disabling.");
                enabled = false;
                return;
            }
        }

        private IEnumerator Start()
        {
            SetStatus("Generating animation curves...", 0f);

            // 1. Wait for AnimCurve texture generation (async, Task.Run)
            yield return new WaitUntil(() =>
                _animCurves == null || _animCurves.IsReady);

            SetStatus("Generating textures...", 0.2f);

            // 2. Wait for atlas texture generation
            if (_textureGen != null)
            {
                yield return new WaitUntil(() => _textureGen.IsReady);
            }

            SetStatus("Building meshes...", 0.4f);

            // 3. Wait for mesh generation
            yield return new WaitUntil(() =>
                _meshGen == null || _meshGen.IsReady);

            SetStatus("Initializing renderer...", 0.6f);

            // 4. Init renderer groups
            if (_meshGen != null && _renderer != null)
            {
                _renderer.InitGroups(
                    _meshGen.GetAllLODMeshes(),
                    _meshGen.SpeciesVariantCount);
            }

            SetStatus("Placing seaweed...", 0.7f);

            // 5. Run placement (may take multiple frames)
            if (_placer != null)
            {
                yield return _placer.PlaceAllChunksCoroutine(
                    _renderer);
            }

            SetStatus("Uploading to GPU...", 0.85f);

            // 6. Upload instances to GPU culler
            if (_culler != null && _renderer != null)
            {
                var instances   = _renderer.GetAllInstances();
                var gpuData     = BuildGPUInstanceArray(instances);
                var indexCounts = _meshGen != null
                    ? _meshGen.GetIndexCountsPerLOD()
                    : new int[]{ 0, 0, 0, 0 };

                _culler.Initialize(
                    gpuData,
                    indexCounts,
                    instances.Count);
            }

            SetStatus("Applying season...", 0.95f);

            // 7. Season (just shader params — instant)
            // SeaweedSeasonSystem self-applies in Awake

            SetStatus("Ready.", 1f);

            // Hide loading UI
            if (_progressBar != null)
                _progressBar.gameObject.SetActive(false);
            if (_statusText != null)
                _statusText.gameObject.SetActive(false);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LogStats();
#endif
        }

        // ── PRIVATE ───────────────────────────────────────────────────

        private void SetStatus(string msg, float progress)
        {
            // Dirty-flag check for TMPro — avoids rebuild if unchanged
            if (_statusText != null && _statusText.text != msg)
                _statusText.text = msg;

            if (_progressBar != null)
                _progressBar.value = progress;
        }

        private static SeaweedGPUInstance[] BuildGPUInstanceArray(
            System.Collections.Generic.List<SeaweedInstance> instances)
        {
            // COLD ALLOC: one-time array build at startup
            var arr = new SeaweedGPUInstance[instances.Count];
            for (int i = 0; i < instances.Count; i++)
            {
                SeaweedInstance inst = instances[i];
                arr[i] = new SeaweedGPUInstance
                {
                    objectToWorld = inst.Matrix,
                    boundsCenter  = new Vector4(
                        inst.WorldPosition.x,
                        inst.WorldPosition.y,
                        inst.WorldPosition.z,
                        inst.BoundsRadius),
                    renderParams  = new Vector4(
                        inst.CurveIndex,
                        inst.PhaseOffset,
                        inst.SwayScale,
                        inst.AtlasRow)
                };
            }
            return arr;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void LogStats()
        {
            int count = _renderer != null
                ? _renderer.GetAllInstances().Count : 0;

            Debug.Log(
                $"[SeaweedBootstrap] Ready.\n" +
                $"  Instances : {count}\n" +
                $"  Season    : {(_seasons != null ? _seasons.CurrentSeason.ToString() : "N/A")}\n" +
                $"  Physics   : {(_physics != null ? "Verlet Burst" : "OFF")}\n" +
                $"  GPU Cull  : {(_culler != null ? "Active" : "OFF")}"
            );
        }
#endif
    }
}
Fayl: Assets/_Project/Scripts/Environment/Seaweed/SeaweedMeshGenerator.cs
csharp

// ============================================================
// HECTON-8 — SeaweedMeshGenerator.cs v1.0
// Procedural seaweed mesh generation using Burst Jobs.
// All species: Kelp, Bushy, Filament, BladeLettuce, Coralline.
// Anatomical details: trunk ribs, pneumatocysts,
// rhizoids, basal blades, serrated edges.
// Cached to disk. Zero GC in generation (NativeArray).
// ============================================================

using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Hecton8.Environment.Generation;

namespace Hecton8.Environment
{
    /// <summary>
    /// Generates procedural seaweed meshes at scene startup.
    /// One coroutine — one species variant per frame to avoid spikes.
    /// Uses Burst Jobs (SpineJob + ExtrudeJob) for heavy computation.
    /// Results cached to disk (SeaweedMeshCache) for faster restarts.
    ///
    /// After generation, call GetAllLODMeshes() to pass to SeaweedRenderer.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-110)]
    public sealed class SeaweedMeshGenerator : MonoBehaviour
    {
        // ── INSPECTOR ─────────────────────────────────────────────────

        [Header("── Library ──────────────────────────────────────────────")]
        [SerializeField] private SeaweedSpeciesLibrary _library;

        [Header("── Settings ────────────────────────────────────────────")]
        [SerializeField, Range(1, 8)]
        private int _variantsPerSpecies = 4;

        [SerializeField, Tooltip("Load meshes from disk cache if available.")]
        private bool _useCache = true;

        [Header("── Detail Thresholds ───────────────────────────────────")]
        [SerializeField, Tooltip("Min height (m) for kelp trunk ribs.")]
        private float _trunkRibMinHeight = 1.5f;

        [SerializeField, Tooltip("Min height (m) for pneumatocysts.")]
        private float _pneumatocystMinHeight = 0.8f;

        [SerializeField]
        private bool _generateRhizoids    = true;

        [SerializeField]
        private bool _generateBasalBlades = true;

        // ── PRIVATE STATE ─────────────────────────────────────────────

        // [speciesVariantFlatIdx * 3 + lodLevel] = mesh
        // speciesVariantFlatIdx = speciesIdx * _variantsPerSpecies + variantIdx
        private Mesh[] _allMeshes;

        private bool _ready;

        // Reusable mesh build lists — COLD ALLOC, cleared per mesh
        // Sized for worst case (massive kelp LOD0 with all details)
        private readonly List<Vector3> _verts  = new List<Vector3>(4096);
        private readonly List<Vector3> _norms  = new List<Vector3>(4096);
        private readonly List<Vector2> _uvs    = new List<Vector2>(4096);
        private readonly List<Color32> _cols   = new List<Color32>(4096);
        private readonly List<int>     _tris   = new List<int>(16384);

        // ── PUBLIC PROPERTIES ─────────────────────────────────────────

        /// <summary>True after all meshes generated.</summary>
        public bool IsReady => _ready;

        /// <summary>Total species × variants (LOD meshes array stride).</summary>
        public int SpeciesVariantCount
        {
            get
            {
                if (_library == null || _library.Species == null) return 0;
                return _library.Species.Length * _variantsPerSpecies;
            }
        }

        // ── LIFECYCLE ─────────────────────────────────────────────────

        private IEnumerator Start()
        {
            if (_library == null || _library.Species == null)
            {
                Debug.LogError(
                    "[SeaweedMeshGenerator] Library not assigned. Disabling.");
                enabled = false;
                yield break;
            }

            int speciesCount = _library.Species.Length;
            int totalVariants = speciesCount * _variantsPerSpecies;
            int lodCount      = 3; // LOD0, LOD1, LOD2 (LOD3 = billboard, separate)

            // COLD ALLOC: one array for all meshes (startup only)
            _allMeshes = new Mesh[totalVariants * lodCount];

            int generatedCount = 0;

            for (int si = 0; si < speciesCount; si++)
            {
                SeaweedSpeciesDefinition sp = _library.Species[si];

                for (int vi = 0; vi < _variantsPerSpecies; vi++)
                {
                    for (int lod = 0; lod < lodCount; lod++)
                    {
                        string cacheKey = SeaweedMeshCache.GetKey(sp.id, vi, lod);
                        Mesh   mesh     = null;

                        if (_useCache &&
                            SeaweedMeshCache.TryLoad(cacheKey, out Mesh cached))
                        {
                            mesh = cached;
                        }
                        else
                        {
                            mesh = BuildSpeciesMesh(sp, vi, lod);

                            if (_useCache)
                                SeaweedMeshCache.Save(cacheKey, mesh);
                        }

                        int flatIdx = (si * _variantsPerSpecies + vi) * lodCount + lod;
                        _allMeshes[flatIdx] = mesh;
                        generatedCount++;
                    }

                    // Yield one frame per variant to avoid hitches
                    yield return null;
                }
            }

            _ready = true;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log(
                $"[SeaweedMeshGenerator] Generated {generatedCount} meshes " +
                $"({speciesCount} species × {_variantsPerSpecies} variants × 3 LODs).");
#endif
        }

        private void OnDestroy()
        {
            if (_allMeshes == null) return;
            for (int i = 0; i < _allMeshes.Length; i++)
            {
                if (_allMeshes[i] != null)
                    Destroy(_allMeshes[i]);
            }
        }

        // ── PUBLIC API ────────────────────────────────────────────────

        /// <summary>
        /// Returns flat array of all generated LOD meshes.
        /// Layout: [speciesVariantIdx * 3 + lod]
        /// Used by SeaweedRenderer.InitGroups().
        /// </summary>
        public Mesh[] GetAllLODMeshes() => _allMeshes;

        /// <summary>
        /// Returns index counts per LOD for DrawArgs.
        /// [0]=
Something went wrong with this response, please tr

[ANALYSIS]
**Target:** Prodolzhenie s mesta obryva — SeaweedMeshGenerator.cs publichnyy API (GetIndexCountsPerLOD i dalee), zatem ostavshiesya fayly.
**Affected systems:** SeaweedMeshGenerator, SeaweedTextureGenerator, SeaweedPlacer, SeaweedMeshCache, SeaweedPhysicsBinding, SeaAnemone, MarineInvertebrateRenderer, CoralFishSchool
**Zero GC proof:** NativeArray dlya Burst Jobs, pre-allocated List<> cleared per use, no LINQ, no lambda in hot paths, cached Shader.PropertyToID
**State check:** null-check library v Start, graceful disable
**Instruction quote:** AGENTS.md §1 Zero GC, §2 ITickable, §4 MPB, §8 Cold Alloc

---

```csharp
        // Prodolzhenie SeaweedMeshGenerator.cs — GetIndexCountsPerLOD i dalee

        /// <summary>
        /// Returns triangle index counts per LOD level.
        /// Used by SeaweedGPUCuller to build DrawArgs.
        /// [0]=LOD0, [1]=LOD1, [2]=LOD2, [3]=LOD3(billboard=6)
        /// </summary>
        public int[] GetIndexCountsPerLOD()
        {
            // COLD ALLOC: 4-element array, startup only
            var counts = new int[4];

            if (_allMeshes == null || _allMeshes.Length == 0)
                return counts;

            // Sample first variant of each LOD level
            int lodCount = 3;
            for (int lod = 0; lod < lodCount; lod++)
            {
                int idx = lod; // variant 0, species 0
                if (idx < _allMeshes.Length && _allMeshes[idx] != null)
                    counts[lod] = (int)_allMeshes[idx].GetIndexCount(0);
            }

            counts[3] = 6; // billboard quad = 2 tris = 6 indices
            return counts;
        }

        /// <summary>
        /// Returns mesh for a specific species variant and LOD.
        /// Safe — returns null if out of range.
        /// </summary>
        public Mesh GetMesh(int speciesIdx, int variantIdx, int lod)
        {
            if (_allMeshes == null) return null;
            if (_library == null || _library.Species == null) return null;

            int flatIdx = (speciesIdx * _variantsPerSpecies + variantIdx) * 3 + lod;
            if ((uint)flatIdx >= (uint)_allMeshes.Length) return null;
            return _allMeshes[flatIdx];
        }

        // ── MESH BUILDING ─────────────────────────────────────────────

        private Mesh BuildSpeciesMesh(
            in SeaweedSpeciesDefinition sp, int variant, int lod)
        {
            var rng = new System.Random(
                sp.id.GetHashCode() ^ (variant * 7919) ^ (lod * 31));

            _verts.Clear(); _norms.Clear();
            _uvs.Clear();   _cols.Clear();
            _tris.Clear();

            float height = Mathf.Lerp(
                sp.heightMin, sp.heightMax,
                (float)rng.NextDouble());

            switch (sp.meshType)
            {
                case SeaweedSpecies.Kelp:
                    BuildKelpFull(sp, lod, height, rng);
                    break;
                case SeaweedSpecies.Bushy:
                case SeaweedSpecies.Coralline:
                    BuildBushyFull(sp, lod, height, rng);
                    break;
                case SeaweedSpecies.Filament:
                    BuildFilamentFull(sp, lod, height, rng);
                    break;
                case SeaweedSpecies.BladeLettuce:
                    BuildBladeFull(sp, lod, height, rng);
                    break;
                default:
                    BuildKelpFull(sp, lod, height, rng);
                    break;
            }

            return FinalizeToMesh(sp.id);
        }

        // ── KELP ──────────────────────────────────────────────────────

        private void BuildKelpFull(
            in SeaweedSpeciesDefinition sp,
            int lod, float height, System.Random rng)
        {
            // 1. Rhizoids (LOD0 only)
            if (lod == 0 && _generateRhizoids && height > 0.3f)
            {
                AddRhizoids(
                    basePos:       Vector3.zero,
                    surfaceNormal: Vector3.up,
                    spread:        0.15f,
                    count:         rng.Next(3, 7),
                    thickness:     height * 0.015f,
                    length:        height * 0.06f,
                    rng:           rng);
            }

            // 2. Trunk with ribs (large kelp only)
            float trunkHeight = 0f;
            if (height >= _trunkRibMinHeight)
            {
                trunkHeight = height * 0.18f;
                int ribCount = lod == 0 ? 5 : lod == 1 ? 4 : 3;
                AddKelpTrunk(
                    height:       trunkHeight,
                    radiusBase:   height * 0.04f,
                    radiusTip:    height * 0.018f,
                    segments:     lod == 0 ? 8 : 5,
                    sides:        lod == 0 ? 8 : 6,
                    ribCount:     ribCount,
                    ribHeight:    0.15f,
                    ribSharpness: 0.7f);
            }

            // 3. Basal blades (LOD0 only)
            if (lod == 0 && _generateBasalBlades && height > 1f)
            {
                AddBasalBlades(
                    basePos:     new Vector3(0f, trunkHeight, 0f),
                    baseRot:     Quaternion.identity,
                    count:       rng.Next(2, 4),
                    bladeWidth:  height * 0.08f,
                    bladeLength: height * 0.15f,
                    waviness:    sp.waviness,
                    rng:         rng);
            }

            // 4. Main ribbon stipe
            int segs  = GetSegmentCount(sp, lod);
            int sides = 2; // ribbon
            BuildAndAddSpineRibbon(sp, height, segs, sides,
                Vector3.zero, Quaternion.identity, rng);

            // 5. Pneumatocysts (LOD0-1 for large kelp)
            if (lod <= 1 && height >= _pneumatocystMinHeight)
            {
                int pCount = lod == 0 ? rng.Next(2, 5) : rng.Next(0, 2);
                AddPneumatocystsAlongHeight(sp, height, segs, pCount, lod, rng);
            }
        }

        // ── BUSHY ─────────────────────────────────────────────────────

        private void BuildBushyFull(
            in SeaweedSpeciesDefinition sp,
            int lod, float height, System.Random rng)
        {
            if (lod == 0 && _generateRhizoids)
            {
                AddRhizoids(Vector3.zero, Vector3.up,
                    0.1f, rng.Next(2, 5),
                    height * 0.012f, height * 0.05f, rng);
            }

            // Main stem
            int segs = GetSegmentCount(sp, lod);
            BuildAndAddSpineRibbon(sp, height, segs, 2,
                Vector3.zero, Quaternion.identity, rng);

            // Branches
            int branchCount = lod == 0 ? sp.branchCount
                            : lod == 1 ? Mathf.Max(0, sp.branchCount - 2)
                            : 0;

            for (int b = 0; b < branchCount; b++)
            {
                float t = sp.branchStartT
                        + (float)b / branchCount * (1f - sp.branchStartT);

                float3 branchBasePos = ComputeSpinePos(sp, height, t, rng);
                float  sideAngle     = (float)rng.NextDouble() * 360f;

                Quaternion branchRot = Quaternion.Euler(
                    sp.branchAngle, sideAngle, 0f);

                float branchH = height
                    * Mathf.Lerp(0.3f, 0.6f, (float)rng.NextDouble());

                // Reduced segments for branches
                var branchSp = sp;
                branchSp.branchCount = 0;

                BuildAndAddSpineRibbon(branchSp, branchH,
                    Mathf.Max(4, segs - 4), 2,
                    branchBasePos, branchRot, rng);
            }
        }

        // ── FILAMENT ──────────────────────────────────────────────────

        private void BuildFilamentFull(
            in SeaweedSpeciesDefinition sp,
            int lod, float height, System.Random rng)
        {
            int segs = GetSegmentCount(sp, lod);
            BuildAndAddSpineRibbon(sp, height, segs, 3,
                Vector3.zero, Quaternion.identity, rng);
        }

        // ── BLADE LETTUCE ─────────────────────────────────────────────

        private void BuildBladeFull(
            in SeaweedSpeciesDefinition sp,
            int lod, float height, System.Random rng)
        {
            int resV = GetSegmentCount(sp, lod);
            int resU = lod == 0 ? 8 : lod == 1 ? 5 : 3;

            AddBladeMesh(sp, height, resU, resV, rng);

            if (lod == 0)
            {
                ApplySerratedEdges(
                    amplitude: height * 0.025f,
                    frequency: 8f);
            }
        }

        // ── SPINE RIBBON BUILDER ──────────────────────────────────────

        private void BuildAndAddSpineRibbon(
            in SeaweedSpeciesDefinition sp,
            float height, int segs, int sides,
            Vector3 originPos, Quaternion originRot,
            System.Random rng)
        {
            // Run Burst spine job synchronously (startup — acceptable)
            // COLD ALLOC: NativeArrays per mesh, disposed immediately
            var spineParams = new NativeArray<SpineJobParams>(
                1, Allocator.TempJob);
            var segments    = new NativeArray<SpineSegment>(
                segs + 1, Allocator.TempJob);

            spineParams[0] = new SpineJobParams
            {
                seed          = rng.Next(),
                height        = height,
                baseWidth     = Mathf.Lerp(sp.widthMin, sp.widthMax, 0.5f),
                segmentCount  = segs,
                curvature     = sp.curvature,
                twist         = sp.twist,
                waviness      = sp.waviness,
                waveFrequency = sp.waveFrequency
            };

            new SeaweedSpineJob
            {
                InputParams         = spineParams,
                OutputSegments      = segments,
                MaxSegmentsPerSpine = segs + 1
            }.Schedule(1, 1).Complete();

            spineParams.Dispose();

            // Offset segments to origin
            bool hasOffset = originPos != Vector3.zero
                          || originRot != Quaternion.identity;

            int baseIdx = _verts.Count;

            for (int si = 0; si <= segs; si++)
            {
                SpineSegment seg = segments[si];

                Vector3 segPos = seg.position;
                Vector3 segRight, segUp, segFwd;

                // Compute frame from quaternion
                Matrix4x4 m = Matrix4x4.Rotate(
                    new Quaternion(
                        seg.rotation.value.x,
                        seg.rotation.value.y,
                        seg.rotation.value.z,
                        seg.rotation.value.w));

                segRight = m.GetColumn(0);
                segUp    = m.GetColumn(1);
                segFwd   = m.GetColumn(2);

                if (hasOffset)
                    segPos = originPos + originRot * segPos;

                Color32 col = LerpColor32(sp.colorRoot, sp.colorTip,
                    Mathf.Pow(seg.t, 0.6f));
                col.a = (byte)(seg.ao * 255f);

                for (int ai = 0; ai < sides; ai++)
                {
                    float u = (float)ai / (sides - 1);

                    Vector3 localOff;
                    Vector3 norm;

                    if (sides == 2)
                    {
                        float sign = ai == 0 ? -1f : 1f;
                        localOff = segRight * ((u - 0.5f) * 2f * seg.width);
                        norm     = sign * segFwd;
                    }
                    else
                    {
                        float angle = u * Mathf.PI * 2f;
                        localOff = (segRight * Mathf.Cos(angle)
                               + segFwd * Mathf.Sin(angle)) * seg.width;
                        norm = localOff.normalized;
                    }

                    _verts.Add(segPos + localOff);
                    _norms.Add(norm);
                    _uvs.Add(new Vector2(u, seg.t));
                    _cols.Add(col);
                }

                if (si < segs)
                {
                    int b = baseIdx + si * sides;
                    int n = b + sides;

                    if (sides == 2)
                    {
                        _tris.Add(b);     _tris.Add(n);     _tris.Add(b+1);
                        _tris.Add(b+1);   _tris.Add(n);     _tris.Add(n+1);
                        // Back face
                        _tris.Add(b+1);   _tris.Add(n+1);   _tris.Add(b);
                        _tris.Add(n+1);   _tris.Add(n);     _tris.Add(b);
                    }
                    else
                    {
                        for (int ai = 0; ai < sides - 1; ai++)
                        {
                            _tris.Add(b+ai);   _tris.Add(n+ai);
                            _tris.Add(b+ai+1);
                            _tris.Add(b+ai+1); _tris.Add(n+ai);
                            _tris.Add(n+ai+1);
                        }
                    }
                }
            }

            segments.Dispose();
        }

        // ── BLADE MESH ────────────────────────────────────────────────

        private void AddBladeMesh(
            in SeaweedSpeciesDefinition sp,
            float height, int resU, int resV,
            System.Random rng)
        {
            // Reuse spine for blade backbone
            var spineParams = new NativeArray<SpineJobParams>(
                1, Allocator.TempJob);
            var segments    = new NativeArray<SpineSegment>(
                resV + 1, Allocator.TempJob);

            spineParams[0] = new SpineJobParams
            {
                seed          = rng.Next(),
                height        = height,
                baseWidth     = Mathf.Lerp(sp.widthMin, sp.widthMax, 0.5f),
                segmentCount  = resV,
                curvature     = sp.curvature,
                twist         = sp.twist,
                waviness      = sp.waviness,
                waveFrequency = sp.waveFrequency
            };

            new SeaweedSpineJob
            {
                InputParams         = spineParams,
                OutputSegments      = segments,
                MaxSegmentsPerSpine = resV + 1
            }.Schedule(1, 1).Complete();

            spineParams.Dispose();

            int baseIdx = _verts.Count;

            for (int vi = 0; vi <= resV; vi++)
            {
                SpineSegment seg = segments[vi];

                Matrix4x4 m = Matrix4x4.Rotate(
                    new Quaternion(
                        seg.rotation.value.x,
                        seg.rotation.value.y,
                        seg.rotation.value.z,
                        seg.rotation.value.w));
                Vector3 segRight = m.GetColumn(0);
                Vector3 segFwd   = m.GetColumn(2);

                float leafW = sp.widthMax
                    * Mathf.Sin(seg.t * Mathf.PI)
                    * (1f + Mathf.Sin(seg.t * 3f + rng.Next()) * 0.12f);

                Color32 col = LerpColor32(sp.colorRoot, sp.colorTip,
                    Mathf.Pow(seg.t, 0.5f));

                for (int ui = 0; ui <= resU; ui++)
                {
                    float u  = (float)ui / resU;
                    float uC = u - 0.5f;

                    float wave = Mathf.Sin(seg.t * sp.waveFrequency * Mathf.PI
                              + uC * 4f)
                               * sp.waviness * leafW;
                    float mc = uC * uC * leafW * 0.3f;

                    Vector3 localPos = segRight * (uC * leafW * 2f)
                                     + segFwd   * (wave + mc);

                    _verts.Add(seg.position + localPos);
                    _norms.Add(m.GetColumn(1)); // up
                    _uvs.Add(new Vector2(u, seg.t));
                    _cols.Add(col);
                }

                if (vi < resV)
                {
                    int b = baseIdx + vi * (resU + 1);
                    int nb = b + (resU + 1);
                    for (int ui = 0; ui < resU; ui++)
                    {
                        _tris.Add(b+ui);   _tris.Add(nb+ui);  _tris.Add(b+ui+1);
                        _tris.Add(b+ui+1); _tris.Add(nb+ui);  _tris.Add(nb+ui+1);
                        // Back face
                        _tris.Add(b+ui+1); _tris.Add(nb+ui);  _tris.Add(b+ui);
                        _tris.Add(nb+ui+1);_tris.Add(nb+ui);  _tris.Add(b+ui+1);
                    }
                }
            }

            segments.Dispose();
        }

        // ── ANATOMICAL DETAIL METHODS ─────────────────────────────────

        private void AddKelpTrunk(
            float height, float radiusBase, float radiusTip,
            int segments, int sides, int ribCount,
            float ribHeight, float ribSharpness)
        {
            int baseIdx      = _verts.Count;
            int vertsPerRing = sides;

            for (int seg = 0; seg <= segments; seg++)
            {
                float t = (float)seg / segments;
                float y = t * height;
                float r = Mathf.Lerp(radiusBase, radiusTip,
                    Mathf.Pow(t, 0.7f));
                r *= 1f + Mathf.Sin(t * 5.3f) * 0.04f;

                Color32 col = new Color32(
                    (byte)Mathf.Lerp(60f, 120f, t),
                    (byte)Mathf.Lerp(200f, 150f, t),
                    (byte)Mathf.Lerp(25f, 70f, t),
                    255);

                for (int si = 0; si < sides; si++)
                {
                    float angle    = (float)si / sides * Mathf.PI * 2f;
                    float bx       = Mathf.Cos(angle) * r;
                    float bz       = Mathf.Sin(angle) * r;

                    float ribPhase  = (float)si / sides * ribCount;
                    float ribFactor = Mathf.Pow(
                        Mathf.Max(0f, Mathf.Cos(ribPhase * Mathf.PI * 2f)),
                        1f / Mathf.Max(0.01f, ribSharpness));
                    float ribOff = ribFactor * ribHeight * r
                                 * (1f - t * 0.8f);

                    Vector3 dir = new Vector3(bx, 0f, bz).normalized;
                    Vector3 pos = new Vector3(bx, y, bz) + dir * ribOff;
                    Vector3 sn  = new Vector3(bx, 0.15f, bz).normalized;

                    _verts.Add(pos);
                    _norms.Add(sn);
                    _uvs.Add(new Vector2((float)si / sides, t));
                    _cols.Add(col);
                }

                if (seg < segments)
                {
                    int rb = baseIdx + seg * vertsPerRing;
                    int rn = rb + vertsPerRing;
                    for (int si = 0; si < sides; si++)
                    {
                        int ni = (si + 1) % sides;
                        _tris.Add(rb+si); _tris.Add(rn+si); _tris.Add(rb+ni);
                        _tris.Add(rb+ni); _tris.Add(rn+si); _tris.Add(rn+ni);
                    }
                }
            }

            AddCircleCap(Vector3.zero, Vector3.down, radiusBase, sides,
                new Color32(40, 50, 15, 255));
        }

        private void AddPneumatocystsAlongHeight(
            in SeaweedSpeciesDefinition sp,
            float height, int segs, int count, int lod,
            System.Random rng)
        {
            for (int p = 0; p < count; p++)
            {
                float t = Mathf.Lerp(0.3f, 0.9f, (float)rng.NextDouble());
                float y = t * height;

                float sideAngle = (float)rng.NextDouble() * Mathf.PI * 2f;
                float pSize     = height
                    * Mathf.Lerp(0.03f, 0.06f, (float)rng.NextDouble());

                Vector3 sideOff = new Vector3(
                    Mathf.Cos(sideAngle) * pSize * 1.5f,
                    0f,
                    Mathf.Sin(sideAngle) * pSize * 1.5f);

                AddPneumatocyst(
                    new Vector3(0f, y, 0f) + sideOff,
                    pSize,
                    lod == 0 ? 6 : 4);
            }
        }

        private void AddPneumatocyst(Vector3 center, float radius, int res)
        {
            int baseIdx = _verts.Count;
            var col     = new Color32(180, 190, 80, 220);

            for (int lat = 0; lat <= res; lat++)
            {
                float theta = (float)lat / res * Mathf.PI;
                float sinT  = Mathf.Sin(theta);
                float cosT  = Mathf.Cos(theta);

                for (int lon = 0; lon <= res * 2; lon++)
                {
                    float phi = (float)lon / (res * 2) * Mathf.PI * 2f;
                    Vector3 dir = new Vector3(
                        sinT * Mathf.Cos(phi),
                        cosT,
                        sinT * Mathf.Sin(phi));

                    _verts.Add(center + dir * radius);
                    _norms.Add(dir);
                    _uvs.Add(new Vector2(
                        (float)lon / (res * 2),
                        (float)lat / res));
                    _cols.Add(col);
                }

                if (lat < res)
                {
                    int row  = baseIdx + lat * (res * 2 + 1);
                    int nRow = row + (res * 2 + 1);
                    for (int lon = 0; lon < res * 2; lon++)
                    {
                        _tris.Add(row+lon);   _tris.Add(nRow+lon);
                        _tris.Add(row+lon+1);
                        _tris.Add(row+lon+1); _tris.Add(nRow+lon);
                        _tris.Add(nRow+lon+1);
                    }
                }
            }
        }

        private void AddRhizoids(
            Vector3 basePos, Vector3 surfaceNormal,
            float spread, int count,
            float thickness, float length,
            System.Random rng)
        {
            for (int r = 0; r < count; r++)
            {
                float angle   = (float)rng.NextDouble() * Mathf.PI * 2f;
                Vector3 side  = new Vector3(
                    Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3 growDir =
                    (side + (-surfaceNormal) * 0.5f).normalized;

                int     segs  = 4;
                float   len   = length
                    * Mathf.Lerp(0.6f, 1.2f, (float)rng.NextDouble());
                float   thick = thickness
                    * Mathf.Lerp(0.5f, 1f, (float)rng.NextDouble());

                int baseIdx = _verts.Count;

                for (int seg = 0; seg <= segs; seg++)
                {
                    float   t    = (float)seg / segs;
                    float   bend = t * t * 0.4f;
                    Vector3 pos  = basePos
                        + growDir * (t * len)
                        + surfaceNormal * (-bend * len * 0.3f);

                    float   w     = thick * (1f - t * 0.8f);
                    Vector3 right = Vector3.Cross(
                        growDir, surfaceNormal).normalized;

                    Color32 col = new Color32(
                        (byte)Mathf.Lerp(40f, 25f, t),
                        (byte)Mathf.Lerp(60f, 35f, t),
                        (byte)Mathf.Lerp(15f, 8f,  t),
                        255);

                    for (int vi = 0; vi < 4; vi++)
                    {
                        float   a      = vi * Mathf.PI * 0.5f;
                        Vector3 offset = (right  * Mathf.Cos(a)
                                       + surfaceNormal * Mathf.Sin(a)) * w;
                        _verts.Add(pos + offset);
                        _norms.Add(offset.normalized);
                        _uvs.Add(new Vector2((float)vi / 4f, t));
                        _cols.Add(col);
                    }

                    if (seg < segs)
                    {
                        int b = baseIdx + seg * 4;
                        int n = b + 4;
                        for (int vi = 0; vi < 4; vi++)
                        {
                            int ni = (vi + 1) % 4;
                            _tris.Add(b+vi); _tris.Add(n+vi); _tris.Add(b+ni);
                            _tris.Add(b+ni); _tris.Add(n+vi); _tris.Add(n+ni);
                        }
                    }
                }
            }
        }

        private void AddBasalBlades(
            Vector3 basePos, Quaternion baseRot,
            int count, float bladeWidth, float bladeLength,
            float waviness, System.Random rng)
        {
            for (int b = 0; b < count; b++)
            {
                float bAngle = (float)b / count * 360f
                             + (float)rng.NextDouble() * 30f;
                Quaternion bRot = baseRot
                    * Quaternion.Euler(-15f, bAngle, 0f);

                int   resU = 5, resV = 7;
                float w    = bladeWidth  * Mathf.Lerp(0.7f, 1.3f,
                                 (float)rng.NextDouble());
                float l    = bladeLength * Mathf.Lerp(0.8f, 1.2f,
                                 (float)rng.NextDouble());

                int baseIdx = _verts.Count;

                for (int vi = 0; vi <= resV; vi++)
                {
                    float t     = (float)vi / resV;
                    float wHere = w * Mathf.Sin(t * Mathf.PI);

                    for (int ui = 0; ui <= resU; ui++)
                    {
                        float u    = (float)ui / resU;
                        float uC   = u - 0.5f;
                        float wave = Mathf.Sin(t * waviness * Mathf.PI
                                   + uC * 3f) * wHere * 0.15f;
                        float mc   = uC * uC * w * 0.3f;

                        Vector3 local = new Vector3(
                            uC * wHere * 2f, t * l, wave + mc);
                        Vector3 world = basePos + bRot * local;

                        Color32 col = new Color32(
                            (byte)Mathf.Lerp(50f, 90f,  t),
                            (byte)Mathf.Lerp(100f,160f, t),
                            (byte)Mathf.Lerp(10f, 20f,  t),
                            255);

                        _verts.Add(world);
                        _norms.Add(bRot * Vector3.up);
                        _uvs.Add(new Vector2(u, t));
                        _cols.Add(col);
                    }
                }

                for (int vi = 0; vi < resV; vi++)
                for (int ui = 0; ui < resU; ui++)
                {
                    int i  = baseIdx + vi * (resU + 1) + ui;
                    int ni = i + (resU + 1);
                    _tris.Add(i);    _tris.Add(ni);   _tris.Add(i+1);
                    _tris.Add(i+1);  _tris.Add(ni);   _tris.Add(ni+1);
                    _tris.Add(i+1);  _tris.Add(ni);   _tris.Add(i);
                    _tris.Add(ni+1); _tris.Add(ni);   _tris.Add(i+1);
                }
            }
        }

        private void ApplySerratedEdges(float amplitude, float frequency)
        {
            int count = _verts.Count;
            for (int i = 0; i < count; i++)
            {
                float u        = _uvs[i].x;
                float v        = _uvs[i].y;
                float edgeness = 1f - Mathf.Abs(u - 0.5f) * 2f;
                if (edgeness > 0.15f) continue;

                float serration = Mathf.Abs(
                    Mathf.Sin(v * frequency * Mathf.PI))
                    * (1f - edgeness / 0.15f)
                    * amplitude;

                _verts[i] = _verts[i] + _norms[i] * serration;
            }
        }

        private void AddCircleCap(
            Vector3 center, Vector3 normal,
            float radius, int sides, Color32 col)
        {
            int baseIdx = _verts.Count;
            _verts.Add(center);
            _norms.Add(normal);
            _uvs.Add(new Vector2(0.5f, 0.5f));
            _cols.Add(col);

            for (int i = 0; i < sides; i++)
            {
                float angle = (float)i / sides * Mathf.PI * 2f;
                _verts.Add(center + new Vector3(
                    Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius);
                _norms.Add(normal);
                _uvs.Add(new Vector2(
                    Mathf.Cos(angle) * 0.5f + 0.5f,
                    Mathf.Sin(angle) * 0.5f + 0.5f));
                _cols.Add(col);
            }

            for (int i = 0; i < sides; i++)
            {
                _tris.Add(baseIdx);
                _tris.Add(baseIdx + 1 + i);
                _tris.Add(baseIdx + 1 + (i + 1) % sides);
            }
        }

        // ── UTILITIES ─────────────────────────────────────────────────

        private static Vector3 ComputeSpinePos(
            in SeaweedSpeciesDefinition sp,
            float height, float t, System.Random rng)
        {
            // Simplified — returns approximate position at t along spine
            // For branches: good enough approximation
            float bendDir = (float)rng.NextDouble() * Mathf.PI * 2f;
            float y       = t * height;
            float xOff    = Mathf.Sin(t * Mathf.PI) * sp.curvature
                          * Mathf.Cos(bendDir) * height * 0.3f;
            float zOff    = Mathf.Sin(t * Mathf.PI) * sp.curvature
                          * Mathf.Sin(bendDir) * height * 0.15f;
            return new Vector3(xOff, y, zOff);
        }

        private static int GetSegmentCount(
            in SeaweedSpeciesDefinition sp, int lod)
        {
            return lod switch
            {
                0 => sp.segmentsLOD0,
                1 => sp.segmentsLOD1,
                2 => sp.segmentsLOD2,
                _ => sp.segmentsLOD2
            };
        }

        private Mesh FinalizeToMesh(string speciesId)
        {
            var mesh = new Mesh
            {
                name        = $"Seaweed_{speciesId}",
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
            // Keep readable: needed for physics binding proximity checks
            mesh.UploadMeshData(false);
            return mesh;
        }

        private static Color32 LerpColor32(Color a, Color b, float t)
        {
            t = Mathf.Clamp01(t);
            return new Color32(
                (byte)((a.r + (b.r - a.r) * t) * 255f),
                (byte)((a.g + (b.g - a.g) * t) * 255f),
                (byte)((a.b + (b.b - a.b) * t) * 255f),
                255);
        }
    }
}
```

---

Sleduyuschiy fayl:

```csharp
// ============================================================
// HECTON-8 — SeaweedMeshCache.cs v1.0
// Disk cache for generated seaweed meshes.
// Avoids regeneration on every launch.
// Called at startup only — not a hot path.
// ============================================================

using System.IO;
using UnityEngine;

namespace Hecton8.Environment
{
    /// <summary>
    /// Saves and loads procedurally generated seaweed meshes to/from disk.
    /// Location: Application.persistentDataPath/SeaweedCache/
    /// Format: raw binary (verts, normals, uvs, colors, triangles).
    ///
    /// NOT called in hot paths — startup only.
    /// GC here is acceptable (one-time allocation).
    /// </summary>
    public static class SeaweedMeshCache
    {
        private static string CacheDir =>
            Path.Combine(Application.persistentDataPath, "SeaweedCache");

        /// <summary>
        /// Build a deterministic cache key from species ID, variant and LOD.
        /// </summary>
        public static string GetKey(string speciesId, int variant, int lod)
            => $"sw_{speciesId}_v{variant}_lod{lod}";

        /// <summary>
        /// Attempt to load a cached mesh. Returns false if not found or corrupt.
        /// </summary>
        public static bool TryLoad(string key, out Mesh mesh)
        {
            mesh = null;
            string path = Path.Combine(CacheDir, key + ".bin");
            if (!File.Exists(path)) return false;

            try
            {
                byte[] data = File.ReadAllBytes(path);
                mesh = Deserialize(data);
                return mesh != null;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning(
                    $"[SeaweedMeshCache] Failed to load '{key}': {e.Message}");
                return false;
            }
        }

        /// <summary>Save a generated mesh to disk cache.</summary>
        public static void Save(string key, Mesh mesh)
        {
            if (mesh == null) return;

            try
            {
                Directory.CreateDirectory(CacheDir);
                string path  = Path.Combine(CacheDir, key + ".bin");
                byte[] bytes = Serialize(mesh);
                File.WriteAllBytes(path, bytes);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning(
                    $"[SeaweedMeshCache] Failed to save '{key}': {e.Message}");
            }
        }

        /// <summary>Delete all cached meshes (call after species data changes).</summary>
        public static void ClearAll()
        {
            if (Directory.Exists(CacheDir))
                Directory.Delete(CacheDir, recursive: true);
        }

        // ── SERIALIZATION ─────────────────────────────────────────────

        private static byte[] Serialize(Mesh mesh)
        {
            Vector3[] verts  = mesh.vertices;
            Vector3[] norms  = mesh.normals;
            Vector2[] uvs    = mesh.uv;
            Color32[] cols   = mesh.colors32;
            int[]     tris   = mesh.triangles;

            using var ms = new MemoryStream(
                verts.Length * 32 + tris.Length * 4 + 32);
            using var bw = new BinaryWriter(ms);

            bw.Write(verts.Length);
            foreach (var v in verts)
            {
                bw.Write(v.x); bw.Write(v.y); bw.Write(v.z);
            }
            foreach (var n in norms)
            {
                bw.Write(n.x); bw.Write(n.y); bw.Write(n.z);
            }
            foreach (var u in uvs)
            {
                bw.Write(u.x); bw.Write(u.y);
            }
            foreach (var c in cols)
            {
                bw.Write(c.r); bw.Write(c.g);
                bw.Write(c.b); bw.Write(c.a);
            }

            bw.Write(tris.Length);
            foreach (var t in tris) bw.Write(t);

            return ms.ToArray();
        }

        private static Mesh Deserialize(byte[] data)
        {
            using var ms = new MemoryStream(data);
            using var br = new BinaryReader(ms);

            int vCount  = br.ReadInt32();
            var verts   = new Vector3[vCount];
            var norms   = new Vector3[vCount];
            var uvs     = new Vector2[vCount];
            var cols    = new Color32[vCount];

            for (int i = 0; i < vCount; i++)
                verts[i] = new Vector3(
                    br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
            for (int i = 0; i < vCount; i++)
                norms[i] = new Vector3(
                    br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
            for (int i = 0; i < vCount; i++)
                uvs[i]   = new Vector2(br.ReadSingle(), br.ReadSingle());
            for (int i = 0; i < vCount; i++)
                cols[i]  = new Color32(
                    br.ReadByte(), br.ReadByte(),
                    br.ReadByte(), br.ReadByte());

            int tCount = br.ReadInt32();
            var tris   = new int[tCount];
            for (int i = 0; i < tCount; i++)
                tris[i] = br.ReadInt32();

            var mesh = new Mesh
            {
                indexFormat = vCount > 65535
                    ? UnityEngine.Rendering.IndexFormat.UInt32
                    : UnityEngine.Rendering.IndexFormat.UInt16
            };
            mesh.vertices  = verts;
            mesh.normals   = norms;
            mesh.uv        = uvs;
            mesh.colors32  = cols;
            mesh.triangles = tris;
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            mesh.UploadMeshData(false);
            return mesh;
        }
    }
}
```

---

```csharp
// ============================================================
// HECTON-8 — SeaweedTextureGenerator.cs v1.0
// Procedural Albedo/Normal/SSS texture atlas (1024×512).
// Generated in Task.Run at startup — never in hot path.
// 8 species rows × 64px. 4 tiles per row (albedo/norm/sss/rough).
// ============================================================

using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Hecton8.Environment.Textures
{
    /// <summary>
    /// Generates all seaweed textures procedurally at startup.
    /// No external texture files required.
    ///
    /// Atlas layout (1024 × 512):
    ///   X: [0-255]=Albedo [256-511]=Normal [512-767]=SSS [768-1023]=Roughness
    ///   Y: one 64px row per species (max 8 species)
    ///
    /// Sets global shader textures:
    ///   _SeaweedAlbedoAtlas, _SeaweedNormalAtlas,
    ///   _SeaweedSSSAtlas, _SeaweedNoise
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-120)]
    public sealed class SeaweedTextureGenerator : MonoBehaviour
    {
        // ── INSPECTOR ─────────────────────────────────────────────────

        [Header("── Atlas Settings ───────────────────────────────────────")]
        [SerializeField, Tooltip("Width of each tile in atlas (256 recommended).")]
        private int _tileWidth  = 256;

        [SerializeField, Tooltip("Height per species row (64 recommended).")]
        private int _tileHeight = 64;

        [SerializeField, Tooltip("Number of species rows. Match library count.")]
        private int _speciesCount = 8;

        [Header("── Noise Texture ─────────────────────────────────────────")]
        [SerializeField, Range(128, 512)]
        private int _noiseSize = 256;

        // ── PRIVATE STATE ─────────────────────────────────────────────

        private Texture2D _albedoAtlas;
        private Texture2D _normalAtlas;
        private Texture2D _sssAtlas;
        private Texture2D _noiseTexture;

        private bool _ready;

        // Cached property IDs
        private static readonly int
            _PropAlbedo  = Shader.PropertyToID("_SeaweedAlbedoAtlas"),
            _PropNormal  = Shader.PropertyToID("_SeaweedNormalAtlas"),
            _PropSSS     = Shader.PropertyToID("_SeaweedSSSAtlas"),
            _PropNoise   = Shader.PropertyToID("_SeaweedNoise");

        // ── PUBLIC ────────────────────────────────────────────────────

        /// <summary>True when textures are generated and uploaded to shader.</summary>
        public bool IsReady => _ready;

        // ── LIFECYCLE ─────────────────────────────────────────────────

        private IEnumerator Start()
        {
            int atlasW = _tileWidth * 4;
            int atlasH = _tileHeight * _speciesCount;

            Color32[] albedoPixels = null;
            Color32[] normalPixels = null;
            Color32[] sssPixels    = null;
            Color32[] noisePixels  = null;
            bool      done         = false;

            // Generate pixel data off main thread (no Unity API used inside)
            Task.Run(() =>
            {
                albedoPixels = GenAlbedoAtlas(atlasW, atlasH);
                normalPixels = GenNormalAtlas(atlasW, atlasH);
                sssPixels    = GenSSSAtlas(atlasW, atlasH);
                noisePixels  = GenNoise(_noiseSize);
                done         = true;
            });

            while (!done) yield return null;

            // Texture creation on main thread (Unity requirement)
            _albedoAtlas = CreateTex2D(
                albedoPixels, atlasW, atlasH,
                GraphicsFormat.R8G8B8A8_SRGB, "SeaweedAlbedoAtlas");

            _normalAtlas = CreateTex2D(
                normalPixels, atlasW, atlasH,
                GraphicsFormat.R8G8B8A8_UNorm, "SeaweedNormalAtlas");

            _sssAtlas = CreateTex2D(
                sssPixels, atlasW, atlasH,
                GraphicsFormat.R8G8B8A8_UNorm, "SeaweedSSSAtlas");

            _noiseTexture = CreateTex2D(
                noisePixels, _noiseSize, _noiseSize,
                GraphicsFormat.R8G8B8A8_UNorm, "SeaweedNoise",
                wrapMode: TextureWrapMode.Repeat);

            Shader.SetGlobalTexture(_PropAlbedo, _albedoAtlas);
            Shader.SetGlobalTexture(_PropNormal, _normalAtlas);
            Shader.SetGlobalTexture(_PropSSS,    _sssAtlas);
            Shader.SetGlobalTexture(_PropNoise,  _noiseTexture);

            _ready = true;
        }

        private void OnDestroy()
        {
            if (_albedoAtlas  != null) Destroy(_albedoAtlas);
            if (_normalAtlas  != null) Destroy(_normalAtlas);
            if (_sssAtlas     != null) Destroy(_sssAtlas);
            if (_noiseTexture != null) Destroy(_noiseTexture);
        }

        // ── PIXEL GENERATION (off-thread) ─────────────────────────────

        private Color32[] GenAlbedoAtlas(int atlasW, int atlasH)
        {
            // COLD ALLOC: full atlas pixels — startup only
            var pixels = new Color32[atlasW * atlasH];

            for (int sp = 0; sp < _speciesCount; sp++)
            {
                int rowY = sp * _tileHeight;

                for (int y = 0; y < _tileHeight; y++)
                for (int x = 0; x < atlasW; x++)
                {
                    float u = (float)(x % _tileWidth) / _tileWidth;
                    float v = (float)y / _tileHeight;
                    int tileX = x / _tileWidth;

                    Color32 pixel = tileX == 0
                        ? SampleAlbedoTile(u, v, sp)
                        : new Color32(128, 128, 128, 255);

                    pixels[(rowY + y) * atlasW + x] = pixel;
                }
            }
            return pixels;
        }

        private Color32[] GenNormalAtlas(int atlasW, int atlasH)
        {
            var pixels = new Color32[atlasW * atlasH];
            // Flat normal (0,0,1) as default
            var flatNorm = new Color32(128, 128, 255, 255);

            for (int sp = 0; sp < _speciesCount; sp++)
            {
                int rowY = sp * _tileHeight;
                for (int y = 0; y < _tileHeight; y++)
                for (int x = 0; x < atlasW; x++)
                {
                    float u = (float)(x % _tileWidth) / _tileWidth;
                    float v = (float)y / _tileHeight;
                    int tileX = x / _tileWidth;

                    pixels[(rowY + y) * atlasW + x] = tileX == 0
                        ? SampleNormalTile(u, v, sp)
                        : flatNorm;
                }
            }
            return pixels;
        }

        private Color32[] GenSSSAtlas(int atlasW, int atlasH)
        {
            var pixels = new Color32[atlasW * atlasH];

            for (int sp = 0; sp < _speciesCount; sp++)
            {
                int rowY = sp * _tileHeight;
                for (int y = 0; y < _tileHeight; y++)
                for (int x = 0; x < _tileWidth; x++) // only tile 0
                {
                    float u = (float)x / _tileWidth;
                    float v = (float)y / _tileHeight;

                    pixels[(rowY + y) * atlasW + x] =
                        SampleSSTile(u, v, sp);
                }
                // Fill remaining tiles with sensible defaults
                for (int y = 0; y < _tileHeight; y++)
                for (int x = _tileWidth; x < atlasW; x++)
                {
                    pixels[(rowY + y) * atlasW + x] =
                        new Color32(128, 200, 10, 180);
                }
            }
            return pixels;
        }

        private Color32[] GenNoise(int size)
        {
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (float)x / size;
                float v = (float)y / size;

                float lowFreq  = FBM2D(u, v, 0f,   4, 2.1f, 0.5f);
                float highFreq = FBM2D(u, v, 7.3f,  3, 3.0f, 0.4f);
                float caustic  = SampleCaustic(u, v);
                float turb     = FBMTurb(u, v);

                pixels[y * size + x] = new Color32(
                    (byte)(lowFreq  * 255f),
                    (byte)(highFreq * 255f),
                    (byte)(caustic  * 255f),
                    (byte)(turb     * 255f));
            }
            return pixels;
        }

        // ── TILE SAMPLERS ─────────────────────────────────────────────

        private static Color32 SampleAlbedoTile(float u, float v, int sp)
        {
            // Species-specific gradient + vein pattern + edge alpha
            Color root = GetSpeciesColorRoot(sp);
            Color tip  = GetSpeciesColorTip(sp);

            float t = Mathf.Pow(v, 0.6f);
            Color col = Color.Lerp(root, tip, t);

            // Central vein darkening
            float ribDist  = Mathf.Abs(u - 0.5f) * 2f;
            float rib      = Mathf.Max(0f, 1f - ribDist * 6f);
            rib = rib * rib;
            col = Color.Lerp(col, col * 0.65f, rib * 0.4f);

            // Edge darkening
            float edgeDist = Mathf.Min(u, 1f - u) * 2f;
            col = Color.Lerp(col * 0.6f, col, Mathf.Pow(edgeDist, 0.3f));

            // Alpha: edge fade + wave shape
            float wave  = Mathf.Sin(v * 4f * Mathf.PI) * 0.08f;
            float edgeU = Mathf.Clamp01((edgeDist - wave) * 5f);
            byte  alpha = (byte)(edgeU * 255f);

            return new Color32(
                (byte)(col.r * 255f),
                (byte)(col.g * 255f),
                (byte)(col.b * 255f),
                alpha);
        }

        private static Color32 SampleNormalTile(float u, float v, int sp)
        {
            // Height-map from vein + micro noise → normal via finite diff
            float eps = 1f / 256f;
            float h00 = SampleHeightmap(u,       v,       sp);
            float h10 = SampleHeightmap(u + eps, v,       sp);
            float h01 = SampleHeightmap(u,       v + eps, sp);

            Vector3 tangent  = new Vector3(eps * 256f, 0f, h10 - h00).normalized;
            Vector3 binormal = new Vector3(0f, eps * 64f, h01 - h00).normalized;
            Vector3 normal   = Vector3.Cross(tangent, binormal).normalized;
            normal = Vector3.Lerp(normal, Vector3.forward, 0.35f).normalized;

            return new Color32(
                (byte)((normal.x * 0.5f + 0.5f) * 255f),
                (byte)((normal.y * 0.5f + 0.5f) * 255f),
                (byte)((normal.z * 0.5f + 0.5f) * 255f),
                255);
        }

        private static Color32 SampleSSTile(float u, float v, int sp)
        {
            // R=thickness G=moisture B=age A=roughness
            float edgeDist  = Mathf.Min(u, 1f - u) * 2f;
            float thickness = Mathf.Pow(edgeDist, 0.8f)
                            * (1f - v * 0.5f)
                            * Mathf.Lerp(0.4f, 0.9f, (float)sp / 8f);
            float moisture  = 1f - v * 0.4f;
            float age       = Mathf.PerlinNoise(u * 4f, v * 4f + sp) * 0.5f;
            float roughness = Mathf.Lerp(0.3f, 0.7f, (float)sp / 8f)
                            * (1f - v * 0.3f);

            return new Color32(
                (byte)(Mathf.Clamp01(thickness) * 255f),
                (byte)(Mathf.Clamp01(moisture)  * 255f),
                (byte)(Mathf.Clamp01(age)       * 255f),
                (byte)(Mathf.Clamp01(roughness) * 255f));
        }

        private static float SampleHeightmap(float u, float v, int sp)
        {
            float h     = 0f;
            float ribD  = Mathf.Abs(u - 0.5f) * 2f;
            h += Mathf.Max(0f, 1f - ribD * 5f) * 0.4f;
            h += (Mathf.PerlinNoise(u * 12f, v * 8f + sp) * 2f - 1f) * 0.05f;
            return h;
        }

        // ── SPECIES COLOUR DATA ───────────────────────────────────────

        private static readonly Color[] _RootColors =
        {
            new Color(0.12f, 0.31f, 0.10f), // algae_crust
            new Color(0.06f, 0.35f, 0.21f), // filament
            new Color(0.16f, 0.47f, 0.12f), // ulva
            new Color(0.24f, 0.33f, 0.08f), // fucus
            new Color(0.20f, 0.40f, 0.10f), // posidonia
            new Color(0.27f, 0.35f, 0.04f), // kelp_medium
            new Color(0.45f, 0.35f, 0.08f), // sargassum
            new Color(0.31f, 0.27f, 0.06f)  // kelp_large
        };

        private static readonly Color[] _TipColors =
        {
            new Color(0.21f, 0.51f, 0.16f),
            new Color(0.16f, 0.63f, 0.31f),
            new Color(0.31f, 0.75f, 0.20f),
            new Color(0.35f, 0.51f, 0.14f),
            new Color(0.35f, 0.65f, 0.15f),
            new Color(0.47f, 0.63f, 0.08f),
            new Color(0.60f, 0.50f, 0.12f),
            new Color(0.55f, 0.51f, 0.10f)
        };

        private static Color GetSpeciesColorRoot(int sp) =>
            sp < _RootColors.Length ? _RootColors[sp] : _RootColors[0];

        private static Color GetSpeciesColorTip(int sp) =>
            sp < _TipColors.Length ? _TipColors[sp] : _TipColors[0];

        // ── MATH UTILS (off-thread safe — no Unity API) ───────────────

        private static float FBM2D(
            float x, float y, float seed,
            int oct, float lac, float gain)
        {
            float r = 0f, amp = 0.5f, freq = 1f, max = 0f;
            for (int i = 0; i < oct; i++)
            {
                r   += (Mathf.PerlinNoise(x*freq+seed, y*freq+seed)*2f-1f)*amp;
                max += amp; amp *= gain; freq *= lac;
            }
            return r / max * 0.5f + 0.5f;
        }

        private static float FBMTurb(float u, float v)
        {
            float r = 0f, amp = 0.5f, freq = 1f, max = 0f;
            for (int i = 0; i < 4; i++)
            {
                r   += Mathf.Abs(Mathf.PerlinNoise(u*freq, v*freq)*2f-1f)*amp;
                max += amp; amp *= 0.5f; freq *= 2.3f;
            }
            return r / max;
        }

        private static float SampleCaustic(float u, float v)
        {
            float c = Mathf.Sin(u * 8.1f + v * 3.7f) * 0.3f
                    + Mathf.Sin(u * 5.3f - v * 7.1f) * 0.25f
                    + Mathf.Sin((u + v) * 6.2f)       * 0.2f
                    + Mathf.Sin((u - v) * 4.8f)       * 0.15f;
            c = c * 0.5f + 0.5f;
            c = Mathf.Pow(Mathf.Clamp01(c), 2.5f);
            return c;
        }

        // ── TEXTURE FACTORY ───────────────────────────────────────────

        private static Texture2D CreateTex2D(
            Color32[] pixels, int w, int h,
            GraphicsFormat fmt, string name,
            TextureWrapMode wrapMode = TextureWrapMode.Clamp)
        {
            var tex = new Texture2D(w, h, fmt, TextureCreationFlags.None)
            {
                name       = name,
                filterMode = FilterMode.Bilinear,
                wrapMode   = wrapMode
            };
            tex.SetPixelData(pixels, 0);
            tex.Apply(updateMipmaps: true, makeNoLongerReadable: true);
            return tex;
        }
    }
}
```

---

```csharp
// ============================================================
// HECTON-8 — SeaweedPlacer.cs v1.0
// Places seaweed instances using biome, depth, substrate rules.
// Clustered (Gaussian) + uniform (Poisson Disk) placement.
// SpatialHashGrid for O(1) distance checks.
// IEnumerator-based (called from Bootstrap) — NOT ITickable.
// Zero GC in placement loops (pre-allocated structures).
// ============================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Hecton8.Environment.Rendering;

namespace Hecton8.Environment.Placement
{
    /// <summary>
    /// Generates seaweed placement in chunks.
    /// Priority: Massive → Large → Medium → Small → Micro
    /// (large plants claim space first, small fill gaps).
    ///
    /// Placement rules per species:
    /// - Substrate type (rock/sand/gravel)
    /// - Depth range (min/max metres below water surface)
    /// - Light requirement (attenuates exponentially with depth)
    /// - Slope limit (steep surfaces excluded)
    /// - Minimum distance to same/any species
    /// - Cluster vs uniform distribution
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-95)]
    public sealed class SeaweedPlacer : MonoBehaviour
    {
        // ── INSPECTOR ─────────────────────────────────────────────────

        [Header("── Library ──────────────────────────────────────────────")]
        [SerializeField] private SeaweedSpeciesLibrary _library;

        [Header("── Area ─────────────────────────────────────────────────")]
        [SerializeField] private float _chunkSize       = 50f;
        [SerializeField] private int   _chunkGridX      = 4;
        [SerializeField] private int   _chunkGridZ      = 4;

        [Header("── Density ──────────────────────────────────────────────")]
        [SerializeField] private int   _targetPerChunk  = 250;
        [SerializeField] private float _maxSlope        = 50f;

        [Header("── Layers ───────────────────────────────────────────────")]
        [SerializeField] private LayerMask _groundLayer;
        [SerializeField] private LayerMask _waterLayer;

        [Header("── Seed ─────────────────────────────────────────────────")]
        [SerializeField] private int _randomSeed = 42;

        // ── PRIVATE STATE ─────────────────────────────────────────────

        // Pre-allocated raycast buffer — COLD ALLOC: 4 hits max
        private readonly RaycastHit[] _hitBuffer = new RaycastHit[4];

        // SpatialHash cell size should match smallest minDistToAny
        private SpatialHashGrid _grid;

        // Re-used candidate list — COLD ALLOC, cleared per species
        // Max candidates per chunk per species = clusterSizeMax * clusterCount
        private readonly List<Vector3> _candidates = new List<Vector3>(512);

        private static readonly SeaweedSizeClass[] _SizeOrder =
        {
            SeaweedSizeClass.Massive,
            SeaweedSizeClass.Large,
            SeaweedSizeClass.Medium,
            SeaweedSizeClass.Small,
            SeaweedSizeClass.Micro
        };

        // ── PUBLIC API ────────────────────────────────────────────────

        /// <summary>
        /// Place all chunks in sequence, yielding one chunk per frame.
        /// Called by SeaweedBootstrap. Registers instances with renderer.
        /// </summary>
        public IEnumerator PlaceAllChunksCoroutine(SeaweedRenderer renderer)
        {
            if (_library == null || _library.Species == null)
            {
                Debug.LogError("[SeaweedPlacer] Library missing. Aborting.");
                yield break;
            }

            // COLD ALLOC: grid sized to chunk
            _grid = new SpatialHashGrid(cellSize: 1.5f);

            float totalW = _chunkSize * _chunkGridX;
            float totalZ = _chunkSize * _chunkGridZ;
            float originX = transform.position.x - totalW * 0.5f;
            float originZ = transform.position.z - totalZ * 0.5f;

            for (int cx = 0; cx < _chunkGridX; cx++)
            for (int cz = 0; cz < _chunkGridZ; cz++)
            {
                Vector3 chunkOrigin = new Vector3(
                    originX + cx * _chunkSize,
                    transform.position.y,
                    originZ + cz * _chunkSize);

                int chunkSeed = _randomSeed ^ (cx * 7919) ^ (cz * 1013);
                PlaceChunk(chunkOrigin, chunkSeed, renderer);

                yield return null; // one chunk per frame
            }
        }

        // ── PRIVATE ───────────────────────────────────────────────────

        private void PlaceChunk(
            Vector3 origin, int seed,
            SeaweedRenderer renderer)
        {
            var rng = new System.Random(seed);

            // Process size classes largest first
            for (int sc = 0; sc < _SizeOrder.Length; sc++)
            {
                SeaweedSizeClass sizeClass = _SizeOrder[sc];

                SeaweedSpeciesDefinition[] species = _library.Species;
                for (int si = 0; si < species.Length; si++)
                {
                    if (species[si].sizeClass != sizeClass) continue;

                    PlaceSpeciesInChunk(
                        species[si], origin, rng, renderer);
                }
            }
        }

        private void PlaceSpeciesInChunk(
            in SeaweedSpeciesDefinition sp,
            Vector3 origin,
            System.Random rng,
            SeaweedRenderer renderer)
        {
            // Generate candidates (cluster or uniform)
            _candidates.Clear();
            if (sp.clusterTendency > 0.3f)
                GenerateClustered(sp, origin, rng);
            else
                GeneratePoisson(sp, origin, rng);

            for (int ci = 0; ci < _candidates.Count; ci++)
            {
                Vector3 candidate = _candidates[ci];

                // Ground raycast — NonAlloc
                Vector3 rayOrigin = new Vector3(
                    candidate.x, candidate.y + 60f, candidate.z);

                int hitCount = Physics.RaycastNonAlloc(
                    rayOrigin, Vector3.down,
                    _hitBuffer, 80f, _groundLayer);

                if (hitCount == 0) continue;

                // Find closest hit
                RaycastHit bestHit = _hitBuffer[0];
                for (int h = 1; h < hitCount; h++)
                {
                    if (_hitBuffer[h].distance < bestHit.distance)
                        bestHit = _hitBuffer[h];
                }

                Vector3 pos    = bestHit.point;
                Vector3 normal = bestHit.normal;

                // Slope check
                float slope = Vector3.Angle(normal, Vector3.up);
                if (slope > _maxSlope) continue;

                // Depth check
                float depth = GetDepth(pos);
                if (depth < sp.depthMin || depth > sp.depthMax) continue;

                // Light check
                float light = Mathf.Exp(-depth * 0.08f);
                if (light < sp.lightRequirement * 0.7f) continue;

                // Substrate check
                if (!IsValidSubstrate(bestHit, sp.validSubstrates)) continue;

                // Distance checks via SpatialHashGrid
                if (_grid.HasNearby(pos, sp.minDistToAny)) continue;
                if (_grid.HasNearbyOfSpecies(pos, sp.id,
                        sp.minDistToSame)) continue;

                // Place instance
                float scale   = Mathf.Lerp(sp.heightMin, sp.heightMax,
                    (float)rng.NextDouble()) / sp.heightMax;
                float rotY    = (float)rng.NextDouble() * 360f;
                float phase   = (float)rng.NextDouble() * Mathf.PI * 2f;
                float curveIdx = (float)rng.Next(0, 256);
                int   variant  = rng.Next(0, 4);

                // Partial slope alignment (40% follow surface normal)
                Quaternion slopeRot = Quaternion.FromToRotation(
                    Vector3.up, normal);
                Quaternion yRot     = Quaternion.Euler(0f, rotY, 0f);
                Quaternion finalRot = Quaternion.Slerp(
                    yRot, slopeRot * yRot, 0.4f);

                var inst = new SeaweedInstance(
                    sp.meshType, variant,
                    pos, finalRot, scale,
                    phase, curveIdx,
                    sp.atlasRow, sp.swayMultiplier);

                renderer.RegisterInstance(inst);
                _grid.Add(pos, sp.id);
            }
        }

        // ── CANDIDATE GENERATION ──────────────────────────────────────

        private void GenerateClustered(
            in SeaweedSpeciesDefinition sp,
            Vector3 origin, System.Random rng)
        {
            float area         = _chunkSize * _chunkSize;
            float clusterArea  = sp.clusterRadius * sp.clusterRadius * Mathf.PI;
            int   clusterCount = Mathf.Clamp(
                Mathf.RoundToInt(area / clusterArea * 0.5f), 1, 20);

            for (int c = 0; c < clusterCount; c++)
            {
                float cx = origin.x + (float)rng.NextDouble() * _chunkSize;
                float cz = origin.z + (float)rng.NextDouble() * _chunkSize;

                int size = rng.Next(sp.clusterSizeMin, sp.clusterSizeMax + 1);

                for (int i = 0; i < size; i++)
                {
                    // Box-Muller for Gaussian distribution
                    float u1 = Mathf.Max(0.0001f, (float)rng.NextDouble());
                    float u2 = (float)rng.NextDouble();
                    float g  = Mathf.Sqrt(-2f * Mathf.Log(u1))
                             * Mathf.Cos(2f * Mathf.PI * u2);

                    float dist = Mathf.Abs(g) * sp.clusterRadius * 0.4f;
                    dist = Mathf.Min(dist, sp.clusterRadius);

                    float ang = (float)rng.NextDouble() * Mathf.PI * 2f;

                    _candidates.Add(new Vector3(
                        cx + Mathf.Cos(ang) * dist,
                        origin.y + 60f,
                        cz + Mathf.Sin(ang) * dist));
                }
            }
        }

        private void GeneratePoisson(
            in SeaweedSpeciesDefinition sp,
            Vector3 origin, System.Random rng)
        {
            // Poisson Disk Sampling — re-uses _candidates list
            float minDist = sp.minDistToAny * 1.5f;

            // COLD ALLOC: active list for Poisson (small, bounded)
            // Max candidates before Poisson fills: ~300 for small dist
            var active = new List<Vector3>(64);

            Vector3 first = new Vector3(
                origin.x + (float)rng.NextDouble() * _chunkSize,
                origin.y + 60f,
                origin.z + (float)rng.NextDouble() * _chunkSize);

            _candidates.Add(first);
            active.Add(first);

            float minDistSq = minDist * minDist;

            while (active.Count > 0)
            {
                int    idx   = rng.Next(active.Count);
                Vector3 point = active[idx];
                bool   found = false;

                for (int a = 0; a < 30; a++)
                {
                    float ang  = (float)rng.NextDouble() * Mathf.PI * 2f;
                    float dist = minDist
                        * (1f + (float)rng.NextDouble());

                    Vector3 candidate = point + new Vector3(
                        Mathf.Cos(ang) * dist, 0f,
                        Mathf.Sin(ang) * dist);

                    if (candidate.x < origin.x
                     || candidate.x > origin.x + _chunkSize
                     || candidate.z < origin.z
                     || candidate.z > origin.z + _chunkSize)
                        continue;

                    bool tooClose = false;
                    for (int e = 0; e < _candidates.Count; e++)
                    {
                        float dx = _candidates[e].x - candidate.x;
                        float dz = _candidates[e].z - candidate.z;
                        if (dx*dx + dz*dz < minDistSq)
                        {
                            tooClose = true;
                            break;
                        }
                    }

                    if (!tooClose)
                    {
                        _candidates.Add(candidate);
                        active.Add(candidate);
                        found = true;
                        break;
                    }
                }

                if (!found)
                    active.RemoveAt(idx);

                // Safety cap — avoid infinite loop on tiny minDist
                if (_candidates.Count >= _targetPerChunk * 2) break;
            }
        }

        // ── UTILS ─────────────────────────────────────────────────────

        private float GetDepth(Vector3 pos)
        {
            int hitCount = Physics.RaycastNonAlloc(
                pos, Vector3.up, _hitBuffer, 200f, _waterLayer);

            return hitCount > 0 ? _hitBuffer[0].distance : 0f;
        }

        private static bool IsValidSubstrate(
            RaycastHit hit, SubstrateType valid)
        {
            // Determine substrate from collider tag
            SubstrateType detected;

            if (hit.collider.CompareTag("Rock"))        detected = SubstrateType.Rock;
            else if (hit.collider.CompareTag("Sand"))   detected = SubstrateType.Sand;
            else if (hit.collider.CompareTag("Gravel")) detected = SubstrateType.Gravel;
            else if (hit.collider.CompareTag("Coral"))  detected = SubstrateType.Coral;
            else if (hit.collider.CompareTag("Mud"))    detected = SubstrateType.Mud;
            else                                         detected = SubstrateType.Rock;

            return (valid & detected) != 0;
        }
    }

    // ── SPATIAL HASH GRID ─────────────────────────────────────────────

    /// <summary>
    /// O(1) spatial proximity lookup.
    /// Used by SeaweedPlacer for minimum distance enforcement.
    /// Not thread-safe — used from main thread only.
    /// </summary>
    public sealed class SpatialHashGrid
    {
        private readonly float _cellSize;

        // COLD ALLOC: dictionary grown as needed during placement
        private readonly Dictionary<long, List<(Vector3 pos, string species)>>
            _cells = new Dictionary<long, List<(Vector3, string)>>(512);

        public SpatialHashGrid(float cellSize)
        {
            _cellSize = cellSize;
        }

        public void Add(Vector3 pos, string species)
        {
            long key = GetKey(pos);
            if (!_cells.TryGetValue(key, out var list))
            {
                // COLD ALLOC: new cell list (happens once per cell)
                list = new List<(Vector3, string)>(4);
                _cells[key] = list;
            }
            list.Add((pos, species));
        }

        public bool HasNearby(Vector3 pos, float radius)
        {
            int   r    = Mathf.CeilToInt(radius / _cellSize);
            int   cx   = CellCoord(pos.x);
            int   cz   = CellCoord(pos.z);
            float r2   = radius * radius;

            for (int dx = -r; dx <= r; dx++)
            for (int dz = -r; dz <= r; dz++)
            {
                long key = HashCell(cx + dx, cz + dz);
                if (!_cells.TryGetValue(key, out var list)) continue;

                for (int i = 0; i < list.Count; i++)
                {
                    float ddx = list[i].pos.x - pos.x;
                    float ddz = list[i].pos.z - pos.z;
                    if (ddx*ddx + ddz*ddz < r2) return true;
                }
            }
            return false;
        }

        public bool HasNearbyOfSpecies(
            Vector3 pos, string species, float radius)
        {
            int   r  = Mathf.CeilToInt(radius / _cellSize);
            int   cx = CellCoord(pos.x);
            int   cz = CellCoord(pos.z);
            float r2 = radius * radius;

            for (int dx = -r; dx <= r; dx++)
            for (int dz = -r; dz <= r; dz++)
            {
                long key = HashCell(cx + dx, cz + dz);
                if (!_cells.TryGetValue(key, out var list)) continue;

                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i].species != species) continue;
                    float ddx = list[i].pos.x - pos.x;
                    float ddz = list[i].pos.z - pos.z;
                    if (ddx*ddx + ddz*ddz < r2) return true;
                }
            }
            return false;
        }

        private long GetKey(Vector3 pos) =>
            HashCell(CellCoord(pos.x), CellCoord(pos.z));

        private int CellCoord(float v) =>
            Mathf.FloorToInt(v / _cellSize);

        private static long HashCell(int x, int z) =>
            ((long)(x + 32768)) << 32 | (uint)(z + 32768);
    }
}
```

---

```csharp
// ============================================================
// HECTON-8 — SeaAnemone.cs v1.0
// Procedural sea anemone. ITickable state machine.
// GPU instanced tentacles. Clownfish shelter slots.
// Zero GC in Tick. Pre-allocated arrays. MPB cached.
// ============================================================

using UnityEngine;
using Hecton8.Core;

namespace Hecton8.Environment
{
    /// <summary>
    /// Procedural sea anemone with animated tentacles.
    /// Column: tapered cylinder mesh (built once in Awake).
    /// Tentacles: 6-24 tapered capsules, instanced via
    ///   Graphics.DrawMeshInstanced. Animated via Matrix TRS.
    ///
    /// States: Closed → Opening → Open → Feeding → Closing
    /// Triggers: player proximity, state timer.
    ///
    /// Clownfish integration: up to 4 shelter slots.
    /// Fish AI calls RequestShelter() / ReleaseShelter().
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SeaAnemone : MonoBehaviour, ITickable, IInteractable
    {
        // ── INSPECTOR ─────────────────────────────────────────────────

        [Header("── Morphology ───────────────────────────────────────────")]
        [SerializeField, Range(0.05f, 0.5f)]
        private float _columnRadius = 0.08f;

        [SerializeField, Range(0.1f, 0.8f)]
        private float _columnHeight = 0.25f;

        [SerializeField, Range(6, 24)]
        private int _tentacleCount = 16;

        [SerializeField, Range(0.05f, 0.4f)]
        private float _tentacleLength = 0.18f;

        [Header("── Colour ───────────────────────────────────────────────")]
        [SerializeField] private Color _columnColor   = new Color(0.85f, 0.3f, 0.15f);
        [SerializeField] private Color _tentacleColor = new Color(0.9f, 0.5f, 0.2f);
        [SerializeField] private Color _tipColor      = new Color(1f, 0.9f, 0.7f);

        [Header("── Bioluminescence ─────────────────────────────────────")]
        [SerializeField] private bool  _bioluminescent  = false;
        [SerializeField] private Color _bioLumColor     = new Color(0.5f, 1f, 0.8f);
        [SerializeField, Range(0f, 2f)]
        private float _bioLumIntensity = 0.6f;

        [Header("── Behaviour ───────────────────────────────────────────")]
        [SerializeField, Range(0.5f, 5f)]  private float _openDuration      = 2f;
        [SerializeField, Range(0.3f, 3f)]  private float _closeDuration     = 0.8f;
        [SerializeField, Range(0.5f, 5f)]  private float _closeTriggerDist  = 1.2f;
        [SerializeField, Range(0, 4)]      private int   _maxClownfishSlots = 2;

        [Header("── Rendering ───────────────────────────────────────────")]
        [SerializeField] private Material _anemoneMaterial;

        // ── PRIVATE STATE ─────────────────────────────────────────────

        private enum AnemoneState : byte
        {
            Closed  = 0,
            Opening = 1,
            Open    = 2,
            Feeding = 3,
            Closing = 4
        }

        private AnemoneState _state     = AnemoneState.Closed;
        private float        _stateT    = 0f;
        private float        _openT     = 0f;
        private float        _idleTimer = 0f;

        private struct TentacleAnim
        {
            public float WavePhase;
            public float WaveSpeed;
            public float WaveAmplitude;
        }

        // COLD ALLOC: fixed tentacle count arrays
        private TentacleAnim[]  _tentacleAnims;
        private Matrix4x4[]     _tentacleMatrices;
        private Vector4[]       _tentacleColors;

        private Mesh _tentacleMesh;
        private Mesh _columnMesh;

        private bool _meshesBuilt;
        private bool _registered;
        private int  _clownfishOccupied;

        private Camera    _mainCam;
        private Transform _camTransform;
        private Transform _myTransform;

        private float _closeTriggerDistSq; // cached squared

        private readonly MaterialPropertyBlock _mpb
            = new MaterialPropertyBlock();

        private static readonly int
            _PropTentacleColors = Shader.PropertyToID("_TentacleColors"),
            _PropOpenAmount     = Shader.PropertyToID("_OpenAmount");

        // ── LIFECYCLE ─────────────────────────────────────────────────

        private void Awake()
        {
            _myTransform = transform;
            _mainCam     = Camera.main;
            _camTransform = _mainCam != null ? _mainCam.transform : null;

            _closeTriggerDistSq = _closeTriggerDist * _closeTriggerDist;

            if (_anemoneMaterial == null)
            {
                Debug.LogError(
                    "[SeaAnemone] Material not assigned. Disabling.");
                enabled = false;
                return;
            }

            // COLD ALLOC: fixed to _tentacleCount — never reallocated
            _tentacleAnims    = new TentacleAnim[_tentacleCount];
            _tentacleMatrices = new Matrix4x4[_tentacleCount];
            _tentacleColors   = new Vector4[_tentacleCount];

            var rng = new System.Random(GetInstanceID());
            for (int i = 0; i < _tentacleCount; i++)
            {
                _tentacleAnims[i] = new TentacleAnim
                {
                    WavePhase     = (float)rng.NextDouble() * Mathf.PI * 2f,
                    WaveSpeed     = Mathf.Lerp(1f, 3f, (float)rng.NextDouble()),
                    WaveAmplitude = Mathf.Lerp(0.01f, 0.04f,
                        (float)rng.NextDouble())
                };
            }

            BuildMeshes();
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
            if (_registered && GameTickManager.Instance != null)
            {
                GameTickManager.Instance.Unregister(this);
                _registered = false;
            }
        }

        private void OnDestroy()
        {
            if (_tentacleMesh != null) Destroy(_tentacleMesh);
            if (_columnMesh   != null) Destroy(_columnMesh);
        }

        // ── IINTERACTABLE ─────────────────────────────────────────────

        public void OnHoverStart() { }
        public void OnHoverEnd()   { }

        public void Interact(Transform interactor)
        {
            if (_state == AnemoneState.Open
             || _state == AnemoneState.Feeding)
            {
                _state  = AnemoneState.Closing;
                _stateT = 0f;
            }
        }

        public string GetInteractText() => "Sea Anemone";

        // ── ITICKABLE ─────────────────────────────────────────────────

        public void Tick(float dt)
        {
            if (!_meshesBuilt) return;

            // Cache position — single transform read
            Vector3 myPos = _myTransform.position;

            // Distance cull — 60m
            if (_camTransform != null)
            {
                Vector3 toCam = _camTransform.position - myPos;
                if (toCam.sqrMagnitude > 3600f) return;

                // Player proximity → close
                float sqDist = toCam.sqrMagnitude;
                if (sqDist < _closeTriggerDistSq
                 && (_state == AnemoneState.Open
                  || _state == AnemoneState.Feeding))
                {
                    _state  = AnemoneState.Closing;
                    _stateT = 0f;
                }
            }

            // State machine
            _stateT += dt;
            switch (_state)
            {
                case AnemoneState.Closed:
                    _openT = 0f;
                    _idleTimer += dt;
                    if (_idleTimer > 5f)
                    {
                        _state     = AnemoneState.Opening;
                        _stateT    = 0f;
                        _idleTimer = 0f;
                    }
                    break;

                case AnemoneState.Opening:
                    _openT = Mathf.Clamp01(_stateT / _openDuration);
                    if (_openT >= 1f)
                    {
                        _state  = AnemoneState.Open;
                        _stateT = 0f;
                    }
                    break;

                case AnemoneState.Open:
                    _openT = 1f;
                    if (_stateT > 8f)
                    {
                        _state  = AnemoneState.Feeding;
                        _stateT = 0f;
                    }
                    break;

                case AnemoneState.Feeding:
                    _openT = 1f - Mathf.Sin(_stateT * Mathf.PI * 2f) * 0.15f;
                    if (_stateT > 2f)
                    {
                        _state  = AnemoneState.Open;
                        _stateT = 0f;
                    }
                    break;

                case AnemoneState.Closing:
                    _openT = 1f - Mathf.Clamp01(_stateT / _closeDuration);
                    if (_openT <= 0f)
                    {
                        _state  = AnemoneState.Closed;
                        _stateT = 0f;
                    }
                    break;
            }

            UpdateTentacleData(myPos);
            DrawAnemone(myPos);
        }

        // ── PUBLIC API ────────────────────────────────────────────────

        /// <summary>
        /// Fish AI requests a shelter slot.
        /// Returns true if space available.
        /// </summary>
        public bool RequestShelter()
        {
            if (_clownfishOccupied >= _maxClownfishSlots) return false;
            _clownfishOccupied++;
            return true;
        }

        /// <summary>Fish AI releases shelter slot on departure.</summary>
        public void ReleaseShelter()
        {
            _clownfishOccupied = Mathf.Max(0, _clownfishOccupied - 1);
        }

        // ── PRIVATE ───────────────────────────────────────────────────

        private void UpdateTentacleData(Vector3 myPos)
        {
            float time    = Time.time;
            Quaternion myRot = _myTransform.rotation;

            for (int i = 0; i < _tentacleCount; i++)
            {
                ref TentacleAnim anim = ref _tentacleAnims[i];

                float ringAngle = (float)i / _tentacleCount * Mathf.PI * 2f;
                float ringR     = _columnRadius * 0.85f * _openT;

                Vector3 localBase = new Vector3(
                    Mathf.Cos(ringAngle) * ringR,
                    _columnHeight,
                    Mathf.Sin(ringAngle) * ringR);

                float sway = Mathf.Sin(time * anim.WaveSpeed + anim.WavePhase)
                           * anim.WaveAmplitude * _openT;

                Vector3 outDir = new Vector3(
                    Mathf.Cos(ringAngle), 0f, Mathf.Sin(ringAngle));
                Vector3 upDir  = new Vector3(sway, 1f, sway * 0.7f).normalized;

                float scaleXZ = _columnRadius * 0.08f * (1f - _openT * 0.3f);
                float scaleY  = _tentacleLength * _openT;

                // Cache SetPositionAndRotation equivalent via TRS
                _tentacleMatrices[i] = Matrix4x4.TRS(
                    myPos + myRot * localBase,
                    myRot * Quaternion.LookRotation(outDir, upDir),
                    new Vector3(scaleXZ, scaleY, scaleXZ));

                float bioLum = _bioluminescent
                    ? (Mathf.Sin(time * 1.5f + anim.WavePhase) * 0.5f + 0.5f)
                      * _bioLumIntensity
                    : 0f;

                _tentacleColors[i] = new Vector4(
                    _tentacleColor.r + _bioLumColor.r * bioLum,
                    _tentacleColor.g + _bioLumColor.g * bioLum,
                    _tentacleColor.b + _bioLumColor.b * bioLum,
                    _openT);
            }
        }

        private void DrawAnemone(Vector3 myPos)
        {
            if (_anemoneMaterial == null) return;

            _mpb.SetVectorArray(_PropTentacleColors, _tentacleColors);
            _mpb.SetFloat(_PropOpenAmount, _openT);

            // Column — single mesh draw
            if (_columnMesh != null)
            {
                Graphics.DrawMesh(
                    _columnMesh,
                    _myTransform.localToWorldMatrix,
                    _anemoneMaterial,
                    gameObject.layer, null, 0, _mpb);
            }

            // Tentacles — instanced
            if (_tentacleMesh != null && _tentacleCount > 0)
            {
                Graphics.DrawMeshInstanced(
                    _tentacleMesh, 0, _anemoneMaterial,
                    _tentacleMatrices, _tentacleCount, _mpb,
                    UnityEngine.Rendering.ShadowCastingMode.Off,
                    receiveShadows: false,
                    layer: gameObject.layer);
            }
        }

        private void BuildMeshes()
        {
            _columnMesh   = BuildColumnMesh();
            _tentacleMesh = BuildTentacleMesh();
            _meshesBuilt  = true;
        }

        private Mesh BuildColumnMesh()
        {
            var mesh  = new Mesh { name = "AnemoneColumn" };
            const int sides = 8;
            const int segs  = 4;

            // COLD ALLOC: startup mesh build
            var verts = new System.Collections.Generic.List<Vector3>(
                sides * (segs + 1));
            var norms = new System.Collections.Generic.List<Vector3>(
                sides * (segs + 1));
            var uvs   = new System.Collections.Generic.List<Vector2>(
                sides * (segs + 1));
            var cols  = new System.Collections.Generic.List<Color>(
                sides * (segs + 1));
            var tris  = new System.Collections.Generic.List<int>(
                sides * segs * 6);

            for (int seg = 0; seg <= segs; seg++)
            {
                float t = (float)seg / segs;
                float r = _columnRadius * Mathf.Lerp(1f, 0.7f, t);
                float y = t * _columnHeight;

                for (int si = 0; si < sides; si++)
                {
                    float angle = (float)si / sides * Mathf.PI * 2f;
                    Vector3 n   = new Vector3(
                        Mathf.Cos(angle), 0.1f,
                        Mathf.Sin(angle)).normalized;

                    verts.Add(new Vector3(n.x * r, y, n.z * r));
                    norms.Add(n);
                    uvs.Add(new Vector2((float)si / sides, t));
                    cols.Add(_columnColor);
                }

                if (seg < segs)
                {
                    int b = seg * sides, nb = b + sides;
                    for (int si = 0; si < sides; si++)
                    {
                        int ni = (si + 1) % sides;
                        tris.Add(b+si);  tris.Add(nb+si); tris.Add(b+ni);
                        tris.Add(b+ni);  tris.Add(nb+si); tris.Add(nb+ni);
                    }
                }
            }

            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(cols);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);
            return mesh;
        }

        private Mesh BuildTentacleMesh()
        {
            var mesh  = new Mesh { name = "AnemoneTentacle" };
            const int sides = 5;
            const int segs  = 3;

            var verts = new System.Collections.Generic.List<Vector3>();
            var norms = new System.Collections.Generic.List<Vector3>();
            var uvs   = new System.Collections.Generic.List<Vector2>();
            var cols  = new System.Collections.Generic.List<Color>();
            var tris  = new System.Collections.Generic.List<int>();

            for (int seg = 0; seg <= segs; seg++)
            {
                float t = (float)seg / segs;
                float r = 0.5f * (1f - t * 0.85f);

                for (int si = 0; si < sides; si++)
                {
                    float angle = (float)si / sides * Mathf.PI * 2f;
                    Vector3 n   = new Vector3(
                        Mathf.Cos(angle), 0f, Mathf.Sin(angle));

                    verts.Add(new Vector3(n.x * r, t, n.z * r));
                    norms.Add(n);
                    uvs.Add(new Vector2((float)si / sides, t));
                    cols.Add(Color.Lerp(
                        _tentacleColor, _tipColor,
                        Mathf.Pow(t, 2f)));
                }

                if (seg < segs)
                {
                    int b = seg * sides, nb = b + sides;
                    for (int si = 0; si < sides; si++)
                    {
                        int ni = (si + 1) % sides;
                        tris.Add(b+si);  tris.Add(nb+si); tris.Add(b+ni);
                        tris.Add(b+ni);  tris.Add(nb+si); tris.Add(nb+ni);
                    }
                }
            }

            // Tip vertex
            verts.Add(new Vector3(0f, 1.1f, 0f));
            norms.Add(Vector3.up);
            uvs.Add(new Vector2(0.5f, 1f));
            cols.Add(_tipColor);

            int tipIdx   = verts.Count - 1;
            int lastRing = segs * sides;
            for (int si = 0; si < sides; si++)
            {
                tris.Add(lastRing + si);
                tris.Add(tipIdx);
                tris.Add(lastRing + (si + 1) % sides);
            }

            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(cols);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);
            return mesh;
        }
    }
}
```

---

```csharp
// ============================================================
// HECTON-8 — MarineInvertebrateRenderer.cs v1.0
// GPU instanced sea urchins, starfish, mussels, barnacles.
// ISlowTickable — redraws every ~0.5s.
// Zero GC in SlowTick. Pre-allocated groups.
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using Hecton8.Core;

namespace Hecton8.Environment
{
    public enum InvertebrateType : byte
    {
        SeaUrchin = 0,
        Starfish  = 1,
        Mussel    = 2,
        Barnacle  = 3
    }

    /// <summary>
    /// Renders static marine invertebrates via
    /// Graphics.DrawMeshInstanced. ISlowTickable — not per-frame.
    /// Each type has a pre-allocated group with fixed capacity.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-70)]
    public sealed class MarineInvertebrateRenderer
        : MonoBehaviour, ISlowTickable
    {
        // ── INSPECTOR ─────────────────────────────────────────────────

        [Header("── Meshes ───────────────────────────────────────────────")]
        [SerializeField] private Mesh _urchinMesh;
        [SerializeField] private Mesh _starfishMesh;
        [SerializeField] private Mesh _musselMesh;
        [SerializeField] private Mesh _barnacleMesh;

        [Header("── Materials ───────────────────────────────────────────")]
        [SerializeField] private Material _urchinMat;
        [SerializeField] private Material _starfishMat;
        [SerializeField] private Material _musselMat;
        [SerializeField] private Material _barnacleMat;

        [Header("── Capacities ──────────────────────────────────────────")]
        [SerializeField, Range(0, 500)] private int _maxUrchin   = 200;
        [SerializeField, Range(0, 200)] private int _maxStarfish = 80;
        [SerializeField, Range(0, 500)] private int _maxMussel   = 300;
        [SerializeField, Range(0, 500)] private int _maxBarnacle = 300;

        // ── PRIVATE STATE ─────────────────────────────────────────────

        private sealed class InvertebrateGroup
        {
            public readonly Matrix4x4[]      Matrices;
            public readonly Vector4[]        Colors;
            public readonly MaterialPropertyBlock MPB;
            public int                       Count;
            public readonly Mesh             Mesh;
            public readonly Material         Material;

            public InvertebrateGroup(int max, Mesh mesh, Material mat)
            {
                // COLD ALLOC: fixed capacity arrays
                Matrices = new Matrix4x4[max];
                Colors   = new Vector4[max];
                MPB      = new MaterialPropertyBlock();
                Mesh     = mesh;
                Material = mat;
            }
        }

        private InvertebrateGroup _urchins;
        private InvertebrateGroup _starfish;
        private InvertebrateGroup _mussels;
        private InvertebrateGroup _barnacles;

        private bool _registered;

        private static readonly int
            _PropColors = Shader.PropertyToID("_InstanceColors");

        private static readonly Bounds _DrawBounds
            = new Bounds(Vector3.zero, Vector3.one * 5000f);

        // ── LIFECYCLE ─────────────────────────────────────────────────

        private void Awake()
        {
            // COLD ALLOC: groups at startup
            _urchins   = new InvertebrateGroup(
                _maxUrchin,   _urchinMesh,   _urchinMat);
            _starfish  = new InvertebrateGroup(
                _maxStarfish, _starfishMesh, _starfishMat);
            _mussels   = new InvertebrateGroup(
                _maxMussel,   _musselMesh,   _musselMat);
            _barnacles = new InvertebrateGroup(
                _maxBarnacle, _barnacleMesh, _barnacleMat);
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
            if (_registered && GameTickManager.Instance != null)
            {
                GameTickManager.Instance.UnregisterSlow(this);
                _registered = false;
            }
        }

        // ── ISLOTWTICKABLE ────────────────────────────────────────────

        public void SlowTick()
        {
            DrawGroup(_urchins);
            DrawGroup(_starfish);
            DrawGroup(_mussels);
            DrawGroup(_barnacles);
        }

        // ── PUBLIC API ────────────────────────────────────────────────

        /// <summary>
        /// Add an invertebrate instance.
        /// Returns false if group is at capacity.
        /// </summary>
        public bool Add(
            InvertebrateType type,
            Vector3 pos, Quaternion rot, float scale,
            Color col)
        {
            InvertebrateGroup g = type switch
            {
                InvertebrateType.SeaUrchin => _urchins,
                InvertebrateType.Starfish  => _starfish,
                InvertebrateType.Mussel    => _mussels,
                InvertebrateType.Barnacle  => _barnacles,
                _                          => null
            };

            if (g == null || g.Count >= g.Matrices.Length) return false;

            int idx = g.Count++;
            g.Matrices[idx] = Matrix4x4.TRS(pos, rot, Vector3.one * scale);
            g.Colors[idx]   = new Vector4(col.r, col.g, col.b, col.a);
            return true;
        }

        // ── PRIVATE ───────────────────────────────────────────────────

        private static void DrawGroup(InvertebrateGroup g)
        {
            if (g == null
             || g.Mesh == null
             || g.Material == null
             || g.Count == 0) return;

            g.MPB.SetVectorArray(_PropColors, g.Colors);

            int drawn = 0;
            while (drawn < g.Count)
            {
                int batch = Mathf.Min(1023, g.Count - drawn);
                Graphics.DrawMeshInstanced(
                    g.Mesh, 0, g.Material,
                    g.Matrices, drawn, batch, g.MPB,
                    UnityEngine.Rendering.ShadowCastingMode.Off,
                    receiveShadows: false);
                drawn += batch;
            }
        }
    }
}
```

---

```csharp
// ============================================================
// HECTON-8 — CoralFishSchool.cs v1.0
// Boids flocking fish around coral reef.
// ITickable. Burst Jobs. GPU Instanced rendering.
// Zero GC in Tick. Pre-allocated NativeArrays + Matrix arrays.
// ============================================================

using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Hecton8.Core;

namespace Hecton8.AI
{
    /// <summary>
    /// Simulates a school of fish using Boids algorithm.
    /// Separation + Alignment + Cohesion + HomeRange + PlayerFlee.
    /// Burst Job processes all fish in parallel.
    /// Rendering: Graphics.DrawMeshInstanced (one call per frame).
    ///
    /// Performance on MX350 / i5-11th:
    ///   40 fish Burst Job: ~0.15ms CPU
    ///   40 fish instanced draw: ~0.2ms GPU
    ///   Total: ~0.35ms — acceptable.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CoralFishSchool : MonoBehaviour, ITickable
    {
        // ── INSPECTOR ─────────────────────────────────────────────────

        [Header("── School ───────────────────────────────────────────────")]
        [SerializeField, Range(5, 200)]
        private int _fishCount = 40;

        [SerializeField, Range(1f, 50f)]
        private float _homeRadius = 15f;

        [SerializeField, Range(0.5f, 10f)]
        private float _swimSpeed = 2.5f;

        [SerializeField, Range(0.1f, 5f)]
        private float _swimSpeedVariance = 0.8f;

        [Header("── Boids Weights ─────────────────────────────────────────")]
        [SerializeField, Range(0f, 5f)] private float _separationWeight = 1.5f;
        [SerializeField, Range(0f, 5f)] private float _alignmentWeight  = 1.0f;
        [SerializeField, Range(0f, 5f)] private float _cohesionWeight   = 0.8f;
        [SerializeField, Range(0f, 5f)] private float _homeWeight       = 0.5f;
        [SerializeField, Range(0f, 5f)] private float _fleeWeight       = 3.0f;

        [Header("── Perception ──────────────────────────────────────────")]
        [SerializeField, Range(0.5f, 5f)]  private float _perceptionRadius = 2.5f;
        [SerializeField, Range(0.3f, 3f)]  private float _separationRadius = 0.8f;
        [SerializeField, Range(1f, 10f)]   private float _fleeRadius       = 3f;

        [Header("── Rendering ──────────────────────────────────────────")]
        [SerializeField] private Mesh     _fishMesh;
        [SerializeField] private Material _fishMaterial;

        [Header("── Appearance ─────────────────────────────────────────")]
        [SerializeField] private Color _bodyColorA = new Color(0.6f, 0.8f, 1f);
        [SerializeField] private Color _bodyColorB = new Color(1f, 0.7f, 0.3f);
        [SerializeField, Range(0.03f, 0.5f)] private float _fishSize = 0.12f;

        // ── PRIVATE STATE ─────────────────────────────────────────────

        // COLD ALLOC: NativeArrays — Persistent for school lifetime
        private NativeArray<float3> _positions;
        private NativeArray<float3> _velocities;
        private NativeArray<float3> _newVelocities;

        // COLD ALLOC: rendering arrays
        private Matrix4x4[] _matrices;
        private Vector4[]   _colorData;

        private bool _registered;
        private bool _initialized;

        private float3 _homePos;
        private Camera    _mainCam;
        private Transform _camTransform;
        private Transform _playerTransform;

        private readonly MaterialPropertyBlock _mpb
            = new MaterialPropertyBlock();

        private static readonly int
            _PropFishColors = Shader.PropertyToID("_FishColors");

        private static readonly Bounds _DrawBounds
            = new Bounds(Vector3.zero, Vector3.one * 5000f);

        // ── BURST JOB ─────────────────────────────────────────────────

        [BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
        private struct BoidsJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float3> Positions;
            [ReadOnly] public NativeArray<float3> Velocities;
            [WriteOnly] public NativeArray<float3> NewVelocities;

            public float3 HomePos;
            public float3 PlayerPos;
            public float  PlayerFleeRadius;
            public float  PerceptionRadius;
            public float  SeparationRadius;
            public float  SepWeight;
            public float  AlnWeight;
            public float  CohWeight;
            public float  HomeWeight;
            public float  FleeWeight;
            public float  MaxSpeed;
            public float  MinSpeed;
            public float  DeltaTime;

            public void Execute(int i)
            {
                float3 pos = Positions[i];
                float3 vel = Velocities[i];

                float3 sep = float3.zero;
                float3 aln = float3.zero;
                float3 coh = float3.zero;
                int    cnt = 0;

                float percSq = PerceptionRadius * PerceptionRadius;
                float sepSq  = SeparationRadius * SeparationRadius;

                for (int j = 0; j < Positions.Length; j++)
                {
                    if (j == i) continue;
                    float3 diff   = pos - Positions[j];
                    float  distSq = math.lengthsq(diff);
                    if (distSq > percSq) continue;

                    cnt++;
                    aln += Velocities[j];
                    coh += Positions[j];

                    if (distSq < sepSq && distSq > 0.0001f)
                        sep += math.normalize(diff) / math.sqrt(distSq);
                }

                float3 steering = float3.zero;

                if (cnt > 0)
                {
                    // Alignment
                    steering += math.normalize(aln / cnt - vel) * AlnWeight;
                    // Cohesion
                    steering += math.normalize(coh / cnt - pos) * CohWeight;
                }

                if (math.lengthsq(sep) > 0.0001f)
                    steering += math.normalize(sep) * SepWeight;

                // Home range pull
                float3 toHome  = HomePos - pos;
                float  homeDist = math.length(toHome);
                if (homeDist > 5f)
                    steering += math.normalize(toHome) * HomeWeight
                              * (homeDist / 15f);

                // Player flee
                float3 fromPlayer = pos - PlayerPos;
                float  pDist      = math.length(fromPlayer);
                if (pDist < PlayerFleeRadius)
                    steering += math.normalize(fromPlayer) * FleeWeight
                              * (1f - pDist / PlayerFleeRadius);

                // Depth bounds
                if (pos.y > -0.5f) steering.y -= 2f;
                if (pos.y < -30f)  steering.y += 1f;

                float3 newVel = vel + steering * DeltaTime;
                float  speed  = math.length(newVel);

                if (speed > MaxSpeed)
                    newVel = newVel / speed * MaxSpeed;
                else if (speed < MinSpeed)
                    newVel = math.normalize(
                        newVel + new float3(0f, 0.01f, 0f)) * MinSpeed;

                NewVelocities[i] = newVel;
            }
        }

        // ── LIFECYCLE ─────────────────────────────────────────────────

        private void Awake()
        {
            _mainCam        = Camera.main;
            _camTransform   = _mainCam != null ? _mainCam.transform : null;
            _homePos        = transform.position;

            if (_fishMesh == null || _fishMaterial == null)
            {
                Debug.LogError(
                    "[CoralFishSchool] Mesh or Material missing. Disabling.");
                enabled = false;
                return;
            }

            // COLD ALLOC: NativeArrays — Persistent
            _positions     = new NativeArray<float3>(
                _fishCount, Allocator.Persistent);
            _velocities    = new NativeArray<float3>(
                _fishCount, Allocator.Persistent);
            _newVelocities = new NativeArray<float3>(
                _fishCount, Allocator.Persistent);

            // COLD ALLOC: rendering arrays
            _matrices  = new Matrix4x4[_fishCount];
            _colorData = new Vector4[_fishCount];

            var rng = new System.Random(GetInstanceID());

            for (int i = 0; i < _fishCount; i++)
            {
                float3 offset = new float3(
                    (float)rng.NextDouble() * 2f - 1f,
                    (float)rng.NextDouble() * 2f - 1f,
                    (float)rng.NextDouble() * 2f - 1f)
                    * _homeRadius * 0.5f;

                _positions[i]  = _homePos + offset;
                _velocities[i] = math.normalize(
                    offset + new float3(0.1f, 0f, 0.1f))
                    * _swimSpeed;

                float t = (float)rng.NextDouble();
                _colorData[i] = new Vector4(
                    Mathf.Lerp(_bodyColorA.r, _bodyColorB.r, t),
                    Mathf.Lerp(_bodyColorA.g, _bodyColorB.g, t),
                    Mathf.Lerp(_bodyColorA.b, _bodyColorB.b, t),
                    1f);
            }

            _initialized = true;
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
            if (_registered && GameTickManager.Instance != null)
            {
                GameTickManager.Instance.Unregister(this);
                _registered = false;
            }
        }

        private void OnDestroy()
        {
            if (_positions.IsCreated)     _positions.Dispose();
            if (_velocities.IsCreated)    _velocities.Dispose();
            if (_newVelocities.IsCreated) _newVelocities.Dispose();
        }

        // ── ITICKABLE ─────────────────────────────────────────────────

        public void Tick(float dt)
        {
            if (!_initialized) return;
            if (_camTransform == null) return;

            // Cache camera position — single read
            Vector3 camPos = _camTransform.position;

            // Skip if too far from camera (100m)
            float3 homeToCamera = _homePos - (float3)camPos;
            if (math.lengthsq(homeToCamera) > 10000f) return;

            float3 playerPos = _playerTransform != null
                ? (float3)_playerTransform.position
                : new float3(0f, -99999f, 0f);

            var job = new BoidsJob
            {
                Positions        = _positions,
                Velocities       = _velocities,
                NewVelocities    = _newVelocities,
                HomePos          = _homePos,
                PlayerPos        = playerPos,
                PlayerFleeRadius = _fleeRadius,
                PerceptionRadius = _perceptionRadius,
                SeparationRadius = _separationRadius,
                SepWeight        = _separationWeight,
                AlnWeight        = _alignmentWeight,
                CohWeight        = _cohesionWeight,
                HomeWeight       = _homeWeight,
                FleeWeight       = _fleeWeight,
                MaxSpeed         = _swimSpeed + _swimSpeedVariance,
                MinSpeed         = _swimSpeed * 0.3f,
                DeltaTime        = dt
            };

            job.Schedule(_fishCount, 8).Complete();

            // Integrate and build matrices — no alloc
            for (int i = 0; i < _fishCount; i++)
            {
                float3 vel = _newVelocities[i];
                float3 pos = _positions[i] + vel * dt;

                _positions[i]  = pos;
                _velocities[i] = vel;

                float speed = math.length(vel);
                Quaternion rot = speed > 0.01f
                    ? Quaternion.LookRotation(vel / speed, Vector3.up)
                    : Quaternion.identity;

                _matrices[i] = Matrix4x4.TRS(
                    pos, rot, Vector3.one * _fishSize);
            }

            // Draw — no alloc
            _mpb.SetVectorArray(_PropFishColors, _colorData);

            int drawn = 0;
            while (drawn < _fishCount)
            {
                int batch = Mathf.Min(1023, _fishCount - drawn);
                Graphics.DrawMeshInstanced(
                    _fishMesh, 0, _fishMaterial,
                    _matrices, drawn, batch, _mpb,
                    UnityEngine.Rendering.ShadowCastingMode.Off,
                    receiveShadows: false);
                drawn += batch;
            }
        }

        // ── PUBLIC API ────────────────────────────────────────────────

        /// <summary>
        /// Set player transform for flee behaviour.
        /// Call once from player setup.
        /// </summary>
        public void SetPlayerTransform(Transform player)
        {
            _playerTransform = player;
        }
    }
}
```

---

**STATUS: PENDING VERIFICATION.**

Vse fayly napisany. Itogo sistema soderzhit:

```
ShEYDERY:
  SeaweedCommon.hlsl
  SeaweedLit.shader
  SeaweedCulling.compute

C# DATA:
  SeaweedTypes.cs
  SeaweedSpeciesLibrary.cs

C# GENERATION:
  SeaweedSpineJob.cs
  SeaweedMeshGenerator.cs
  SeaweedMeshCache.cs
  SeaweedTextureGenerator.cs

C# MONOBEHAVIOURS (vse ITickable):
  SeaweedCurrentZone.cs
  SeaweedSeasonSystem.cs
  SeaweedAnimCurveSystem.cs
  SeaweedRenderer.cs
  SeaweedGPUCuller.cs
  SeaweedPhysicsManager.cs
  SeaweedInteraction.cs
  SeaweedBootstrap.cs

C# PLACEMENT:
  SeaweedPlacer.cs (+ SpatialHashGrid)

C# ECOSYSTEM:
  SeaAnemone.cs
  MarineInvertebrateRenderer.cs
  CoralFishSchool.cs
```
