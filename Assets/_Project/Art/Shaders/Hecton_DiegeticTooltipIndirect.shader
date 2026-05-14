Shader "Hecton8/UI/DiegeticTooltipIndirect"
{
    Properties
    {
        _MainTex ("Font Atlas", 2D) = "white" {}
        _GradientScale ("Gradient Scale", Float) = 8
        _FaceDilate ("Face Dilate", Float) = 0
        _DitherEnabled ("Dither Enabled", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "AlphaTest"
            "RenderType" = "TransparentCutout"
        }

        ZWrite On
        Cull Off
        Blend Off
        AlphaToMask On

        Pass
        {
            Name "TooltipGlyphIndirect"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5
            #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON _ADDITIONAL_LIGHT_SHADOWS
            #pragma skip_variants POINT POINT_COOKIE _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH LIGHTMAP_SHADOW_MIXING SHADOWS_SHADOWMASK

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _GradientScale;
                float _FaceDilate;
                float _DitherEnabled;
            CBUFFER_END

            struct TooltipGlyphInstance
            {
                float4x4 LocalToWorld;
                float4 Tint;
                float4 GlyphIndex;
            };

            StructuredBuffer<TooltipGlyphInstance> _TooltipInstances;
            StructuredBuffer<float4> _TooltipUvRects;

            float HectonDitherCoverage(float2 positionCS)
            {
                float2 pixel = floor(positionCS);
                return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
            }

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 tint : COLOR0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                TooltipGlyphInstance instance = _TooltipInstances[input.instanceID];
                uint glyphIndex = (uint)round(max(0.0, instance.GlyphIndex.x));
                float4 uvRect = _TooltipUvRects[glyphIndex];
                float4 world = mul(instance.LocalToWorld, float4(input.positionOS, 1.0));
                output.positionCS = TransformWorldToHClip(world.xyz);
                output.uv = lerp(uvRect.xy, uvRect.zw, input.uv);
                output.tint = instance.Tint;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float sdf = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a;
                float alpha = saturate((sdf - 0.5 + _FaceDilate) * _GradientScale + 0.5);
                float coverage = input.tint.a * alpha;
                float threshold = _DitherEnabled > 0.5 ? HectonDitherCoverage(input.positionCS.xy) : 0.01;
                clip(coverage - threshold);
                return half4(input.tint.rgb, 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
