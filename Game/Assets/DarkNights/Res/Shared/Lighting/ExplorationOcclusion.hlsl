#ifndef DN_EXPLORATION_OCCLUSION
#define DN_EXPLORATION_OCCLUSION
Texture2D<float4> _DNLightCells;
float4 _DNKnownBounds;
float4 _DNShadowSettings; // emitter radius, wall depth, wall strength, cone feather

bool DNKnown(float2 p)
{
    return all(p >= _DNKnownBounds.xy) && all(p < _DNKnownBounds.xy + _DNKnownBounds.zw);
}
bool DNSolid(float2 p)
{
    if (!DNKnown(p)) return true;
    float4 cell = _DNLightCells.Load(int3(int2(floor(p)), 0));
    if (cell.a < .5) return true;
    if (cell.r < .001) return false;
    int shape = (int)round(cell.g * 255);
    if (shape == 0) return true;
    float x = frac(p.x), y = 1 - frac(p.y);
    bool ceiling = shape >= 7;
    int s = ceiling ? shape - 6 : shape;
    float edge = s == 1 ? x : s == 2 ? 1-x : s == 3 ? x*.5 :
        s == 4 ? .5+x*.5 : s == 5 ? 1-x*.5 : .5-x*.5;
    return ceiling ? y >= edge : y <= edge;
}
float DNVisibility(float2 origin, float2 target, float wallDepth)
{
    if (!DNKnown(origin) || !DNKnown(target) || DNSolid(origin)) return 0;
    bool targetSolid = DNSolid(target);
    float length = distance(origin, target);
    int steps = min(256, max(1, (int)ceil(length * 8)));
    float first = -1;
    for (int n = 1; n <= steps; n++)
    {
        float t = (float)n / steps;
        float2 p = lerp(origin, target, t);
        if (!DNKnown(p) || _DNLightCells.Load(int3(int2(floor(p)),0)).a < .5) return 0;
        if (DNSolid(p))
        {
            if (!targetSolid || wallDepth <= 0) return 0;
            if (first < 0) first = max(0, length * t - .125);
            if (length - first > wallDepth) return 0;
        }
    }
    return first < 0 ? 1 : min(_DNShadowSettings.z, saturate(1 - (length-first)/max(wallDepth,.001)));
}
#endif
