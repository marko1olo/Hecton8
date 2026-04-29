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
        _VolumeHalfExtent ("Volume Half Extent", Vector) = (0.55,0.55,0.55,0)
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
                float4 _VolumeHalfExtent;
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

            bool TryIntersectVolume(float3 rayOrigin, float3 rayDirection, float3 volumeMin, float3 volumeMax, out float enterDistance, out float exitDistance)
            {
                float3 safeDirection = rayDirection + (abs(rayDirection) < 1e-5.xxx ? 1e-5.xxx : 0.0.xxx);
                float3 inverseDirection = 1.0 / safeDirection;
                float3 t0 = (volumeMin - rayOrigin) * inverseDirection;
                float3 t1 = (volumeMax - rayOrigin) * inverseDirection;
                float3 tMin = min(t0, t1);
                float3 tMax = max(t0, t1);
                enterDistance = max(max(tMin.x, tMin.y), tMin.z);
                exitDistance = min(min(tMax.x, tMax.y), tMax.z);
                return exitDistance > max(enterDistance, 0.0);
            }

            half4 frag(v2f i) : SV_Target
            {
                float2 centeredUv = (i.uv * 2.0) - 1.0;
                float3 volumeHalfExtent = max(_VolumeHalfExtent.xyz, 0.05.xxx);
                float3 volumeMin = -volumeHalfExtent;
                float3 volumeMax = volumeHalfExtent;
                float3 rayOrigin = float3(centeredUv * (volumeHalfExtent.xy * 1.75), -(volumeHalfExtent.z + 0.78));
                float3 rayDirection = normalize(float3(centeredUv * float2(0.18, 0.21), 1.45));
                float enterDistance;
                float exitDistance;
                if (!TryIntersectVolume(rayOrigin, rayDirection, volumeMin, volumeMax, enterDistance, exitDistance))
                    return half4(0.0, 0.0, 0.0, 0.0);

                float marchDistance = max(enterDistance, 0.0);
                float marchLength = max(exitDistance - marchDistance, 0.0);
                float marchStep = max(marchLength / 96.0, 0.008);
                float3 position = rayOrigin + rayDirection * marchDistance;

                float3 mapColor = 0.0;
                float mapAlpha = 0.0;
                float threatGlow = 0.0;
                float3 invVolumeSize = 1.0 / max((volumeHalfExtent * 2.0), 0.001.xxx);
                float3 gridScale = max(_GridDimensions.xyz - 1.0, 1.0.xxx);

                [loop]
                for (int stepIndex = 0; stepIndex < 96; stepIndex++)
                {
                    float3 uvw = saturate((position - volumeMin) * invVolumeSize);
                    float sdf = DecodeSdf(uvw);
                    float surfaceBand = 1.0 - saturate(abs(sdf) / max(_SdfRange * 0.055, 0.001));
                    float3 voxelCell = abs(frac(uvw * gridScale) - 0.5);
                    float voxelWire = 1.0 - smoothstep(0.16, 0.34, min(voxelCell.x, min(voxelCell.y, voxelCell.z)));

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
                        float fresnel = pow(1.0 - saturate(abs(dot(normal, rayDirection))), 2.4);
                        float scanline = frac((position.y + 0.5) * 28.0 + (_TimePhase * 0.75));
                        float scanlineGlow = pow(saturate(1.0 - abs(scanline * 2.0 - 1.0)), 6.0);
                        float wireStrength = saturate((voxelWire * 0.82) + (surfaceBand * 0.35) + (fresnel * 0.25));
                        mapColor = lerp(_MapTint.rgb * 0.14, _EdgeTint.rgb, wireStrength);
                        mapColor += _MapTint.rgb * scanlineGlow * 0.18;
                        mapAlpha = saturate((_MapTint.a * 0.12) + (wireStrength * 0.62) + (surfaceBand * 0.18));
                        break;
                    }

                    position += rayDirection * marchStep;
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
