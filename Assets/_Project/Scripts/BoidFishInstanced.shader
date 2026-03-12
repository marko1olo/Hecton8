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
        
        [Header(Color Variation)]
        _ColorVariance ("Color Hue Variance", Float) = 0.05
        _BellyColor ("Belly Color", Color) = (0.8, 0.85, 0.9, 1.0)
        _BellyBlend ("Belly Blend (Y threshold)", Float) = 0.0
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
                float  pad0;        // 4 bytes
                float  pad1;        // 4 bytes
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
                
                // Color variation
                float  _ColorVariance;
                float4 _BellyColor;
                float  _BellyBlend;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

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
            /// Simple hash for per-instance variation.
            /// Returns value in [0..1]. Deterministic for same ID.
            /// Cost: 3 ALU (multiply, frac).
            /// </summary>
            float InstanceRandom(uint id)
            {
                return frac(sin(float(id) * 127.1 + 311.7) * 43758.5453);
            }

            // ══════════════════════════════════════════════════════
            //  VERTEX SHADER
            // ══════════════════════════════════════════════════════

            Varyings vert(Attributes input, uint instanceID : SV_InstanceID)
            {
                Varyings output;

                // ══════════════════════════════════════════════════
                //  1. READ BOID DATA
                // ══════════════════════════════════════════════════

                BoidData boid = _BoidsBuffer[instanceID];
                float3 boidPos = boid.position;
                float3 boidVel = boid.velocity;
                float  speed   = length(boidVel);

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

                // Tail factor: 0 at head (+Z), 1 at tail tip (-Z)
                // Using -Z so that negative Z (tail) gives positive factor
                float tailFactor = saturate(-localPos.z);
                tailFactor = pow(tailFactor, _TailPower);

                // Phase with body wave component
                float freqAdjusted = _TailFrequency + speed * _TailSpeedInfluence;
                float phase = _Time.y * freqAdjusted 
                            + float(instanceID) * _TailPhaseVariance;
                
                // Body wave: phase varies along Z for S-curve
                // bodyWaveK = 2.0 creates ~1 full wave along body
                float bodyWaveK = 2.0;
                float wavePhase = phase + localPos.z * bodyWaveK;

                // Amplitude scales with speed (faster = smaller wag)
                float ampAdjusted = _TailAmplitude * (1.0 + 0.3 * instRand);
                
                // Apply displacement to local X (horizontal wag)
                localPos.x += sin(wavePhase) * ampAdjusted * tailFactor;

                // Subtle Y displacement (vertical undulation, half amplitude)
                localPos.y += cos(wavePhase * 0.7) * ampAdjusted * 0.3 * tailFactor;

                // ══════════════════════════════════════════════════
                //  4. SCALE
                // ══════════════════════════════════════════════════

                // Per-instance size variation (±15%)
                float scaleVariation = 0.85 + instRand * 0.3;
                localPos *= _FishScale * scaleVariation;

                // ══════════════════════════════════════════════════
                //  5. ROTATION (LookRotation from velocity)
                // ══════════════════════════════════════════════════

                float3x3 rotMatrix = BuildLookRotation(boidVel);
                
                float3 worldPos = mul(rotMatrix, localPos) + boidPos;
                float3 worldNrm = mul(rotMatrix, input.normalOS);

                // ══════════════════════════════════════════════════
                //  6. OUTPUT
                // ══════════════════════════════════════════════════

                output.positionCS = TransformWorldToHClip(worldPos);
                output.normalWS   = normalize(worldNrm);
                output.uv         = TRANSFORM_TEX(input.uv, _BaseMap);

                // Belly blend: vertices below local Y center → belly color
                output.colorBlend = saturate(-input.positionOS.y - _BellyBlend);

                return output;
            }

            // ══════════════════════════════════════════════════════
            //  FRAGMENT SHADER
            // ══════════════════════════════════════════════════════

            half4 frag(Varyings input) : SV_Target
            {
                // ── Texture sample ──
                half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);

                // ── Per-instance color variation ──
                // Slight hue shift using instance random
                half3 baseCol = _BaseColor.rgb;
                
                // Hue shift via RGB rotation (cheap approximation)
                half hueShift = (input.instanceRand - 0.5) * _ColorVariance;
                baseCol.r += hueShift;
                baseCol.g -= hueShift * 0.5;
                baseCol.b += hueShift * 0.3;
                baseCol = saturate(baseCol);

                // ── Belly/back color blend ──
                half3 finalColor = lerp(baseCol, _BellyColor.rgb, input.colorBlend);

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

                return half4(color, 1.0);
            }

            ENDHLSL
        }
    }

    // ── No fallback — this shader requires compute buffer support ──
    FallBack Off
}