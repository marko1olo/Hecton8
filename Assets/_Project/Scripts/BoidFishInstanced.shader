// ============================================================================
// HECTON-8 — BoidFishInstanced.shader
// URP Unlit GPU-Instanced shader для стайных рыб.
//
// АРХИТЕКТУРА:
//   • StructuredBuffer<BoidData> — позиции/скорости из Compute Shader.
//   • SV_InstanceID — индексация в буфере (один draw call → 5000 рыб).
//   • Vertex displacement — процедурная анимация хвоста (sin wave).
//   • LookRotation — вращение модели по направлению скорости.
//   • Zero overhead: no shadows, no fog, no lightmap, no GI.
//
// МОДЕЛЬ РЫБЫ (СОГЛАШЕНИЕ):
//   • Рыба смотрит по +Z (forward).
//   • Хвост по -Z, голова по +Z.
//   • X = horizontal axis (влево-вправо).
//   • Y = vertical axis (вверх-вниз).
//   • Pivot (origin) — в районе центра тела или головы.
//   • Хвостовые вершины имеют ОТРИЦАТЕЛЬНЫЙ Z (local).
//
// TAIL WAG (процедурная анимация):
//   displacement = sin(time × freq + instanceID × phaseOffset) 
//                  × amplitude × pow(abs(localZ), power)
//   
//   localZ < 0 = хвост → максимальное смещение
//   localZ ≈ 0 = центр → минимальное
//   localZ > 0 = голова → нулевое (clamped)
//
//   Displacement применяется к LOCAL X (горизонтальное виляние).
//   Это создаёт естественное S-образное движение тела рыбы.
//
// PERFORMANCE на MX350:
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
        _FinStretchStrength ("Fin Stretch Strength", Range(0, 0.35)) = 0.16
        
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

        // ── Один pass, без теней, без depth prepass ──
        // ShadowCaster и DepthOnly passes НАМЕРЕННО ОТСУТСТВУЮТ.
        // 5000 рыб × shadow pass = убийство для MX350.

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

            // ── Минимальные фичи ──
            #pragma multi_compile_instancing
            #pragma target 4.5  // Required for StructuredBuffer

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

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _BaseMap_ST;
                float  _FishScale;
                
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
                float  _FinStretchStrength;
                
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

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_VatPositionTex);
            SAMPLER(sampler_VatPositionTex);
            TEXTURE2D(_VatNormalTex);
            SAMPLER(sampler_VatNormalTex);
            TEXTURE2D(_HectonCausticsTextureA);
            SAMPLER(sampler_HectonCausticsTextureA);
            TEXTURE2D(_BlueNoiseTex);
            SAMPLER(sampler_BlueNoiseTex);
            float4 _HectonCausticsTextureParams;
            float4 _BlueNoiseTex_TexelSize;

            #define BOID_FLAG_CONSUMED 8u
            #define BOID_FLAG_MUTATION_AGGRESSIVE 16u

            // ══════════════════════════════════════════════════════
            //  VERTEX / FRAGMENT STRUCTURES
            // ══════════════════════════════════════════════════════

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float  colorBlend : TEXCOORD2;   // belly/back blend factor
                float  instanceRand : TEXCOORD3; // per-instance random [0..1]
                float  aliveMask : TEXCOORD4;
                float  aggressiveMask : TEXCOORD5;
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
            /// Cost: ~12 ALU (3× cross, 3× normalize).
            /// </summary>
            float3x3 BuildLookRotation(float3 forward)
            {
                // Normalize forward
                float fwdLen = length(forward);
                
                // Safety: if velocity is near-zero, default to +Z
                if (fwdLen < 0.0001)
                    forward = float3(0, 0, 1);
                else
                    forward = forward / fwdLen;
                
                // Choose up vector (avoid parallel case)
                float3 up = float3(0, 1, 0);
                if (abs(dot(forward, up)) > 0.999)
                    up = float3(1, 0, 0);
                
                // Build orthonormal basis
                float3 right = normalize(cross(up, forward));
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

            float InstanceRandom(uint id)
            {
                return (float)(HashUInt(id) & 0x00ffffffu) * (1.0 / 16777216.0);
            }

            float HashTile2(float2 value)
            {
                uint2 cell = (uint2)floor(abs(value) * 4096.0);
                return InstanceRandom(cell.x ^ (cell.y * 0x9e3779b9u));
            }

            half3 ApplyInstanceHueShift(half3 color, half hueShift)
            {
                half amount = saturate(abs(hueShift));
                half3 positiveShift = color.gbr;
                half3 negativeShift = color.brg;
                half3 shifted = hueShift >= 0.0h ? positiveShift : negativeShift;
                return saturate(lerp(color, shifted, amount));
            }

            float ResolveBiolumSpotNoise(float2 uv)
            {
                float2 tiledUv = frac(uv);
                if (_HectonCausticsTextureParams.x > 0.5)
                    return SAMPLE_TEXTURE2D(_HectonCausticsTextureA, sampler_HectonCausticsTextureA, tiledUv).r;

                return HashTile2(tiledUv);
            }

            float ResolveInterleavedDither(float2 pixel)
            {
                uint2 p = (uint2)pixel;
                uint hash = HashUInt(p.x ^ (p.y * 0x27d4eb2du) ^ 0x9e3779b9u);
                return (float)(hash & 255u) * (1.0 / 255.0);
            }

            float ResolveBlueNoiseDither(float4 positionCS)
            {
                float2 pixel = floor(positionCS.xy);
                if (_BlueNoiseTex_TexelSize.z > 0.0001 && _BlueNoiseTex_TexelSize.w > 0.0001)
                {
                    float2 temporalOffset = frac(_Time.y * float2(0.75487766, 0.56984029));
                    float2 blueNoiseUv = frac(pixel * _BlueNoiseTex_TexelSize.xy + temporalOffset);
                    return SAMPLE_TEXTURE2D(_BlueNoiseTex, sampler_BlueNoiseTex, blueNoiseUv).r;
                }

                return ResolveInterleavedDither(pixel);
            }

            float2 ResolveVatFrameUv(uint vertexID, float frameIndex)
            {
                float safeVertexCount = max(_VatVertexCount, 1.0);
                float safeFrameCount = max(_VatFrameCount, 1.0);
                return float2(
                    (vertexID + 0.5) / safeVertexCount,
                    (frameIndex + 0.5) / safeFrameCount);
            }

            float3 SampleVatPosition(uint vertexID, float frameIndex)
            {
                float2 uv = ResolveVatFrameUv(vertexID, frameIndex);
                return SAMPLE_TEXTURE2D_LOD(_VatPositionTex, sampler_VatPositionTex, uv, 0).xyz * _VatPositionScale;
            }

            float3 SampleVatNormal(uint vertexID, float frameIndex, float3 fallbackNormalOS)
            {
                float2 uv = ResolveVatFrameUv(vertexID, frameIndex);
                float3 encodedNormal = SAMPLE_TEXTURE2D_LOD(_VatNormalTex, sampler_VatNormalTex, uv, 0).xyz * 2.0 - 1.0;
                float encodedLengthSq = dot(encodedNormal, encodedNormal);
                if (encodedLengthSq <= 0.0001)
                    return fallbackNormalOS;

                float3 vatNormal = encodedNormal * rsqrt(encodedLengthSq);
                return normalize(lerp(fallbackNormalOS, vatNormal, saturate(_VatNormalBlend)));
            }

            // ══════════════════════════════════════════════════════
            //  VERTEX SHADER
            // ══════════════════════════════════════════════════════

            Varyings vert(Attributes input, uint rawInstanceID : SV_InstanceID, uint vertexID : SV_VertexID)
            {
                Varyings output;

                uint instanceID = rawInstanceID;

                // ══════════════════════════════════════════════════
                //  1. READ BOID DATA
                // ══════════════════════════════════════════════════

                BoidData boid = _BoidsBuffer[instanceID];
                float3 boidPos = boid.position;
                float3 boidVel = boid.velocity * saturate(_VelocitySleepScale);
                float  speed   = length(boidVel);
                bool   isConsumed = (boid.stateFlags & BOID_FLAG_CONSUMED) != 0u;
                float  aggressiveMask = (boid.stateFlags & BOID_FLAG_MUTATION_AGGRESSIVE) != 0u ? 1.0 : 0.0;
                float  aggressiveSpeedScale = lerp(1.0, 2.0, aggressiveMask);
                float  velocityAnim01 = saturate(speed / max(_VatSpeedReference, 0.001));
                float  consumed01 = isConsumed ? saturate(boid.panic) : 0.0;
                float  aliveMask = consumed01 < 0.999 ? 1.0 : 0.0;
                float  aupPhase = boidPos.x * 13.37;
                float  consumedScale = 1.0 - consumed01;

                // ══════════════════════════════════════════════════
                //  2. PER-INSTANCE RANDOM
                // ══════════════════════════════════════════════════

                float instRand = InstanceRandom(instanceID);
                output.instanceRand = instRand;

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
                //   factor = pow(saturate(-localZ / meshLength), power)
                //   dx = sin(phase + localZ × bodyWaveK) × amplitude × factor
                //
                // bodyWaveK creates an S-curve along the body
                // (phase varies along Z = travelling wave).

                float3 localPos = input.positionOS.xyz;
                float3 localNormal = input.normalOS;
                bool useVat = _VatEnabled > 0.5 && _VatFrameCount > 1.0 && _VatVertexCount > 1.0;
                if (useVat)
                {
                    float safeFrameCount = max(_VatFrameCount, 1.0);
                    float vatMotionSpeed = max(_VatPlaybackSpeed, 0.0) * velocityAnim01 * aggressiveSpeedScale;
                    float vatPhase = frac((_Time.y * vatMotionSpeed) + (float(instanceID) * max(_VatInstancePhaseScale, 0.0)) + aupPhase * 0.15915494);
                    float vatFrame = vatPhase * safeFrameCount;
                    float vatFrameFloor = floor(vatFrame);
                    float vatFrameCeil = fmod(vatFrameFloor + 1.0, safeFrameCount);
                    float vatBlend = frac(vatFrame);
                    float3 vatPositionA = SampleVatPosition(vertexID, vatFrameFloor);
                    float3 vatPositionB = SampleVatPosition(vertexID, vatFrameCeil);
                    float3 vatNormalA = SampleVatNormal(vertexID, vatFrameFloor, localNormal);
                    float3 vatNormalB = SampleVatNormal(vertexID, vatFrameCeil, localNormal);
                    localPos = lerp(vatPositionA, vatPositionB, vatBlend);
                    localNormal = normalize(lerp(vatNormalA, vatNormalB, vatBlend));
                }
                else
                {
                    // Tail factor: 0 at head (+Z), 1 at tail tip (-Z)
                    // Using -Z so that negative Z (tail) gives positive factor
                    float tailFactor = saturate(-localPos.z);
                    tailFactor = pow(tailFactor, _TailPower);

                    // Phase with body wave component
                    float freqAdjusted = (_TailFrequency + speed * _TailSpeedInfluence) * velocityAnim01 * aggressiveSpeedScale;
                    float phase = _Time.y * freqAdjusted 
                                + aupPhase
                                + float(instanceID) * _TailPhaseVariance;
                    
                    // Body wave: phase varies along Z for S-curve
                    // bodyWaveK = 2.0 creates ~1 full wave along body
                    float bodyWaveK = 2.0;
                    float worldYPhase = (boidPos.y + localPos.y) * _TailWorldYPhase;
                    float wavePhase = phase + worldYPhase + localPos.z * bodyWaveK;

                    // Amplitude scales with speed (faster = smaller wag)
                    float parasiteMode = saturate(_ParasiteMode);
                    float ampAdjusted = _TailAmplitude * (1.0 + 0.3 * instRand) * lerp(1.0, 0.28, parasiteMode);
                    
                    // Apply displacement to local X (horizontal wag)
                    localPos.x += sin(wavePhase) * ampAdjusted * tailFactor;

                    // Subtle Y displacement (vertical undulation, half amplitude)
                    localPos.y += cos(wavePhase * 0.7) * ampAdjusted * 0.3 * tailFactor;
                }

                float finMask = saturate(abs(localPos.x) * 1.8 + saturate(localPos.y) * 0.25) * saturate(1.0 - abs(localPos.z) * 0.35);
                float finStretch = 1.0 + ((InstanceRandom(instanceID ^ 0x6c8e9cf5u) - 0.5) * 2.0) * _FinStretchStrength * finMask;
                localPos.x *= finStretch;

                // ══════════════════════════════════════════════════
                //  4. SCALE
                // ══════════════════════════════════════════════════

                // Per-instance size variation (±15%)
                float aupScaleJitter = 1.0 + frac(boidPos.x) * 0.2;
                float scaleVariation = 0.95 + instRand * 0.1;
                localPos *= _FishScale * scaleVariation * aupScaleJitter * consumedScale;

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
                output.normalWS   = normalize(worldNrm);
                output.uv         = TRANSFORM_TEX(input.uv, _BaseMap);
                output.aliveMask  = aliveMask;
                output.aggressiveMask = aggressiveMask;

                // Belly blend: vertices below local Y center → belly color
                output.colorBlend = saturate(-input.positionOS.y - _BellyBlend);

                return output;
            }

            // ══════════════════════════════════════════════════════
            //  FRAGMENT SHADER
            // ══════════════════════════════════════════════════════

            half4 frag(Varyings input) : SV_Target
            {
                clip(input.aliveMask - 0.5);
                clip(_LodDitherKeep01 - ResolveBlueNoiseDither(input.positionCS));
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
                half3 fakeLightDir = normalize(half3(0.3, 0.8, 0.2));
                half NdotL = saturate(dot(input.normalWS, fakeLightDir));
                
                // Combine: ambient + directional
                half lighting = lerp(0.4, 1.0, NdotL * 0.6 + NdotUp * 0.4);

                // ── Final ──
                half3 color = finalColor * texColor.rgb * lighting;

                // ── Depth-based fade (underwater atmosphere) ──
                // Fish far from camera get slightly bluer (scatter simulation)
                float depth = input.positionCS.w; // linear depth
                half depthFade = saturate(depth / 150.0); // fade over 150m
                half3 waterColor = half3(0.1, 0.2, 0.35);
                color = lerp(color, waterColor, depthFade * 0.6);

                half nightFactor = saturate(_HectonNightFactor * _BiolumNightResponse);
                half biolumPhase = (_Time.y * _SargassumBiolumPhaseMultiplier) + input.instanceRand * 6.28318h + input.uv.y * 2.1h + depth * 0.012h;
                half biolumPulse = 1.0h + sin(biolumPhase) * _BiolumPulseAmplitude;
                half oceanBiolumInfluence = saturate(_HectonOceanBiolumStrength);
                half3 biolumColor = lerp(_BiolumColor.rgb, _HectonOceanBiolumColor.rgb, oceanBiolumInfluence * 0.65h);
                half globalOceanPanic = saturate((half)_GlobalOceanPanic);
                biolumColor = lerp(biolumColor, (half3)_GlobalOceanPanicColor.rgb, globalOceanPanic);
                biolumColor = lerp(biolumColor, half3(1.0h, 0.08h, 0.03h), saturate(input.aggressiveMask));
                half spotNoise = ResolveBiolumSpotNoise(input.uv * max(_BiolumSpotScale, 0.001) + input.instanceRand * half2(13.17h, 31.73h));
                half spotMask = step((half)_BiolumSpotThreshold, spotNoise);
                half biolumMask = saturate(0.28h + (1.0h - input.colorBlend) * 0.34h + (1.0h - lighting) * 0.22h);
                half spottedBiolumMask = saturate(biolumMask + spotMask * 0.75h);
                color += biolumColor * (_BiolumStrength * (1.0h + oceanBiolumInfluence * 0.6h + globalOceanPanic * 0.45h) * nightFactor * biolumPulse * spottedBiolumMask);
                color += half3(1.0h, 0.05h, 0.02h) * (_AggressiveGlowStrength * saturate(input.aggressiveMask) * spotMask * nightFactor);

                half parasitePulse = 1.0h + sin((_Time.y * _SargassumBiolumPhaseMultiplier * 1.65h) + input.instanceRand * 9.7h + input.uv.x * 12.0h) * 0.35h;
                half parasiteMask = saturate(0.35h + (1.0h - abs(input.normalWS.y)) * 0.45h + input.uv.x * 0.2h);
                color = lerp(color, color * lerp(1.0h, 0.82h, parasiteMode), parasiteMode);
                color += _ParasiteGlowColor.rgb * (_ParasiteGlowStrength * parasiteMode * parasiteMask * parasitePulse * lerp(0.55h, 1.15h, parasiteAggression));

                return half4(color, 1.0);
            }

            ENDHLSL
        }
    }

    // ── No fallback — this shader requires compute buffer support ──
    FallBack Off
}
