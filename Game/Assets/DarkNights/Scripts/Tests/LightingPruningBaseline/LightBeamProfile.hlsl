#ifndef DN_LIGHT_BEAM_PROFILE
#define DN_LIGHT_BEAM_PROFILE
// 灯口是与方向垂直的有限线段；距离仍从真实灯口中心计算，保留圆弧末端。
float DNBeam(float2 delta, float2 forward, float cutoff, float halfWidth, float feather)
{
    if (cutoff < -.9) return 1;
    float axial = dot(delta, forward);
    if (axial < 0) return 0;
    float lateral = max(0, abs(dot(delta, float2(-forward.y, forward.x))) - halfWidth);
    float length = sqrt(axial * axial + lateral * lateral);
    float cosine = length <= .0001 ? 1 : axial / length;
    return smoothstep(cutoff, cutoff + max(.0001, feather), cosine);
}
#endif
