// ─────────────────────────────────────────────────────────────────
// SG_GasGiant_CelestialLighting.hlsl
// Custom Lighting Function для Shader Graph газового гиганта
// Подключается через Custom Function Node
// ─────────────────────────────────────────────────────────────────

#ifndef SG_GAS_GIANT_CELESTIAL_LIGHTING_INCLUDED
#define SG_GAS_GIANT_CELESTIAL_LIGHTING_INCLUDED

// ═══════════════════════════════════════════════════════
// MAIN CUSTOM LIGHTING
// ═══════════════════════════════════════════════════════
// Вход: Normal (World), SunDir, Albedo, BacklitIntensity, Phase
// Выход: FinalColor

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
    // Нормализация
    float3 N = normalize(WorldNormal);
    float3 L = normalize(-SunDirection); // направление К солнцу
    float3 V = normalize(ViewDirection);

    // ─── 1. БАЗОВОЕ ОСВЕЩЕНИЕ С МЯГКИМ ТЕРМИНАТОРОМ ───
    float NdotL = dot(N, L);

    // Мягкий терминатор: расширяем переходную зону
    // Вместо clamp(NdotL, 0, 1) используем сигмоиду
    float terminatorWidth = 0.15; // ширина переходной зоны
    float softNdotL = saturate((NdotL + terminatorWidth) / (2.0 * terminatorWidth + 0.001));

    // Smoothstep для ещё более мягкого перехода
    softNdotL = smoothstep(0.0, 1.0, softNdotL);

    // ─── 2. РЭЛЕЕВСКОЕ РАССЕЯНИЕ НА ТЕРМИНАТОРЕ ───
    // Оранжевый ободок на границе дня и ночи
    // Активен только в узкой зоне терминатора
    float terminatorZone = 1.0 - abs(NdotL) / (terminatorWidth * 2.0 + 0.001);
    terminatorZone = saturate(terminatorZone);
    terminatorZone = terminatorZone * terminatorZone; // квадратичный falloff

    // Цвет Рэлеевского рассеяния (тёплый оранжевый → красный на закате)
    float3 rayleighColor = float3(1.0, 0.5, 0.15);
    float3 rayleighContribution = rayleighColor * terminatorZone * 0.4;

    TerminatorMask = terminatorZone;

    // ─── 3. BACKLIT (Подсветка теневой стороны) ───
    // Теневая сторона не чёрная — подсвечена рассеянным звёздным фоном
    float shadowSide = saturate(-NdotL); // 1 на полностью теневой стороне
    float3 backlitColor = float3(0.03, 0.04, 0.08); // холодный синеватый
    float3 backlit = backlitColor * shadowSide * BacklitIntensity;

    // ─── 4. FRESNEL GLOW (Контровой свет) ───
    float fresnel = 1.0 - saturate(dot(N, V));
    fresnel = pow(fresnel, 3.0); // узкий ободок

    // Fresnel glow активен только при контровом свете
    float backlit_facing = saturate(dot(-V, L)); // камера смотрит против солнца
    FresnelGlow = fresnel * backlit_facing;

    // ─── 5. ФИНАЛЬНАЯ КОМПОЗИЦИЯ ───
    float3 daylight = Albedo * softNdotL;
    float3 terminator = rayleighContribution * Albedo;
    float3 rim = float3(0.6, 0.7, 1.0) * FresnelGlow * 0.5; // голубоватый rim

    FinalColor = daylight + terminator + backlit + rim;
}


// ═══════════════════════════════════════════════════════
// DIFFERENTIAL ROTATION HELPER
// ═══════════════════════════════════════════════════════
// Рассчитывает UV offset на основе широты и времени

void DifferentialRotation_float(
    float2 UV,
    float  Time,
    float  EquatorialSpeed,
    float  PolarMultiplier,
    out float2 RotatedUV
)
{
    // Маска широты: UV.y = 0 (полюс) → 1 (экватор) → 0 (полюс)
    // Для сферы: y=0.5 это экватор
    float latitude = abs(UV.y - 0.5) * 2.0; // 0 на экваторе, 1 на полюсах
    float latitudeMask = 1.0 - latitude;

    // Скорость вращения: экватор быстрее, полюса медленнее
    float speed = lerp(EquatorialSpeed * PolarMultiplier, EquatorialSpeed, latitudeMask);

    // Добавляем нелинейность для реалистичности (как у Юпитера)
    // cos²(latitude) приближение
    float cosLat = latitudeMask; // уже ~cos(lat)
    speed *= cosLat;

    RotatedUV = float2(UV.x + Time * speed, UV.y);
}


// ═══════════════════════════════════════════════════════
// ATMOSPHERE FRESNEL (Многослойный)
// ═══════════════════════════════════════════════════════

void AtmosphereFresnel_float(
    float3 WorldNormal,
    float3 ViewDirection,
    float3 SunDirection,
    float3 AtmosphereColorInner,  // основной цвет атмосферы
    float3 AtmosphereColorOuter,  // внешний ободок
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

    // Два слоя Fresnel
    float innerFresnel = pow(fresnel, InnerPower);
    float outerFresnel = pow(fresnel, OuterPower);

    // Контровая вспышка: внешний слой ярче при backlit
    float backFacing = saturate(dot(-V, L));
    float backlitBoost = 1.0 + backFacing * 3.0; // до 4x при контровом

    float3 inner = AtmosphereColorInner * innerFresnel;
    float3 outer = AtmosphereColorOuter * outerFresnel * backlitBoost;

    AtmosphereColor = inner + outer;
    AtmosphereAlpha = saturate(innerFresnel + outerFresnel * backlitBoost);
}

#endif // SG_GAS_GIANT_CELESTIAL_LIGHTING_INCLUDED