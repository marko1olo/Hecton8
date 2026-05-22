Shader "HECTON/HUD/DamageHologramInstanced"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1, 0.12, 0.05, 0.86)
        _PointSize ("Point Size", Range(0.002, 0.08)) = 0.024
        _EdgeGain ("Edge Gain", Range(0, 8)) = 2.4
        _Flicker ("Flicker", Range(0, 1)) = 0
        _FloodBlueGain ("Flood Blue Gain", Range(0, 1)) = 0.72
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent+25"
            "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Name "DamageHologram"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON _ADDITIONAL_LIGHTS _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHT_SHADOWS _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _PointSize;
                float _EdgeGain;
                float _Flicker;
                float _FloodBlueGain;
            CBUFFER_END

            StructuredBuffer<float4> _HectonDamageHologramPoints;
            StructuredBuffer<float> _HectonDamageRoomWaterLevels;
            float4x4 _HectonDamageHologramLocalToWorld;
            float4 _HectonDamageHologramParams; // x=time, y=globalAlpha, z=roomCount, w=qualityPressure01
            float4 _HectonDamageHologramBounds; // x=minX, y=maxX, z=minY, w=maxY

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                uint instanceId : SV_InstanceID;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float4 colorAlpha : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float HectonTriangle01(float value)
            {
                return 1.0 - abs(frac(value) * 2.0 - 1.0);
            }

            float HectonResolveFlood01(float3 localPosition)
            {
                int roomCount = min(max((int)_HectonDamageHologramParams.z, 0), 32);
                if (roomCount <= 0)
                    return 0.0;

                float minX = _HectonDamageHologramBounds.x;
                float maxX = max(minX + 0.0001, _HectonDamageHologramBounds.y);
                float normalizedX = saturate((localPosition.x - minX) * rcp(maxX - minX));
                int roomIndex = min(roomCount - 1, (int)floor(normalizedX * (float)roomCount));
                return saturate(_HectonDamageRoomWaterLevels[roomIndex]);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float4 holoPoint = _HectonDamageHologramPoints[input.instanceId];
                float severity = holoPoint.w;
                float scanline = severity < 0.0 ? 1.0 : 0.0;
                float damage01 = scanline > 0.5 ? 0.0 : saturate(severity);
                float flood01 = max(HectonResolveFlood01(holoPoint.xyz), scanline > 0.5 ? saturate(-severity - 1.0) : 0.0);
                float pulse = HectonTriangle01(_HectonDamageHologramParams.x * (scanline > 0.5 ? 0.7 : 1.9) + input.instanceId * 0.011);
                float qualityPressure01 = saturate(_HectonDamageHologramParams.w);
                float size = _PointSize * lerp(0.72, 1.85, max(damage01, scanline * 0.45)) * lerp(1.0, 2.0, qualityPressure01);
                float3 localVertex = holoPoint.xyz + input.positionOS.xyz * size;
                float3 worldPosition = mul(_HectonDamageHologramLocalToWorld, float4(localVertex, 1.0)).xyz;
                output.positionCS = TransformWorldToHClip(worldPosition);
                output.normalWS = mul((float3x3)_HectonDamageHologramLocalToWorld, input.normalOS);

                float3 damageColor = lerp(float3(1.0, 0.84, 0.05), float3(1.0, 0.04, 0.02), damage01);
                float3 scanColor = float3(0.0, 0.88, 1.0);
                float3 floodColor = float3(0.0, 0.12, 0.9);
                float3 baseColor = lerp(damageColor, scanColor, scanline);
                baseColor = lerp(baseColor, floodColor, saturate(flood01 * _FloodBlueGain));
                float alpha = _BaseColor.a * _HectonDamageHologramParams.y;
                alpha *= lerp(0.38 + pulse * 0.32, 0.76 + pulse * 0.24, max(damage01, qualityPressure01));
                alpha *= 1.0 - saturate(_Flicker) * (0.25 + pulse * 0.55);
                output.colorAlpha = float4(baseColor, saturate(alpha));
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float rim = saturate(abs(input.normalWS.z) * _EdgeGain + 0.35);
                float alpha = saturate(input.colorAlpha.a * rim);
                return half4(input.colorAlpha.rgb, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
