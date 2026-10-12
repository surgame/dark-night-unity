#ifndef DN_WALL_DIFFUSE_PREPARE
#define DN_WALL_DIFFUSE_PREPARE
#include "WallDiffuseProfile.hlsl"
#include "WallDiffuseVisibility.hlsl"
StructuredBuffer<DNWallCandidate> _DNWallCandidates;
RWStructuredBuffer<DNWallProfile> _DNCandidateProfiles;
RWStructuredBuffer<float> _DNCandidateNodes;
float4 _DNWallCandidateCounts[16];

float DNWallIncident(int lamp,float2 hit,float2 normal)
{
    float2 origin=_DNLightPositions[lamp].xy, delta=hit-origin, forward=_DNLightDirections[lamp].xy;
    float d=length(delta), range=_DNLightPositions[lamp].z;
    if(d>=range || d>=_DNDiffuseSettings.z || _DNLightColors[lamp].w<=0 || !DNKnown(hit) ||
        !DNSolid(hit-normal*.003) || DNSolid(hit+normal*.003)) return 0;
    float beam=DNBeam(delta,forward,_DNLightPositions[lamp].w,_DNLightApertures[lamp].x,_DNLightShadows[lamp].y);
    if(beam<=0) return 0;
    float softness=_DNLightShadows[lamp].x, aperture=_DNLightApertures[lamp].x;
    float2 perpendicular=float2(-forward.y,forward.x), target=hit+normal*.003;
    float visible=0;
    if(softness<=0)
    {
        float2 mouth=origin+perpendicular*clamp(dot(delta,perpendicular),-aperture,aperture);
        visible=DNExactVisibility(origin,mouth)*DNExactVisibility(mouth,target);
    }
    else for(int s=-2;s<=2;s++)
    {
        float2 mouth=origin+perpendicular*(s*.5)*max(aperture,softness);
        visible+=DNExactVisibility(origin,mouth)*DNExactVisibility(mouth,target)*(3-abs(s))/9;
    }
    return beam*pow(saturate(1-d/range),1.15)*_DNLightColors[lamp].w*visible*saturate(dot(-delta/max(d,.001),normal));
}

[numthreads(64,1,1)]
void PrepareWallProfiles(uint3 id:SV_DispatchThreadID)
{
    int lamp=id.y, candidate=id.x, index=lamp*64+candidate;
    DNWallProfile profile=(DNWallProfile)0;
    if(lamp<_DNLightCount && candidate<(int)_DNWallCandidateCounts[lamp].x)
    {
        DNWallCandidate wall=_DNWallCandidates[index];
        float2 axis=wall.ends.zw-wall.ends.xy;
        float wallLength=length(axis), first=32,last=0, sum=0, distanceSum=0;
        float incident[33];
        for(int j=0;j<33;j++)
        {
            float2 hit=lerp(wall.ends.xy,wall.ends.zw,j/32.0);
            incident[j]=DNWallIncident(lamp,hit,wall.normal.xy);
            if(incident[j]>1e-6) { first=min(first,j); last=max(last,j); }
        }
        if(last>=first && incident[(int)first]>0)
        {
            // 一段物理墙的影区保留为零值；仅裁去首尾未受光区，不拆分阴影。
            first=max(0,first-1); last=min(32,last+1);
            float2 start=lerp(wall.ends.xy,wall.ends.zw,first/32);
            float litLength=wallLength*(last-first)/32;
            for(int j=0;j<33;j++)
            {
                float at=lerp(first,last,j/32.0), value;
                if(_DNDiffuseBenchmark==1)
                {
                    at=lerp(first,last,(floor(j/8.0)*8+4)/32.0);
                    at=clamp(at,first,last);
                }
                int lo=min(31,(int)floor(at)); value=lerp(incident[lo],incident[lo+1],at-lo);
                _DNCandidateNodes[index*33+j]=value;
                float weight=(j==0 || j==32)?.5:1;
                sum+=value*weight; distanceSum+=distance(_DNLightPositions[lamp].xy,start+axis/wallLength*(litLength*j/32))*value*weight;
            }
            float distance=distanceSum/max(sum,1e-6);
            float fade=1-smoothstep(_DNDiffuseSettings.y,_DNDiffuseSettings.z,distance);
            profile.frame=float4(start,axis/wallLength);
            profile.shape=float4(wall.normal.xy,litLength,_DNDiffuseSettings.w*fade);
            profile.stats=float4(sum/32,fade,sum*litLength/32,0);
        }
    }
    _DNCandidateProfiles[index]=profile;
}

[numthreads(16,1,1)]
void SelectWallProfiles(uint3 id:SV_DispatchThreadID)
{
    int lamp=id.x, chosen[2]={-1,-1}; float quality[2]={0,0};
    for(int i=0;i<(int)_DNWallCandidateCounts[lamp].x;i++)
    {
        float score=_DNCandidateProfiles[lamp*64+i].stats.z;
        if(score>quality[0]) { quality[1]=quality[0]; chosen[1]=chosen[0]; quality[0]=score; chosen[0]=i; }
        else if(score>quality[1]) { quality[1]=score; chosen[1]=i; }
    }
    for(int slot=0;slot<2;slot++)
    {
        int target=lamp*2+slot, source=lamp*64+chosen[slot];
        DNWallProfile selected=(DNWallProfile)0;
        if(chosen[slot]>=0) selected=_DNCandidateProfiles[source];
        _DNWallProfiles[target]=selected;
        for(int j=0;j<33;j++) _DNWallNodes[target*33+j]=chosen[slot]<0?0:_DNCandidateNodes[source*33+j];
    }
}
#endif
