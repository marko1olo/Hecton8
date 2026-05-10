// ─────────────────────────────────────────────────────────────────
// SG_GasGiant_CelestialLighting.hlsl
// Custom Lighting Function dlya Shader Graph gazovogo giganta
// Podklyuchaetsya cherez Custom Function Node
// ─────────────────────────────────────────────────────────────────

#ifndef SG_GAS_GIANT_CELESTIAL_LIGHTING_INCLUDED
#define SG_GAS_GIANT_CELESTIAL_LIGHTING_INCLUDED

// ═══════════════════════════════════════════════════════
// MAIN CUSTOM LIGHTING
// ═══════════════════════════════════════════════════════
// Vhod: Normal (World), SunDir, Albedo, BacklitIntensity, Phase
// Vyhod: FinalColor

void GasGiantLighting_float(
    float3 WorldNormal,
    float3 SunDirection,       // _SunDirection global
    float3 Albedo,
    float  BacklitIntensity,
    float3 ViewDirection,
    out float3 FinalColor,
    out float  TerminatorMask,
    out float  FresnelGlow
)
{
    // Normalizatsiya
    float3 N = normalize(WorldNormal);
    float3 L = normalize(-SunDirection); // napravlenie K solntsu
    float3 V = normalize(ViewDirection);

    // ─── 1. BAZOVOE OSVESchENIE S MYaGKIM TERMINATOROM ───
    float NdotL = dot(N, L);

    // Myagkiy terminator: rasshiryaem perehodnuyu zonu
    // Vmesto clamp(NdotL, 0, 1) ispolzuem sigmoidu
    float terminatorWidth = 0.15; // shirina perehodnoy zony
    float softNdotL = saturate((NdotL + terminatorWidth) / (2.0 * terminatorWidth + 0.001));

    // Smoothstep dlya esche bolee myagkogo perehoda
    softNdotL = smoothstep(0.0, 1.0, softNdotL);

    // ─── 2. RELEEVSKOE RASSEYaNIE NA TERMINATORE ───
    // Oranzhevyy obodok na granitse dnya i nochi
    // Aktiven tolko v uzkoy zone terminatora
    float terminatorZone = 1.0 - abs(NdotL) / (terminatorWidth * 2.0 + 0.001);
    terminatorZone = saturate(terminatorZone);
    terminatorZone = terminatorZone * terminatorZone; // kvadratichnyy falloff

    // Tsvet Releevskogo rasseyaniya (teplyy oranzhevyy → krasnyy na zakate)
    float3 rayleighColor = float3(1.0, 0.5, 0.15);
    float3 rayleighContribution = rayleighColor * terminatorZone * 0.4;

    TerminatorMask = terminatorZone;

    // ─── 3. BACKLIT (Podsvetka tenevoy storony) ───
    // Tenevaya storona ne chernaya — podsvechena rasseyannym zvezdnym fonom
    float shadowSide = saturate(-NdotL); // 1 na polnostyu tenevoy storone
    float3 backlitColor = float3(0.03, 0.04, 0.08); // holodnyy sinevatyy
    float3 backlit = backlitColor * shadowSide * BacklitIntensity;

    // ─── 4. FRESNEL GLOW (Kontrovoy svet) ───
    float fresnel = 1.0 - saturate(dot(N, V));
    fresnel = pow(fresnel, 3.0); // uzkiy obodok

    // Fresnel glow aktiven tolko pri kontrovom svete
    float backlit_facing = saturate(dot(-V, L)); // kamera smotrit protiv solntsa
    FresnelGlow = fresnel * backlit_facing;

    // ─── 5. FINALNAYa KOMPOZITsIYa ───
    float3 daylight = Albedo * softNdotL;
    float3 terminator = rayleighContribution * Albedo;
    float3 rim = float3(0.6, 0.7, 1.0) * FresnelGlow * 0.5; // golubovatyy rim

    FinalColor = daylight + terminator + backlit + rim;
}


// ═══════════════════════════════════════════════════════
// DIFFERENTIAL ROTATION HELPER
// ═══════════════════════════════════════════════════════
// Rasschityvaet UV offset na osnove shiroty i vremeni

void DifferentialRotation_float(
    float2 UV,
    float  Time,
    float  EquatorialSpeed,
    float  PolarMultiplier,
    out float2 RotatedUV
)
{
    // Maska shiroty: UV.y = 0 (polyus) → 1 (ekvator) → 0 (polyus)
    // Dlya sfery: y=0.5 eto ekvator
    float latitude = abs(UV.y - 0.5) * 2.0; // 0 na ekvatore, 1 na polyusah
    float latitudeMask = 1.0 - latitude;

    // Skorost vrascheniya: ekvator bystree, polyusa medlennee
    float speed = lerp(EquatorialSpeed * PolarMultiplier, EquatorialSpeed, latitudeMask);

    // Dobavlyaem nelineynost dlya realistichnosti (kak u Yupitera)
    // cos²(latitude) priblizhenie
    float cosLat = latitudeMask; // uzhe ~cos(lat)
    speed *= cosLat;

    RotatedUV = float2(UV.x + Time * speed, UV.y);
}


// ═══════════════════════════════════════════════════════
// ATMOSPHERE FRESNEL (Mnogosloynyy)
// ═══════════════════════════════════════════════════════

void AtmosphereFresnel_float(
    float3 WorldNormal,
    float3 ViewDirection,
    float3 SunDirection,
    float3 AtmosphereColorInner,  // osnovnoy tsvet atmosfery
    float3 AtmosphereColorOuter,  // vneshniy obodok
    float  InnerPower,            // ~2.0
    float  OuterPower,            // ~5.0
    out float3 AtmosphereColor,
    out float  AtmosphereAlpha
)
{
    float3 N = normalize(WorldNormal);
    float3 V = normalize(ViewDirection);
    float3 L = normalize(-SunDirection);

    float NdotV = saturate(dot(N, V));
    float fresnel = 1.0 - NdotV;

    // Dva sloya Fresnel
    float innerFresnel = pow(fresnel, InnerPower);
    float outerFresnel = pow(fresnel, OuterPower);

    // Kontrovaya vspyshka: vneshniy sloy yarche pri backlit
    float backFacing = saturate(dot(-V, L));
    float backlitBoost = 1.0 + backFacing * 3.0; // do 4x pri kontrovom

    float3 inner = AtmosphereColorInner * innerFresnel;
    float3 outer = AtmosphereColorOuter * outerFresnel * backlitBoost;

    AtmosphereColor = inner + outer;
    AtmosphereAlpha = saturate(innerFresnel + outerFresnel * backlitBoost);
}

#endif // SG_GAS_GIANT_CELESTIAL_LIGHTING_INCLUDED