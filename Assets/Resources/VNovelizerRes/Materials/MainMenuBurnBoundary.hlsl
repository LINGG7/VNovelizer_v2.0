#ifndef VN_MAIN_MENU_BURN_BOUNDARY_INCLUDED
#define VN_MAIN_MENU_BURN_BOUNDARY_INCLUDED

// Keep the CPU ribbon profile in MainMenuBurnTransition identical.
float BurnBoundaryProfile(float angle, float seconds)
{
    return (sin(angle * 5.0 + 1.7 + seconds * 0.85) * 0.28
          + sin(angle * 9.0 - 0.8 - seconds * 1.15) * 0.22
          + sin(angle * 17.0 + 2.4 + seconds * 1.6) * 0.16
          + sin(angle * 29.0 - 1.1 - seconds * 2.1) * 0.10) * 0.72;
}

float BurnSignedDistance(float2 uv, float2 center, float aspect, float progress,
                         float radiusScale, float noiseStrength, float seconds)
{
    float2 p = uv - center;
    p.x *= max(aspect, 0.01);
    float angle = atan2(p.y, p.x);
    float cornerRadius = length(float2(max(center.x, 1.0 - center.x) * aspect,
                                        max(center.y, 1.0 - center.y)));
    float radius = max(progress * cornerRadius * radiusScale, 0.035);
    radius += BurnBoundaryProfile(angle, seconds) * noiseStrength * saturate(progress / 0.08);
    return length(p) - max(radius, 0.0);
}
#endif