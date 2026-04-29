Shader "Hecton8/UI/PDA Sonar Map"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _MapTint ("Map Tint", Color) = (0.18, 0.94, 0.96, 0.28)
        _EdgeTint ("Edge Tint", Color) = (0.62, 0.98, 1.0, 0.86)
        _ThreatTint ("Threat Tint", Color) = (1.0, 0.18, 0.14, 0.82)
        _SdfVolume ("SDF Volume", 3D) = "" {}
        _SdfRange ("SDF Range", Float) = 1
        _GridDimensions ("Grid Dimensions", Vector) = (32,32,32,0)
        _TimePhase ("Time Phase", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "PDASonarMap"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _MapTint;
                float4 _EdgeTint;
                float4 _ThreatTint;
                float4 _GridDimensions;
                float _SdfRange;
                float _TimePhase;
                int _ThreatPingCount;
                float4 _ThreatPings[8];
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE3D(_SdfVolume);
            SAMPLER(sampler_SdfVolume);

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex.xyz);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            float DecodeSdf(float3 uvw)
            {
                float encoded = SAMPLE_TEXTURE3D(_SdfVolume, sampler_SdfVolume, saturate(uvw)).r;
                return ((encoded * 2.0) - 1.0) * max(_SdfRange, 0.001);
            }

            float3 EstimateNormal(float3 uvw)
            {
                float2 texel = 1.0 / max(_GridDimensions.xy, float2(1.0, 1.0));
                float texelZ = 1.0 / max(_GridDimensions.z, 1.0);
                float dx = DecodeSdf(uvw + float3(texel.x, 0.0, 0.0)) - DecodeSdf(uvw - float3(texel.x, 0.0, 0.0));
                float dy = DecodeSdf(uvw + float3(0.0, texel.y, 0.0)) - DecodeSdf(uvw - float3(0.0, texel.y, 0.0));
                float dz = DecodeSdf(uvw + float3(0.0, 0.0, texelZ)) - DecodeSdf(uvw - float3(0.0, 0.0, texelZ));
                return normalize(float3(dx, dy, dz) + 1e-5);
            }

            half4 frag(v2f i) : SV_Target
            {
                float2 centeredUv = (i.uv * 2.0) - 1.0;
                float3 rayOrigin = float3(centeredUv * float2(0.82, 0.82), -1.2);
                float3 rayDirection = normalize(float3(centeredUv * float2(0.24, 0.24), 1.45));
                float marchStep = 0.03;
                float3 position = rayOrigin;

                float3 mapColor = 0.0;
                float mapAlpha = 0.0;
                float threatGlow = 0.0;

                [loop]
                for (int stepIndex = 0; stepIndex < 96; stepIndex++)
                {
                    position += rayDirection * marchStep;
                    if (any(abs(position) > 0.55))
                        continue;

                    float3 uvw = position + 0.5;
                    float sdf = DecodeSdf(uvw);
                    float surfaceBand = 1.0 - saturate(abs(sdf) / max(_SdfRange * 0.1, 0.001));

                    [unroll]
                    for (int pingIndex = 0; pingIndex < 8; pingIndex++)
                    {
                        if (pingIndex >= _ThreatPingCount)
                            break;

                        float4 ping = _ThreatPings[pingIndex];
                        float pingRadius = lerp(0.06, 0.14, saturate(ping.w));
                        float pingDistance = distance(position, ping.xyz);
                        float pulse = 0.5 + 0.5 * sin((_TimePhase * 7.5) + pingIndex);
                        float pingGlow = saturate(1.0 - (pingDistance / max(pingRadius, 0.001)));
                        threatGlow = max(threatGlow, pingGlow * pulse * ping.w);
                    }

                    if (sdf < 0.0)
                    {
                        float3 normal = EstimateNormal(uvw);
                        float fresnel = pow(1.0 - saturate(abs(dot(normal, rayDirection))), 2.0);
                        float scanline = frac((position.y + 0.5) * 28.0 + (_TimePhase * 0.75));
                        float scanlineGlow = pow(saturate(1.0 - abs(scanline * 2.0 - 1.0)), 6.0);
                        mapColor = lerp(_MapTint.rgb, _EdgeTint.rgb, saturate(surfaceBand + fresnel * 0.35));
                        mapColor += _MapTint.rgb * scanlineGlow * 0.35;
                        mapAlpha = saturate((_MapTint.a * 0.48) + (surfaceBand * 0.28) + (fresnel * 0.22));
                        break;
                    }
                }

                float threatAlpha = threatGlow * _ThreatTint.a;
                float3 finalColor = mapColor + (_ThreatTint.rgb * threatGlow);
                float finalAlpha = saturate(max(mapAlpha, threatAlpha));
                return half4(finalColor, finalAlpha) * i.color;
            }
            ENDHLSL
        }
    }
}
