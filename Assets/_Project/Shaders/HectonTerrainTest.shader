Shader "Hecton8/URP/Terrain_TestSimple"
{
    Properties
    {
        [NoScaleOffset] _AlbedoArray("Albedo Array", 2DArray) = "" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D_ARRAY(_AlbedoArray);
            // sampler_LinearRepeat is declared by core GlobalSamplers.hlsl (via Core.hlsl) — do NOT redeclare.

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 texcoord   : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.texcoord;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 col = _AlbedoArray.Sample(sampler_LinearRepeat, float3(i.uv * 10.0, 0.0)).rgb;
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
}
