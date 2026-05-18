Shader "Hecton8/VFX/PlasmaBeamIndirect"
{
    Properties
    {
        _H8PlasmaUvScroll("UV Scroll", Float) = 9.0
        _H8PlasmaIntensity("Intensity", Float) = 2.4
        _H8PlasmaGlobalQualityWeight("Quality Weight", Range(0, 1)) = 1.0
        _H8PlasmaNoirScatter("Noir Scatter", Range(0, 1)) = 0.18
        _H8PlasmaFrameTime("Frame Time", Float) = 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "PlasmaBeamIndirect"
            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON _ADDITIONAL_LIGHT_SHADOWS
            #pragma skip_variants POINT POINT_COOKIE _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH LIGHTMAP_SHADOW_MIXING SHADOWS_SHADOWMASK

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct BeamVertexDTO
            {
                float3 Position;
                uint ColorPacked;
                float2 UV;
                uint2 Pad;
            };

            StructuredBuffer<BeamVertexDTO> _H8PlasmaBeamVertices;

            CBUFFER_START(UnityPerMaterial)
                float _H8PlasmaUvScroll;
                float _H8PlasmaIntensity;
                float _H8PlasmaGlobalQualityWeight;
                float _H8PlasmaNoirScatter;
                float _H8PlasmaFrameTime;
            CBUFFER_END

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR0;
            };

            float4 UnpackRgba(uint packed)
            {
                float4 rgba;
                rgba.r = (packed & 255u) * (1.0 / 255.0);
                rgba.g = ((packed >> 8) & 255u) * (1.0 / 255.0);
                rgba.b = ((packed >> 16) & 255u) * (1.0 / 255.0);
                rgba.a = ((packed >> 24) & 255u) * (1.0 / 255.0);
                return rgba;
            }

            float Triangle01(float x)
            {
                float f = frac(x);
                return 1.0 - abs(f * 2.0 - 1.0);
            }

            float Hash21(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            Varyings Vert(uint vertexID : SV_VertexID)
            {
                BeamVertexDTO vertex = _H8PlasmaBeamVertices[vertexID];
                Varyings output;
                float3 positionWS = vertex.Position;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = vertex.UV;
                output.color = UnpackRgba(vertex.ColorPacked);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float scroll = input.uv.y - _H8PlasmaFrameTime * max(0.01, _H8PlasmaUvScroll);
                float flowBand = Triangle01(scroll * 7.0 + input.uv.x * 3.0);
                float spark = Hash21(float2(floor(scroll * 32.0), floor(input.uv.x * 16.0) + _H8PlasmaFrameTime * 0.25));
                float core = smoothstep(0.08, 0.58, flowBand);
                float scatterDim = lerp(1.0, 0.46, saturate(_H8PlasmaNoirScatter));
                float qualityFlicker = lerp(0.82, 1.18, saturate(_H8PlasmaGlobalQualityWeight));
                float alpha = saturate(input.color.a * (0.26 + core * 0.72 + step(0.93, spark) * 0.2));
                float3 color = input.color.rgb * (_H8PlasmaIntensity * scatterDim * qualityFlicker);
                color += step(0.97, input.color.a) * core * 0.45;
                return half4((half3)color, (half)alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
