// ============================================================================
// HECTON-8 — BoidFishInstanced.shader
// URP Unlit GPU-Instanced shader dlya staynyh ryb.
//
// ARHITEKTURA:
//   • StructuredBuffer<BoidData> — pozitsii/skorosti iz Compute Shader.
//   • SV_InstanceID — indeksatsiya v bufere (odin draw call → 5000 ryb).
//   • Vertex displacement — protsedurnaya animatsiya hvosta (sin wave).
//   • LookRotation — vraschenie modeli po napravleniyu skorosti.
//   • Zero overhead: no shadows, no fog, no lightmap, no GI.
//
// MODEL RYBY (SOGLAShENIE):
//   • Ryba smotrit po +Z (forward).
//   • Hvost po -Z, golova po +Z.
//   • X = horizontal axis (vlevo-vpravo).
//   • Y = vertical axis (vverh-vniz).
//   • Pivot (origin) — v rayone tsentra tela ili golovy.
//   • Hvostovye vershiny imeyut OTRITsATELNYY Z (local).
//
// TAIL WAG (protsedurnaya animatsiya):
//   displacement = cheapWave(time × freq + instanceID × phaseOffset)
//                  x amplitude x cheap tail polynomial
//   
//   localZ < 0 = hvost → maksimalnoe smeschenie
//   localZ ≈ 0 = tsentr → minimalnoe
//   localZ > 0 = golova → nulevoe (clamped)
//
//   Displacement primenyaetsya k LOCAL X (gorizontalnoe vilyanie).
//   Eto sozdaet estestvennoe S-obraznoe dvizhenie tela ryby.
//
// PERFORMANCE na MX350:
//   2000 fish × 200 tris = 400K tris, 1 draw call.
//   Vertex shader: ~10 ALU ops per vertex (sin + matrix + scale).
//   Fragment shader: ~3 ALU ops (texture sample + tint).
//   Estimated: ~0.2ms total.
// ============================================================================

Shader "Hecton8/BoidFishInstanced"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Texture", 2D) = "white" {}
        [MainColor]   _BaseColor ("Base Color", Color) = (0.4, 0.6, 0.8, 1.0)
        
        [Header(Fish Scale)]
        _FishScale ("Fish Scale", Float) = 0.3
        _H8FoveatedVatTimeScale ("Foveated VAT Time Scale", Float) = 1
        
        [Header(Tail Animation)]
        _TailFrequency ("Tail Wag Frequency", Float) = 6.0
        _TailAmplitude ("Tail Wag Amplitude", Float) = 0.15
        _TailPower ("Tail Falloff Power (higher = sharper)", Float) = 2.0
        _TailPhaseVariance ("Phase Variance (per instance)", Float) = 3.7
        _TailSpeedInfluence ("Speed Influence on Frequency", Float) = 0.5
        _TailWorldYPhase ("Tail World-Y Phase", Float) = 1.15

        [Header(VAT Animation)]
        _VatEnabled ("VAT Enabled", Float) = 0
        _VatPositionTex ("VAT Position Texture", 2D) = "black" {}
        _VatNormalTex ("VAT Normal Texture", 2D) = "bump" {}
        _VatFrameCount ("VAT Frame Count", Float) = 1
        _VatVertexCount ("VAT Vertex Count", Float) = 1
        _VatPlaybackSpeed ("VAT Playback Speed", Float) = 1
        _VatInstancePhaseScale ("VAT Instance Phase Scale", Float) = 0.25
        _VatPositionScale ("VAT Position Scale", Float) = 1
        _VatNormalBlend ("VAT Normal Blend", Range(0, 1)) = 1
        _VatSpeedReference ("VAT Speed Reference", Float) = 6
        _Phase ("VAT Phase", Range(0, 1)) = 0
        _FinStretchStrength ("Fin Stretch Strength", Range(0, 0.35)) = 0.16

        [Header(Hit Reaction)]
        _HitFlashStartTime ("Hit Flash Start Time", Float) = -1000
        _HitFlashDuration ("Hit Flash Duration", Float) = 0.1
        _HitFlashIntensity ("Hit Flash Intensity", Range(0, 1)) = 0
        _HitFlashRadius ("Hit Flash Radius", Float) = 0
        _HitFlashBloat ("Hit Flash Bloat", Range(0, 0.12)) = 0.035
        _HitFlashOriginWS ("Hit Flash Origin WS", Vector) = (0, 0, 0, 0)
        _HitFlashColor ("Hit Flash Color", Color) = (1, 0.08, 0.04, 1)
        
        [Header(Color Variation)]
        _ColorVariance ("Color Hue Variance", Float) = 0.05
        _BellyColor ("Belly Color", Color) = (0.8, 0.85, 0.9, 1.0)
        _BellyBlend ("Belly Blend (Y threshold)", Float) = 0.0

        [Header(Bioluminescence)]
        _BiolumColor ("Biolum Color", Color) = (0.16, 0.86, 0.88, 1.0)
        _BiolumStrength ("Biolum Strength", Range(0, 4)) = 0.42
        _BiolumPulseAmplitude ("Biolum Pulse Amplitude", Range(0, 1)) = 0.18
        _BiolumNightResponse ("Biolum Night Response", Range(0, 2)) = 1.0
        _BiolumSpotScale ("Biolum Spot Scale", Float) = 18
        _BiolumSpotThreshold ("Biolum Spot Threshold", Range(0, 1)) = 0.72
        _AggressiveGlowStrength ("Aggressive Glow Strength", Range(0, 4)) = 0.8
        _LodDitherKeep01 ("LOD Dither Keep 01", Range(0, 1)) = 1

        [Header(Parasite Drones)]
        _ParasiteBaseColor ("Parasite Base Color", Color) = (0.32, 0.35, 0.42, 1.0)
        _ParasiteGlowColor ("Parasite Glow Color", Color) = (0.22, 0.95, 1.0, 1.0)
        _ParasiteGlowStrength ("Parasite Glow Strength", Range(0, 6)) = 1.65
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
            "UniversalMaterialType" = "Unlit"
        }

        // ── Odin pass, bez teney, bez depth prepass ──
        // ShadowCaster i DepthOnly passes NAMERENNO OTSUTSTVUYuT.
        // 5000 ryb × shadow pass = ubiystvo dlya MX350.

        Pass
        {
            Name "BoidFishForward"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual
            Blend One Zero

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // ── Minimalnye fichi ──
            #pragma target 4.5  // Required for StructuredBuffer
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling

            // ══════════════════════════════════════════════════════
            //  INCLUDES
            // ══════════════════════════════════════════════════════

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // ══════════════════════════════════════════════════════
            //  BOID DATA — must match BoidSimulation.compute EXACTLY
            // ══════════════════════════════════════════════════════

            struct BoidData
            {
                float3 position;    // 12 bytes
                float3 velocity;    // 12 bytes
                float  panic;       // 4 bytes
                uint   stateFlags;  // 4 bytes
                // TOTAL: 32 bytes
            };

            // ══════════════════════════════════════════════════════
            //  BUFFERS & UNIFORMS
            // ══════════════════════════════════════════════════════

            StructuredBuffer<BoidData> _BoidsBuffer;
            StructuredBuffer<uint> _VisibleBoidIndices;

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _BaseMap_ST;
                float  _FishScale;
                float  _H8FoveatedVatTimeScale;
                
                // Tail animation
                float  _TailFrequency;
                float  _TailAmplitude;
                float  _TailPower;
                float  _TailPhaseVariance;
                float  _TailSpeedInfluence;
                float  _TailWorldYPhase;

                // VAT animation
                float  _VatEnabled;
                float  _VatFrameCount;
                float  _VatVertexCount;
                float  _VatPlaybackSpeed;
                float  _VatInstancePhaseScale;
                float  _VatPositionScale;
                float  _VatNormalBlend;
                float  _VatSpeedReference;
                float  _Phase;
                float  _FinStretchStrength;
                float  _BoidUseVisibleIndices;

                float  _HitFlashStartTime;
                float  _HitFlashDuration;
                float  _HitFlashIntensity;
                float  _HitFlashRadius;
                float  _HitFlashBloat;
                float4 _HitFlashOriginWS;
                float4 _HitFlashColor;
                
                // Color variation
                float  _ColorVariance;
                float4 _BellyColor;
                float  _BellyBlend;
                float4 _BiolumColor;
                float  _BiolumStrength;
                float  _BiolumPulseAmplitude;
                float  _BiolumNightResponse;
                float  _BiolumSpotScale;
                float  _BiolumSpotThreshold;
                float  _AggressiveGlowStrength;
                float  _LodDitherKeep01;
                float4 _ParasiteBaseColor;
                float4 _ParasiteGlowColor;
                float  _ParasiteGlowStrength;
            CBUFFER_END

            float _SargassumBiolumPhaseMultiplier;
            float _HectonNightFactor;
            float4 _HectonOceanBiolumColor;
            float _HectonOceanBiolumStrength;
            float _GlobalOceanPanic;
            float4 _GlobalOceanPanicColor;
            float _ParasiteMode;
            float _ParasiteAggression;
            float _VelocitySleepScale;
            float4 _TotalUniverseOffset;

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_VatPositionTex);
            SAMPLER(sampler_VatPositionTex);
            TEXTURE2D(_VatNormalTex);
            SAMPLER(sampler_VatNormalTex);

            #define BOID_FLAG_CONSUMED 8u
            #define BOID_FLAG_MUTATION_AGGRESSIVE 16u

            // ══════════════════════════════════════════════════════
            //  VERTEX / FRAGMENT STRUCTURES
            // ══════════════════════════════════════════════════════

            struct Attributes
            {
                #if defined(UNITY_INSTANCING_ENABLED)
                    UNITY_VERTEX_INPUT_INSTANCE_ID
                #else
                uint instanceID : SV_InstanceID;
                #endif
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                UNITY_VERTEX_OUTPUT_STEREO
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float  colorBlend : TEXCOORD2;   // belly/back blend factor
                float  instanceRand : TEXCOORD3; // per-instance random [0..1]
                float  aggressiveMask : TEXCOORD4;
                float  hitFlash : TEXCOORD5;
            };

            // ══════════════════════════════════════════════════════
            //  HELPER: BUILD ROTATION MATRIX FROM VELOCITY
            // ══════════════════════════════════════════════════════

            /// <summary>
            /// Constructs a 3×3 rotation matrix that aligns +Z (forward) 
            /// with the given direction vector.
            /// 
            /// Equivalent to Quaternion.LookRotation(forward, up) in C#.
            /// 
            /// Handles edge case: velocity nearly parallel to world up.
            /// 
            /// Cost: L1 reciprocal basis build, branchless zero-velocity fallback.
            /// </summary>
            float3 FastNormalizeL1(float3 value, float3 fallback)
            {
                float len = abs(value.x) + abs(value.y) + abs(value.z);
                return lerp(fallback, value * rcp(max(len, 0.00001)), step(0.00001, len));
            }

            float3x3 BuildLookRotation(float3 forward)
            {
                forward = FastNormalizeL1(forward, float3(0, 0, 1));

                // Choose up vector without a dynamic vertex branch.
                float3 up = lerp(float3(0, 1, 0), float3(1, 0, 0), step(0.999, abs(forward.y)));
                
                // Build orthonormal basis
                float3 right = cross(up, forward);
                right = FastNormalizeL1(right, float3(1, 0, 0));
                up = cross(forward, right);
                
                // Column-major matrix:
                //   Column 0 = right   (local X)
                //   Column 1 = up      (local Y)
                //   Column 2 = forward (local Z)
                return float3x3(
                    right.x,   up.x,   forward.x,
                    right.y,   up.y,   forward.y,
                    right.z,   up.z,   forward.z
                );
            }

            // ══════════════════════════════════════════════════════
            //  HELPER: PER-INSTANCE PSEUDO-RANDOM
            // ══════════════════════════════════════════════════════

            /// <summary>
            /// Integer hash for per-instance variation. Returns [0..1].
            /// </summary>
            uint HashUInt(uint value)
            {
                value ^= value >> 16;
                value *= 0x7feb352du;
                value ^= value >> 15;
                value *= 0x846ca68bu;
                value ^= value >> 16;
                return value;
            }

            float HashToUnit01(uint hash)
            {
                return (float)(hash & 0x00ffffffu) * (1.0 / 16777216.0);
            }

            float RemapSquaredVelocityVat(float speedSq, float referenceSpeed)
            {
                float referenceSq = max(referenceSpeed * referenceSpeed, 0.001);
                float x = saturate(speedSq * rcp(referenceSq));
                return x * (2.0 - x);
            }

            float HashTile2(float2 value)
            {
                uint2 cell = (uint2)(value * 4096.0);
                return HashToUnit01(HashUInt(cell.x ^ (cell.y * 0x9e3779b9u)));
            }

            half3 ApplyInstanceHueShift(half3 color, half hueShift)
            {
                half amount = saturate(abs(hueShift));
                half3 positiveShift = color.gbr;
                half3 negativeShift = color.brg;
                half3 shifted = lerp(negativeShift, positiveShift, step(0.0h, hueShift));
                return saturate(lerp(color, shifted, amount));
            }

            half FastTrianglePulse(half phase)
            {
                half wave = frac(phase * 0.15915494h);
                return 1.0h - abs(wave * 2.0h - 1.0h);
            }

            float FastSignedTriangleWave(float phase)
            {
                return 1.0 - abs(frac(phase * 0.15915494 + 0.25) * 4.0 - 2.0);
            }

            float ResolveBiolumSpotNoise(float2 uv)
            {
                return HashTile2(uv);
            }

            float ResolveInterleavedDither(uint2 pixel)
            {
                uint hash = HashUInt(pixel.x ^ (pixel.y * 0x27d4eb2du) ^ 0x9e3779b9u);
                return HashToUnit01(hash);
            }

            float ResolveIgnDither(float4 positionCS)
            {
                uint2 pixelCoord = (uint2)positionCS.xy;
                return ResolveInterleavedDither(pixelCoord);
            }

            float2 ResolveVatFrameUv(float vertexU, float frameIndex, float invFrameCount)
            {
                return float2(vertexU, (frameIndex + 0.5) * invFrameCount);
            }

            float3 SampleVatPosition(float vertexU, float frameIndex, float invFrameCount)
            {
                float2 uv = ResolveVatFrameUv(vertexU, frameIndex, invFrameCount);
                return SAMPLE_TEXTURE2D_LOD(_VatPositionTex, sampler_VatPositionTex, uv, 0).xyz * _VatPositionScale;
            }

            float3 SampleVatNormal(float vertexU, float frameIndex, float invFrameCount, float3 fallbackNormalOS, float normalBlend)
            {
                float2 uv = ResolveVatFrameUv(vertexU, frameIndex, invFrameCount);
                float3 encodedNormal = SAMPLE_TEXTURE2D_LOD(_VatNormalTex, sampler_VatNormalTex, uv, 0).xyz * 2.0 - 1.0;
                float encodedLengthSq = dot(encodedNormal, encodedNormal);
                return lerp(fallbackNormalOS, encodedNormal, normalBlend * step(0.0001, encodedLengthSq));
            }

            float ResolveHitFlash01(float3 boidPositionWS)
            {
                float duration = max(_HitFlashDuration, 0.0001);
                float time01 = saturate(1.0 - ((_Time.y - _HitFlashStartTime) * rcp(duration)));
                float flash01 = smoothstep(0.0, 1.0, time01);
                float radiusMask = step(0.0001, _HitFlashRadius);
                float3 toHit = boidPositionWS - _HitFlashOriginWS.xyz;
                float radial01 = saturate(1.0 - dot(toHit, toHit) * _HitFlashOriginWS.w);
                return saturate(_HitFlashIntensity * flash01 * lerp(1.0, radial01, radiusMask));
            }

            // ══════════════════════════════════════════════════════
            //  VERTEX SHADER
            // ══════════════════════════════════════════════════════

            Varyings vert(Attributes input, uint vertexID : SV_VertexID)
            {
                Varyings output;
                #if defined(UNITY_INSTANCING_ENABLED)
                    UNITY_SETUP_INSTANCE_ID(input);
                    uint instanceID = unity_InstanceID;
                #else
                uint instanceID = input.instanceID;
                #endif
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                // ══════════════════════════════════════════════════
                //  1. READ BOID DATA
                // ══════════════════════════════════════════════════

                uint boidIndex = _BoidUseVisibleIndices > 0.5 ? _VisibleBoidIndices[instanceID] : instanceID;
                BoidData boid = _BoidsBuffer[boidIndex];
                float3 boidPos = boid.position;
                float3 boidAup = boidPos + _TotalUniverseOffset.xyz;
                float3 boidVel = boid.velocity * saturate(_VelocitySleepScale);
                float  speedSq = dot(boidVel, boidVel);
                float  aggressiveMask = (float)((boid.stateFlags & BOID_FLAG_MUTATION_AGGRESSIVE) >> 4u);
                float  aggressiveSpeedScale = lerp(1.0, 2.0, aggressiveMask);
                float  aggressiveAmplitudeScale = lerp(1.0, 2.0, aggressiveMask);
                float  vatSpeed01 = RemapSquaredVelocityVat(speedSq, _VatSpeedReference);
                float  consumedMask = (float)((boid.stateFlags & BOID_FLAG_CONSUMED) >> 3u);
                float  consumed01 = saturate(boid.panic) * consumedMask;
                float  aliveMask = 1.0 - step(0.999, consumed01);
                float  aupPhase = boidAup.x * 13.37;
                float  consumedScale = 1.0 - consumed01;

                // ══════════════════════════════════════════════════
                //  2. PER-INSTANCE RANDOM
                // ══════════════════════════════════════════════════

                uint instanceHash = HashUInt(instanceID);
                float instRand = HashToUnit01(instanceHash);
                output.instanceRand = instRand;
                output.hitFlash = 0.0;
                float lodKeep = saturate(_LodDitherKeep01);
                float lodVisibleMask = step(max(HashToUnit01(instanceHash ^ 0x5f356495u), 0.000001), lodKeep);
                if (aliveMask * lodVisibleMask < 0.5)
                {
                    output.positionCS = float4(2.0, 2.0, 1.0, 1.0);
                    output.normalWS = float3(0.0, 1.0, 0.0);
                    output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                    output.aggressiveMask = aggressiveMask;
                    output.colorBlend = saturate(-input.positionOS.y - _BellyBlend);
                    return output;
                }

                // ══════════════════════════════════════════════════
                //  3. TAIL WAG ANIMATION (vertex displacement)
                // ══════════════════════════════════════════════════
                //
                // Fish model convention:
                //   +Z = head (forward)
                //   -Z = tail (backward)
                //   X  = left/right
                //
                // Displacement formula:
                //   phase = time × (baseFreq + speed × speedInfluence) 
                //           + instanceID × phaseVariance
                //   factor = cheap polynomial tail mask
                //   dx = cheap phase wave × amplitude × factor
                //
                // bodyWaveK creates an S-curve along the body
                // (phase varies along Z = travelling wave).

                float3 localPos = input.positionOS.xyz;
                float3 localNormal = input.normalOS;
                bool useVat = _VatEnabled > 0.5 && _VatFrameCount > 1.0 && _VatVertexCount > 1.0;
                if (useVat)
                {
                    float safeFrameCount = max(_VatFrameCount, 1.0);
                    float invFrameCount = rcp(safeFrameCount);
                    float vertexU = (vertexID + 0.5) * rcp(max(_VatVertexCount, 1.0));
                    float vatMotionSpeed = max(_VatPlaybackSpeed, 0.0) * vatSpeed01 * aggressiveSpeedScale;
                    float vatPhase = frac(_Phase + (_Time.y * vatMotionSpeed * _H8FoveatedVatTimeScale) + (float(instanceID) * max(_VatInstancePhaseScale, 0.0)) + aupPhase * 0.15915494);
                    float vatFrame = vatPhase * safeFrameCount;
                    float vatFrameFloor = floor(vatFrame);
                    float vatFrameCeil = vatFrameFloor + 1.0;
                    vatFrameCeil -= safeFrameCount * step(safeFrameCount, vatFrameCeil);
                    float vatBlend = frac(vatFrame);
                    float3 vatPositionA = SampleVatPosition(vertexU, vatFrameFloor, invFrameCount);
                    float3 vatPositionB = SampleVatPosition(vertexU, vatFrameCeil, invFrameCount);
                    float3 vatPosition = lerp(vatPositionA, vatPositionB, vatBlend);
                    localPos += (vatPosition - localPos) * aggressiveAmplitudeScale;
                    float vatNormalBlend = saturate(_VatNormalBlend);
                    if (vatNormalBlend > 0.0001)
                    {
                        float3 vatNormalA = SampleVatNormal(vertexU, vatFrameFloor, invFrameCount, localNormal, vatNormalBlend);
                        float3 vatNormalB = SampleVatNormal(vertexU, vatFrameCeil, invFrameCount, localNormal, vatNormalBlend);
                        localNormal = FastNormalizeL1(lerp(vatNormalA, vatNormalB, vatBlend), input.normalOS);
                    }
                }
                else
                {
                    // Tail factor: 0 at head (+Z), 1 at tail tip (-Z)
                    // Using -Z so that negative Z (tail) gives positive factor
                    float tailFactor = saturate(-localPos.z);
                    float tailFactorSq = tailFactor * tailFactor;
                    float tailFactorQuartic = tailFactorSq * tailFactorSq;
                    float tailPowerLow = saturate(_TailPower - 1.0);
                    float tailPowerHigh = saturate((_TailPower - 2.0) * 0.5);
                    tailFactor = lerp(lerp(tailFactor, tailFactorSq, tailPowerLow), tailFactorQuartic, tailPowerHigh);

                    // Phase with body wave component
                    float freqAdjusted = (_TailFrequency + vatSpeed01 * _TailSpeedInfluence) * vatSpeed01 * aggressiveSpeedScale;
                    float phase = _Time.y * freqAdjusted * _H8FoveatedVatTimeScale 
                                + aupPhase
                                + float(instanceID) * _TailPhaseVariance;
                    
                    // Body wave: phase varies along Z for S-curve
                    // bodyWaveK = 2.0 creates ~1 full wave along body
                    float bodyWaveK = 2.0;
                    float worldYPhase = (boidAup.y + localPos.y) * _TailWorldYPhase;
                    float wavePhase = phase + worldYPhase + localPos.z * bodyWaveK;

                    // Amplitude scales with speed (faster = smaller wag)
                    float parasiteMode = saturate(_ParasiteMode);
                    float ampAdjusted = _TailAmplitude * (1.0 + 0.3 * instRand) * lerp(1.0, 0.28, parasiteMode) * aggressiveAmplitudeScale;
                    
                    // Apply displacement to local X (horizontal wag)
                    localPos.x += FastSignedTriangleWave(wavePhase) * ampAdjusted * tailFactor;

                    // Subtle Y displacement (vertical undulation, half amplitude)
                    localPos.y += FastSignedTriangleWave(wavePhase * 0.7 + 1.5707963) * ampAdjusted * 0.3 * tailFactor;
                }

                float geometricFinMask = saturate(abs(localPos.x) * 1.8 + saturate(localPos.y) * 0.25) * saturate(1.0 - abs(localPos.z) * 0.35);
                float finMask = saturate(input.color.r) * geometricFinMask;
                float finStretch = ((HashToUnit01(instanceHash ^ 0x6c8e9cf5u) - 0.5) * 2.0) * _FinStretchStrength * finMask;
                localPos += localNormal * finStretch;
                float hitFlash01 = ResolveHitFlash01(boidPos);
                localPos += localNormal * (_HitFlashBloat * hitFlash01);

                // ══════════════════════════════════════════════════
                //  4. SCALE
                // ══════════════════════════════════════════════════

                // Per-instance size variation (±15%)
                float3 aupScaleJitter = 1.0 + frac(boidAup.xyz) * float3(0.20, 0.08, 0.14);
                float scaleVariation = 0.95 + instRand * 0.1;
                localPos *= _FishScale * scaleVariation * aupScaleJitter * consumedScale;
                float lodMorph = lodKeep * lodKeep * (3.0 - 2.0 * lodKeep);
                localPos *= lodMorph;

                // ══════════════════════════════════════════════════
                //  5. ROTATION (LookRotation from velocity)
                // ══════════════════════════════════════════════════

                float3x3 rotMatrix = BuildLookRotation(boidVel);
                
                float3 worldPos = mul(rotMatrix, localPos) + boidPos;
                float3 worldNrm = mul(rotMatrix, localNormal);

                // ══════════════════════════════════════════════════
                //  6. OUTPUT
                // ══════════════════════════════════════════════════

                output.positionCS = TransformWorldToHClip(worldPos);
                output.normalWS   = worldNrm;
                output.uv         = TRANSFORM_TEX(input.uv, _BaseMap);
                output.aggressiveMask = aggressiveMask;
                output.hitFlash = hitFlash01;

                // Belly blend: vertices below local Y center → belly color
                output.colorBlend = saturate(-input.positionOS.y - _BellyBlend);

                return output;
            }

            // ══════════════════════════════════════════════════════
            //  FRAGMENT SHADER
            // ══════════════════════════════════════════════════════

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                // ── Texture sample ──
                half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);

                // ── Per-instance color variation ──
                // Slight hue shift using instance random
                half hueShift = (input.instanceRand - 0.5) * _ColorVariance;
                half3 baseCol = ApplyInstanceHueShift(_BaseColor.rgb, hueShift);

                // ── Belly/back color blend ──
                half parasiteMode = saturate(_ParasiteMode);
                half parasiteAggression = saturate(_ParasiteAggression);
                half3 finalColor = lerp(baseCol, _BellyColor.rgb, input.colorBlend);
                finalColor = lerp(finalColor, _ParasiteBaseColor.rgb, parasiteMode);

                // ── Simple hemispheric lighting ──
                // Ultra-cheap: dot(normal, up) for basic shading.
                // No light probes, no realtime lights — just ambient directionality.
                half NdotUp = dot(input.normalWS, half3(0, 1, 0)) * 0.5 + 0.5;
                
                // Directional hint (fake sun from slightly angled direction)
                half3 fakeLightDir = half3(0.3419h, 0.9117h, 0.2279h);
                half NdotL = saturate(dot(input.normalWS, fakeLightDir));
                
                // Combine: ambient + directional
                half lighting = lerp(0.4, 1.0, NdotL * 0.6 + NdotUp * 0.4);

                // ── Final ──
                half3 color = finalColor * texColor.rgb * lighting;
                color = lerp(color, _HitFlashColor.rgb, saturate((half)input.hitFlash));

                // ── Depth-based fade (underwater atmosphere) ──
                // Fish far from camera get slightly bluer (scatter simulation)
                float depth = input.positionCS.w; // linear depth
                half depthFade = saturate(depth / 150.0); // fade over 150m
                half3 waterColor = half3(0.1, 0.2, 0.35);
                color = lerp(color, waterColor, depthFade * 0.6);

                half aggressiveMask = saturate(input.aggressiveMask);
                half nightFactor = saturate(_HectonNightFactor * _BiolumNightResponse);
                half biolumBudget = nightFactor * max((half)_BiolumStrength, (half)_AggressiveGlowStrength * aggressiveMask);
                half emissiveBudget = max(biolumBudget, parasiteMode);
                if (emissiveBudget > 0.0001h)
                {
                    if (biolumBudget > 0.0001h)
                    {
                        half biolumPhase = (_Time.y * _SargassumBiolumPhaseMultiplier) + input.instanceRand * 6.28318h + input.uv.y * 2.1h + depth * 0.012h;
                        half biolumPulse = 1.0h + (FastTrianglePulse(biolumPhase) * 2.0h - 1.0h) * _BiolumPulseAmplitude;
                        half oceanBiolumInfluence = saturate(_HectonOceanBiolumStrength);
                        half3 biolumColor = lerp(_BiolumColor.rgb, _HectonOceanBiolumColor.rgb, oceanBiolumInfluence * 0.65h);
                        half globalOceanPanic = saturate((half)_GlobalOceanPanic);
                        biolumColor = lerp(biolumColor, (half3)_GlobalOceanPanicColor.rgb, globalOceanPanic);
                        biolumColor = lerp(biolumColor, half3(1.0h, 0.08h, 0.03h), aggressiveMask);
                        half biolumMask = saturate(0.28h + (1.0h - input.colorBlend) * 0.34h + (1.0h - lighting) * 0.22h);
                        half spotNoise = ResolveBiolumSpotNoise(input.uv * max(_BiolumSpotScale, 0.001) + input.instanceRand * half2(13.17h, 31.73h));
                        half spotThreshold = saturate((half)_BiolumSpotThreshold + (input.instanceRand - 0.5h) * 0.12h);
                        half spotMask = step(spotThreshold, spotNoise);
                        half spottedBiolumMask = saturate(biolumMask + spotMask * 0.75h);
                        color += biolumColor * (_BiolumStrength * (1.0h + oceanBiolumInfluence * 0.6h + globalOceanPanic * 0.45h) * nightFactor * biolumPulse * spottedBiolumMask);
                        color += half3(1.0h, 0.05h, 0.02h) * (_AggressiveGlowStrength * aggressiveMask * spotMask * nightFactor);
                    }

                    if (parasiteMode > 0.0001h)
                    {
                        half parasitePhase = (_Time.y * _SargassumBiolumPhaseMultiplier * 1.65h) + input.instanceRand * 9.7h + input.uv.x * 12.0h;
                        half parasitePulse = 1.0h + (FastTrianglePulse(parasitePhase) * 2.0h - 1.0h) * 0.35h;
                        half parasiteMask = saturate(0.35h + (1.0h - abs(input.normalWS.y)) * 0.45h + input.uv.x * 0.2h);
                        color = lerp(color, color * lerp(1.0h, 0.82h, parasiteMode), parasiteMode);
                        color += _ParasiteGlowColor.rgb * (_ParasiteGlowStrength * parasiteMode * parasiteMask * parasitePulse * lerp(0.55h, 1.15h, parasiteAggression));
                    }
                }

                half lodKeep = saturate((half)_LodDitherKeep01);
                if (lodKeep < 0.999h)
                {
                    half lodDither = (half)ResolveIgnDither(input.positionCS);
                    half lodNoiseAmp = lodKeep * (1.0h - lodKeep);
                    color *= saturate(lodKeep + (lodDither - 0.5h) * lodNoiseAmp);
                }

                return half4(color, 1.0);
            }

            ENDHLSL
        }
    }

    // ── No fallback — this shader requires compute buffer support ──
    FallBack Off
}
