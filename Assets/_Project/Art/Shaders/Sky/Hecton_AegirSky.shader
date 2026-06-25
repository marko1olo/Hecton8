Shader "HECTON/Sky/Hecton_AegirSky"
{
    Properties
    {
        _AegirBandTex ("Aegir Band Texture", 2D) = "gray" {}
        _AegirExposure ("Aegir Exposure", Range(0.1, 4.0)) = 1.35
        _StarDensity ("Star Density", Range(0.95, 0.9999)) = 0.9965
        _StarIntensity ("Star Intensity", Range(0.0, 4.0)) = 1.15
        _AtmosphereTint ("Atmosphere Tint", Color) = (0.42, 0.73, 1.0, 1.0)
        _RingTint ("Ring Tint", Color) = (0.78, 0.9, 1.0, 1.0)
        _VoidTint ("Void Tint", Color) = (0.001, 0.0014, 0.0022, 1.0)
        _ScreenAegirCenterRadius ("Screen Aegir Center Radius", Vector) = (0.60, 0.56, 0.155, 0.0)
        _ScreenAegirOpacity ("Screen Aegir Opacity", Range(0.0, 1.0)) = 0.0
        _RingOpacity ("Ring Opacity", Range(0.0, 1.0)) = 0.0
        _DiscTextureWeight ("Disc Texture Weight", Range(0.0, 1.0)) = 0.82
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent-40"
            "RenderType" = "Transparent"
            "PreviewType" = "Skybox"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off
        ZWrite Off
        ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "AegirSky"

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile _ QUALITY_LOW QUALITY_MED QUALITY_HIGH
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_AegirBandTex);
            SAMPLER(sampler_AegirBandTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _AtmosphereTint;
                float4 _RingTint;
                float4 _VoidTint;
                float _AegirExposure;
                float _StarDensity;
                float _StarIntensity;
                float _pad0;
                float4 _ScreenAegirCenterRadius;
                float _ScreenAegirOpacity;
                float _RingOpacity;
                float _DiscTextureWeight;
                float _pad1;
            CBUFFER_END

            float4 _H8AegirSunDirection;
            float4 _H8AegirPlanetCenterRadius;
            float4 _H8AegirRingPlaneInner;
            float4 _H8AegirOrbitScalars;
            float _H8AegirFlowPhase;
            float _H8AegirFlowPhaseValid;
            float _H8AegirStormEmission;
            float _H8GlobalQualityWeight;

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 rayOS : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.rayOS = input.positionOS.xyz;
                return output;
            }

            float3 SafeUnit(float3 value)
            {
                return value * rsqrt(max(dot(value, value), 0.00000001));
            }

            float AegirFlowPhase(float flowSpeed)
            {
                return _H8AegirFlowPhaseValid > 0.5
                    ? _H8AegirFlowPhase
                    : _Time.y * flowSpeed;
            }

            float AegirStormEmission()
            {
                return _H8AegirFlowPhaseValid > 0.5
                    ? clamp(_H8AegirStormEmission, 0.0, 4.0)
                    : 1.0;
            }

            float Hash13(float3 value)
            {
                value = frac(value * 0.1031);
                value += dot(value, value.yzx + 33.33);
                return frac((value.x + value.y) * value.z);
            }

            float Hash11(float value)
            {
                value = frac(value * 0.1031);
                value *= value + 33.33;
                value *= value + value;
                return frac(value);
            }

            float Hash21(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float H8AegirFastSqrt(float value)
            {
                return value * rsqrt(max(value, 0.00000001));
            }

            float H8AegirFastAtan2(float y, float x)
            {
                float ax = abs(x);
                float ay = abs(y);
                float major = max(max(ax, ay), 0.000001);
                float a = min(ax, ay) / major;
                float s = a * a;
                float r = (((-0.0464964749 * s + 0.15931422) * s - 0.327622764) * s * a) + a;
                r = ay > ax ? 1.57079637 - r : r;
                r = x < 0.0 ? 3.14159265 - r : r;
                return y < 0.0 ? -r : r;
            }

            float Noise2D(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i + float2(0.0, 0.0));
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float FBM(float2 p)
            {
                float sum = 0.0;
                float amp = 0.5;
                float freq = 1.0;
                
                #if defined(QUALITY_HIGH)
                [unroll(4)]
                for(int i = 0; i < 4; i++)
                #elif defined(QUALITY_LOW)
                [unroll(2)]
                for(int i = 0; i < 2; i++)
                #else
                [unroll(3)]
                for(int i = 0; i < 3; i++)
                #endif
                {
                    sum += amp * Noise2D(p * freq);
                    freq *= 2.13;
                    amp *= 0.48;
                    // Keep rotation to break grid alignment
                    p = float2(p.x * 0.866 - p.y * 0.5, p.x * 0.5 + p.y * 0.866);
                }
                return sum;
            }

            float2 AegirUv(float3 surfaceNormal)
            {
                float longitude = H8AegirFastAtan2(surfaceNormal.z, surfaceNormal.x);
                float latitude = saturate(surfaceNormal.y * 0.5 + 0.5);
                return float2(frac(longitude * 0.15915494 + 0.5), latitude);
            }

            bool RaySphere(float3 rayDir, float3 center, float radius, out float hitT, out float3 hitPoint, out float3 hitNormal)
            {
                float3 oc = -center;
                float b = dot(oc, rayDir);
                float c = dot(oc, oc) - radius * radius;
                float h = b * b - c;
                if (h <= 0.0)
                {
                    hitT = 0.0;
                    hitPoint = 0.0;
                    hitNormal = 0.0;
                    return false;
                }

                hitT = -b - H8AegirFastSqrt(h);
                if (hitT <= 0.0)
                {
                    hitPoint = 0.0;
                    hitNormal = 0.0;
                    return false;
                }

                hitPoint = rayDir * hitT;
                float3 relative = hitPoint - center;
                hitNormal = relative * rsqrt(max(dot(relative, relative), 0.000001));
                return true;
            }

            bool RayRingPlane(float3 rayDir, float3 center, float3 planeNormal, out float hitT, out float3 localPoint, out float radiusSq)
            {
                float denom = dot(rayDir, planeNormal);
                if (abs(denom) < 0.00001)
                {
                    hitT = 0.0;
                    localPoint = 0.0;
                    radiusSq = 0.0;
                    return false;
                }

                hitT = dot(center, planeNormal) / denom;
                if (hitT <= 0.0)
                {
                    localPoint = 0.0;
                    radiusSq = 0.0;
                    return false;
                }

                localPoint = rayDir * hitT - center;
                radiusSq = dot(localPoint, localPoint);
                return true;
            }

            float HardRingMaskSq(float radiusSq, float innerRadiusSq, float outerRadiusSq)
            {
                return step(innerRadiusSq, radiusSq) * step(radiusSq, outerRadiusSq);
            }

            float RingShadow(float3 samplePoint, float3 lightDir, float3 center, float3 planeNormal, float innerRadiusSq, float outerRadiusSq)
            {
                float denom = dot(lightDir, planeNormal);
                if (abs(denom) < 0.00001)
                    return 0.0;

                float hitT = dot(center - samplePoint, planeNormal) / denom;
                if (hitT <= 0.0)
                    return 0.0;

                float3 localPoint = samplePoint + lightDir * hitT - center;
                float radiusSq = dot(localPoint, localPoint);
                float mask = HardRingMaskSq(radiusSq, innerRadiusSq, outerRadiusSq);
                float lane = Hash11(floor(radiusSq * 96.0));
                return mask * (0.76 + lane * 0.24);
            }

            float3 StarField(float3 rayDir, float quality)
            {
                float grid = lerp(220.0, 520.0, quality);
                float3 cell = floor(rayDir * grid);
                float3 local = frac(rayDir * grid) - 0.5;
                float seed = Hash13(cell);
                float pin = saturate(1.0 - dot(local, local) * 52.0);
                float pulse = abs(frac(_Time.y * (0.025 + seed * 0.035) + seed) - 0.5) * 2.0;
                float star = step(_StarDensity, seed) * pin * lerp(0.72, 1.0, pulse);
                return star * _StarIntensity * lerp(0.55, 1.35, quality);
            }

            float3 DrawScreenSpaceAegir(float3 background, float4 positionCS, float quality, float flowSpeed, float systemVisibility, inout float alpha)
            {
                float2 screenSize = max(_ScreenParams.xy, float2(1.0, 1.0));
                float2 screenUv = positionCS.xy / screenSize;
                float aspect = screenSize.x / screenSize.y;
                float2 anchor = _ScreenAegirCenterRadius.xy;
                float2 delta = screenUv - anchor;
                delta.x *= aspect;

                float radius = max(0.035, _ScreenAegirCenterRadius.z * lerp(0.94, 1.06, saturate(quality)));
                float dist = length(delta);
                float discMask = 1.0 - smoothstep(radius - 0.004, radius + 0.014, dist);

                float2 ringDelta = delta;
                ringDelta.y *= lerp(5.80, 4.70, quality);
                float ringDistance = length(ringDelta);
                float ringInner = radius * 1.10;
                float ringOuter = radius * lerp(1.72, 2.16, quality);
                float ringMask =
                    smoothstep(ringInner - 0.018, ringInner + 0.020, ringDistance) *
                    (1.0 - smoothstep(ringOuter - 0.020, ringOuter + 0.035, ringDistance));
                float ringLaneA = Hash11(floor(ringDistance * 31.0 + delta.x * 6.0));
                float ringLaneB = Hash11(floor(ringDistance * 57.0 - delta.x * 4.0));
                float dustyLane = lerp(0.46, 1.0, ringLaneA) * lerp(0.58, 1.0, ringLaneB);
                float broadBand = 0.64 + 0.36 * sin(ringDistance * 38.0 + delta.x * 2.1);
                float ringAlpha = ringMask * dustyLane * broadBand * _RingOpacity * lerp(0.58, 1.0, quality) * systemVisibility;
                float ringBackOcclusion = lerp(1.0 - discMask * 0.86, 1.0, smoothstep(-0.015, 0.025, delta.y));
                float3 ringColor = _RingTint.rgb * lerp(0.24, 0.72, dustyLane) * lerp(0.52, 0.95, quality);
                background = lerp(background, ringColor, ringAlpha * ringBackOcclusion);
                alpha = max(alpha, ringAlpha * ringBackOcclusion);

                float radiusSafe = max(radius, 0.0001);
                float2 normalXY = delta / radiusSafe;
                float normalZ = H8AegirFastSqrt(saturate(1.0 - dot(normalXY, normalXY)));
                float3 normal = SafeUnit(float3(normalXY.x, normalXY.y, normalZ));
                float3 lightDir = SafeUnit(float3(-0.42, 0.24, 0.88));
                float light = saturate(dot(normal, lightDir) * 0.86 + 0.38);
                float limb = saturate(1.0 - normalZ);
                float latitude01 = saturate(normalXY.y * 0.5 + 0.5);
                float longitude01 = frac(normalXY.x * 0.46 + 0.5);
                
                float flowPhase = AegirFlowPhase(flowSpeed);

                // --- PROCEDURAL MASTERPIECE: FBM DOMAIN WARPING ---
                float2 noiseCoords = float2(longitude01 * 6.0 + flowPhase * 0.5, latitude01 * 6.0);
                float warpX = FBM(noiseCoords + float2(flowPhase * 0.2, 0.0));
                float warpY = FBM(noiseCoords + float2(12.34, 56.78) - flowPhase * 0.15);
                float2 warp = (float2(warpX, warpY) - 0.5) * 0.18; // Swirling turbulence
                
                // Zonal winds (bands moving at different speeds)
                float windProfile = FBM(float2(latitude01 * 12.0, 0.0));
                float zonalWind = (windProfile - 0.5) * 1.5 * flowPhase;

                float2 bandUv = float2(frac(longitude01 + zonalWind + warp.x), saturate(latitude01 + warp.y));
                float2 detailUv = float2(frac(longitude01 * 1.73 - zonalWind * 1.2 + warp.x * 1.5), saturate(latitude01 * 0.94 + warp.y * 1.2 + 0.03));
                
                float3 bands = SAMPLE_TEXTURE2D(_AegirBandTex, sampler_AegirBandTex, bandUv).rgb;
                float3 detailBands = SAMPLE_TEXTURE2D(_AegirBandTex, sampler_AegirBandTex, detailUv).rgb;
                float textureLuma = saturate(dot(bands, float3(0.3333, 0.3333, 0.3333)) * 2.10);
                float detailLuma = saturate(dot(detailBands, float3(0.3333, 0.3333, 0.3333)) * 1.65);
                float cloudTexture = saturate(textureLuma * 0.72 + detailLuma * 0.34);
                float bandBreakup = 0.78 + Hash11(floor((latitude01 + normalXY.x * 0.17) * 89.0)) * 0.22;
                
                float3 coldGas = float3(0.020, 0.070, 0.082);
                float3 deepTeal = float3(0.050, 0.220, 0.270);
                float3 iceCloud = float3(0.255, 0.690, 0.760);
                float3 stormCopper = float3(0.300, 0.185, 0.090);
                float stormEmission = AegirStormEmission();
                
                float3 textureGas = lerp(deepTeal, iceCloud, cloudTexture);
                
                // Procedural mix driven by FBM and textures, completely eliminating raw sine stripes
                float proceduralDetail = FBM(noiseCoords * 3.0 + warp * 5.0);
                float3 proceduralGas = lerp(deepTeal, iceCloud, proceduralDetail * 0.4 + cloudTexture * 0.6);
                proceduralGas = lerp(proceduralGas, textureGas, _DiscTextureWeight);
                
                // Giant storms (Red spots / copper vortices)
                float stormMask = FBM(noiseCoords * 1.5 - float2(flowPhase * 0.3, 0.0));
                stormMask = smoothstep(0.65, 0.85, stormMask); // isolate vortices
                proceduralGas = lerp(proceduralGas, stormCopper, stormMask * cloudTexture * 0.8 * stormEmission * saturate(quality + 0.25));
                proceduralGas *= _AegirExposure * bandBreakup;
                float nightFill = saturate(0.28 + limb * 0.45);
                float3 gasColor = lerp(coldGas * nightFill, proceduralGas, light);
                gasColor += _AtmosphereTint.rgb * limb * limb * lerp(0.44, 0.92, quality);
                gasColor += float3(0.03, 0.18, 0.22) * saturate(1.0 - light) * (0.35 + limb * 0.5);
                gasColor *= 1.0 - smoothstep(0.66, 0.98, abs(normalXY.y)) * 0.30;
                gasColor *= 1.0 - smoothstep(0.0, 0.72, max(-normalXY.x * 0.38 + normalXY.y * 0.22, 0.0)) * 0.10;

                float discAlpha = discMask * _ScreenAegirOpacity * systemVisibility;
                alpha = max(alpha, discAlpha);
                return lerp(background, gasColor, discAlpha);
            }

            float3 DrawAegir(float3 rayDir, float3 center, float3 lightDir, float3 ringNormal, float quality, float flowSpeed, float ringInnerSq, float ringOuterSq, float shadowStrength, float3 hitPoint, float3 hitNormal)
            {
                float2 uv = AegirUv(hitNormal);
                float flowWeight = saturate((quality - 0.08) * 1.2);
                float flowPhase = AegirFlowPhase(flowSpeed);

                float2 noiseCoords = float2(uv.x * 6.0 + flowPhase * 0.5, uv.y * 6.0);
                float warpX = FBM(noiseCoords + float2(flowPhase * 0.2, 0.0));
                float warpY = FBM(noiseCoords + float2(12.34, 56.78) - flowPhase * 0.15);
                float2 warp = (float2(warpX, warpY) - 0.5) * 0.18 * flowWeight;
                
                float windProfile = FBM(float2(uv.y * 12.0, 0.0));
                float zonalWind = (windProfile - 0.5) * 1.5 * flowPhase * flowWeight;
                
                float2 bandUv = float2(frac(uv.x + zonalWind + warp.x), saturate(uv.y + warp.y));
                float2 detailUv = float2(frac(uv.x * 1.73 - zonalWind * 1.2 + warp.x * 1.5), saturate(uv.y * 0.94 + warp.y * 1.2 + 0.03));

                float3 bands = SAMPLE_TEXTURE2D(_AegirBandTex, sampler_AegirBandTex, bandUv).rgb;
                float3 detailBands = SAMPLE_TEXTURE2D(_AegirBandTex, sampler_AegirBandTex, detailUv).rgb;
                
                float textureLuma = saturate(dot(bands, float3(0.3333, 0.3333, 0.3333)) * 2.10);
                float detailLuma = saturate(dot(detailBands, float3(0.3333, 0.3333, 0.3333)) * 1.65);
                float cloudTexture = saturate(textureLuma * 0.72 + detailLuma * 0.34);
                float bandBreakup = 0.78 + Hash11(floor((uv.y + hitNormal.x * 0.17) * 89.0)) * 0.22;
                
                float3 coldGas = float3(0.020, 0.070, 0.082);
                float3 deepTeal = float3(0.050, 0.220, 0.270);
                float3 iceCloud = float3(0.255, 0.690, 0.760);
                float3 stormCopper = float3(0.300, 0.185, 0.090);
                float stormEmission = AegirStormEmission();
                
                // Procedural mix driven by FBM and textures
                float proceduralDetail = FBM(noiseCoords * 3.0 + warp * 5.0);
                float combinedDetail = lerp(proceduralDetail, cloudTexture, _DiscTextureWeight * 0.4); // Limit flat texture dominance
                combinedDetail = smoothstep(0.2, 0.8, combinedDetail); // High contrast bands
                
                float3 proceduralGas = lerp(deepTeal, iceCloud, combinedDetail);
                
                // Giant storms (Red spots / copper vortices)
                float stormMask = FBM(noiseCoords * 2.5 - float2(flowPhase * 0.4, 0.0) + warp * 2.0);
                stormMask = smoothstep(0.60, 0.85, stormMask); // Isolate vortices
                float stormIntensity = stormMask * stormEmission * saturate(quality + 0.25) * 1.5;
                proceduralGas = lerp(proceduralGas, stormCopper, saturate(stormIntensity));
                proceduralGas *= _AegirExposure * bandBreakup;

                float ndotl = dot(hitNormal, lightDir);
                float day = saturate(ndotl * 0.92 + 0.08);
                float hardTerminator = smoothstep(-0.08, 0.18, ndotl);
                
                float viewFacing = saturate(dot(hitNormal, -rayDir));
                float rim = saturate(1.0 - viewFacing);
                
                float nightFill = saturate(0.28 + rim * 0.45);
                float3 color = lerp(coldGas * nightFill, proceduralGas * lerp(0.055, 1.0, hardTerminator) * (0.22 + day * 0.78), saturate(ndotl + 0.1));

                // Soften ring shadow slightly so it doesn't look like a bug
                float shadow = RingShadow(hitPoint, lightDir, center, ringNormal, ringInnerSq, ringOuterSq);
                color *= 1.0 - shadow * shadowStrength * 0.75;

                float rim2 = rim * rim;
                float rim4 = rim2 * rim2;
                float limbDarken = lerp(1.0, 0.58, saturate(pow(rim, 1.35)));
                float scatter = rim2 * 0.42 + rim4 * (0.35 + quality * 1.35);
                scatter *= saturate(ndotl * 0.65 + 0.35);
                color *= limbDarken;
                color += _AtmosphereTint.rgb * scatter;
                return color;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float quality = saturate(max(_H8GlobalQualityWeight, _H8AegirOrbitScalars.w));
                float3 rayDir = SafeUnit(input.rayOS);
                float3 center = _H8AegirPlanetCenterRadius.xyz;
                float radius = max(_H8AegirPlanetCenterRadius.w, 0.001);
                float3 lightDir = SafeUnit(_H8AegirSunDirection.xyz);
                float3 ringNormal = SafeUnit(_H8AegirRingPlaneInner.xyz);
                float ringInner = max(_H8AegirRingPlaneInner.w, radius + 0.01);
                float ringOuter = max(_H8AegirOrbitScalars.x, ringInner + 0.01);
                float ringInnerSq = ringInner * ringInner;
                float ringOuterSq = ringOuter * ringOuter;
                float shadowStrength = saturate(_H8AegirOrbitScalars.y);
                float flowSpeed = max(_H8AegirOrbitScalars.z, 0.0);
                float systemVisibility = saturate(1.0 - _H8AegirSunDirection.w);

                float3 starColor = StarField(rayDir, quality);
                float3 color = _VoidTint.rgb + starColor;
                float alpha = saturate(dot(starColor, float3(0.3333, 0.3333, 0.3333)) * 1.15);

                float ringT;
                float3 ringLocal;
                float ringRadiusSq;
                bool ringHit = RayRingPlane(rayDir, center, ringNormal, ringT, ringLocal, ringRadiusSq);
                float ringMask = ringHit ? HardRingMaskSq(ringRadiusSq, ringInnerSq, ringOuterSq) : 0.0;
                float3 ringColor = 0.0;
                float ringAlpha = 0.0;
                [branch]
                if (ringMask > 0.0)
                {
                    float ringPhase = frac(ringRadiusSq * 37.0);
                    float lanes = Hash11(floor(ringRadiusSq * 82.0));
                    float gap = step(0.15, ringPhase) * step(ringPhase, 0.94);
                    float lit = saturate(dot(ringNormal, lightDir) * 0.42 + 0.58);
                    ringColor = _RingTint.rgb * (0.35 + lanes * 0.65) * lit;
                    ringAlpha = ringMask * gap * (0.28 + quality * 0.42) * _RingOpacity * systemVisibility;
                }

                float planetT;
                float3 planetPoint;
                float3 planetNormal;
                bool planetHit = RaySphere(rayDir, center, radius, planetT, planetPoint, planetNormal);

                if (ringMask > 0.0 && (!planetHit || ringT < planetT))
                {
                    color = lerp(color, ringColor, ringAlpha);
                    alpha = max(alpha, ringAlpha);
                }

                if (planetHit)
                {
                    float3 planetColor = DrawAegir(rayDir, center, lightDir, ringNormal, quality, flowSpeed, ringInnerSq, ringOuterSq, shadowStrength, planetPoint, planetNormal);
                    color = lerp(color, planetColor, systemVisibility);
                    alpha = max(alpha, systemVisibility);
                }

                if (_ScreenAegirOpacity > 0.0001 && systemVisibility > 0.0001)
                    color = DrawScreenSpaceAegir(color, input.positionCS, quality, flowSpeed, systemVisibility, alpha);

                return float4(color, saturate(alpha));
            }
            ENDHLSL
        }
    }

    FallBack Off
}
