Shader "Hecton8/World/GroundRadarPingIndirect"
{
    Properties
    {
        _GroundRadarScale ("Ground Radar Scale", Float) = 1.4
        _GroundRadarAlpha ("Ground Radar Alpha", Range(0, 1)) = 0.9
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent+10"
            "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Name "GroundRadarForward"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON _ADDITIONAL_LIGHTS _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHT_SHADOWS _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _GroundRadarScale;
                float _GroundRadarAlpha;
                float _GroundRadarPulse;
            CBUFFER_END

            StructuredBuffer<float4> _GroundRadarPings;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                uint instanceId : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float strength : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float4 ping = _GroundRadarPings[input.instanceId];
                float strength = saturate(ping.w);
                float scale = max(0.05, _GroundRadarScale) * lerp(0.85, 2.35, strength);
                float3 worldPosition = ping.xyz + float3(input.positionOS.x * scale, 0.02, input.positionOS.z * scale);
                output.positionCS = TransformWorldToHClip(worldPosition);
                output.uv = input.uv;
                output.strength = strength;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 centered = input.uv * 2.0 - 1.0;
                float radiusSq = dot(centered, centered);
                float radius = radiusSq * rsqrt(max(0.000001, radiusSq));
                clip(1.0 - radiusSq);

                float pulse = frac(_GroundRadarPulse * 0.85);
                float band = abs(frac(radius * 4.0 - pulse) - 0.5);
                float rings = 1.0 - smoothstep(0.035, 0.12, band);
                float rim = 1.0 - smoothstep(0.94, 1.0, radius);
                float strength = saturate(input.strength);
                float alpha = saturate((rings + rim * 0.65) * strength * _GroundRadarAlpha);
                float3 weakBlue = float3(0.02, 0.10, 0.85);
                float3 strongGreen = float3(0.08, 1.0, 0.28);
                float3 color = lerp(weakBlue, strongGreen, strength);
                return half4(color * alpha, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
