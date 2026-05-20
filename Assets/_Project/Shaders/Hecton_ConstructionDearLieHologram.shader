Shader "Hecton8/Construction/DearLieHologram"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.08, 1.0, 0.72, 0.72)
        _H8SnapDampen ("Snap Dampen", Float) = 0.0
        _H8SnapWiggleSpeed ("Snap Wiggle Speed", Float) = 18.0
        _H8GlobalQualityWeight ("Global Quality Weight", Float) = 1.0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "DearLieProcedural"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #define H8_GHOST_FLAG_VALID 2u
            #define H8_GHOST_FLAG_SDF_BLOCKED 8u
            #define H8_GHOST_FLAG_BOUNDS_BLOCKED 16u

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _H8SnapDampen;
                float _H8SnapWiggleSpeed;
                float _H8GlobalQualityWeight;
                int _H8BuilderGhostCount;
            CBUFFER_END

            struct BuilderGhostStateRaw
            {
                float4 c0;
                float4 c1;
                float4 c2;
                float4 c3;
                uint4 aup0;
                uint4 aup1;
                uint4 meta0;
                uint4 meta1;
            };

            struct BuilderGhostVisualRaw
            {
                float4 params0;
                float4 validColor;
                float4 invalidColor;
                uint4 meta0;
            };

            StructuredBuffer<BuilderGhostStateRaw> _H8BuilderGhostStates;
            StructuredBuffer<BuilderGhostVisualRaw> _H8BuilderGhostVisuals;

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                half4 color : COLOR0;
                float3 worldPos : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float quality : TEXCOORD2;
                uint flags : TEXCOORD3;
            };

            static const float3 H8CubeVertices[36] =
            {
                float3(-0.5, -0.5, -0.5), float3(-0.5,  0.5, -0.5), float3( 0.5,  0.5, -0.5),
                float3(-0.5, -0.5, -0.5), float3( 0.5,  0.5, -0.5), float3( 0.5, -0.5, -0.5),
                float3( 0.5, -0.5, -0.5), float3( 0.5,  0.5, -0.5), float3( 0.5,  0.5,  0.5),
                float3( 0.5, -0.5, -0.5), float3( 0.5,  0.5,  0.5), float3( 0.5, -0.5,  0.5),
                float3( 0.5, -0.5,  0.5), float3( 0.5,  0.5,  0.5), float3(-0.5,  0.5,  0.5),
                float3( 0.5, -0.5,  0.5), float3(-0.5,  0.5,  0.5), float3(-0.5, -0.5,  0.5),
                float3(-0.5, -0.5,  0.5), float3(-0.5,  0.5,  0.5), float3(-0.5,  0.5, -0.5),
                float3(-0.5, -0.5,  0.5), float3(-0.5,  0.5, -0.5), float3(-0.5, -0.5, -0.5),
                float3(-0.5,  0.5, -0.5), float3(-0.5,  0.5,  0.5), float3( 0.5,  0.5,  0.5),
                float3(-0.5,  0.5, -0.5), float3( 0.5,  0.5,  0.5), float3( 0.5,  0.5, -0.5),
                float3(-0.5, -0.5,  0.5), float3(-0.5, -0.5, -0.5), float3( 0.5, -0.5, -0.5),
                float3(-0.5, -0.5,  0.5), float3( 0.5, -0.5, -0.5), float3( 0.5, -0.5,  0.5)
            };

            float3 ResolveCubeNormal(uint vertexId)
            {
                uint face = (vertexId % 36u) / 6u;
                if (face == 0u) return float3(0.0, 0.0, -1.0);
                if (face == 1u) return float3(1.0, 0.0, 0.0);
                if (face == 2u) return float3(0.0, 0.0, 1.0);
                if (face == 3u) return float3(-1.0, 0.0, 0.0);
                if (face == 4u) return float3(0.0, 1.0, 0.0);
                return float3(0.0, -1.0, 0.0);
            }

            Varyings vert(uint vertexId : SV_VertexID, uint instanceId : SV_InstanceID)
            {
                Varyings output;
                uint safeInstance = min(instanceId, max((uint)_H8BuilderGhostCount, 1u) - 1u);
                BuilderGhostStateRaw state = _H8BuilderGhostStates[safeInstance];
                BuilderGhostVisualRaw visual = _H8BuilderGhostVisuals[safeInstance];
                uint flags = state.aup1.w;
                float phase = asfloat(state.meta0.x);
                float q = saturate(visual.params0.x * _H8GlobalQualityWeight);
                float smoothQ = q * q * (3.0 - (2.0 * q));
                float localPhase = phase + (_Time.y * visual.params0.z * lerp(0.15, 0.65, smoothQ));
                float pulse = 0.82 + (0.18 * sin(localPhase));
                float blocked = ((flags & H8_GHOST_FLAG_VALID) == 0u) ? 1.0 : 0.0;
                float chroma = blocked * lerp(0.006, 0.028, smoothQ);

                float3 local = H8CubeVertices[vertexId % 36u];
                float3 normalOS = ResolveCubeNormal(vertexId);
                float dampen = max(0.0, _H8SnapDampen + visual.params0.y);
                local -= normalOS * dampen * lerp(0.05, 0.18, smoothQ);
                local += normalOS * sin(localPhase + dot(local, float3(13.0, 17.0, 19.0))) * dampen * 0.08;
                local.x += chroma * sin(localPhase + local.y * 7.0);

                float4 world =
                    (state.c0 * local.x) +
                    (state.c1 * local.y) +
                    (state.c2 * local.z) +
                    state.c3;
                output.positionHCS = TransformWorldToHClip(world.xyz);
                output.worldPos = world.xyz;
                output.normalWS = normalize((state.c0.xyz * normalOS.x) + (state.c1.xyz * normalOS.y) + (state.c2.xyz * normalOS.z));
                float4 valid = visual.validColor;
                float4 invalid = visual.invalidColor;
                output.color = half4(lerp(valid.rgb, invalid.rgb, blocked), lerp(valid.a, invalid.a, blocked) * pulse);
                output.quality = q;
                output.flags = flags;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float blocked = ((input.flags & H8_GHOST_FLAG_VALID) == 0u) ? 1.0 : 0.0;
                float q = saturate(input.quality);
                float scan = frac((input.worldPos.y * lerp(3.5, 12.0, q)) + (_Time.y * lerp(0.6, 3.2, q)));
                float line = smoothstep(0.0, 0.08, scan) * (1.0 - smoothstep(0.12, 0.24, scan));
                float rim = pow(saturate(1.0 - abs(dot(normalize(input.normalWS), normalize(GetWorldSpaceViewDir(input.worldPos))))), 2.0);
                half4 color = input.color;
                color.rgb += (half3)line * lerp(0.04, 0.20, q);
                color.rgb += (half3)rim * lerp(0.10, 0.34, q);
                color.r += blocked * lerp(0.05, 0.18, q) * line;
                color.a *= lerp(0.48h, 0.86h, q);
                return color;
            }
            ENDHLSL
        }
    }

    Fallback "Unlit/Color"
}
