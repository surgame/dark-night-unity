#ifndef DN_WALL_DIFFUSE_PREPARE
#define DN_WALL_DIFFUSE_PREPARE
#include "WallDiffuseProfile.hlsl"
#include "WallDiffuseVisibility.hlsl"
StructuredBuffer<DNWallCandidate> _DNWallCandidates;
RWStructuredBuffer<DNWallProfile> _DNCandidateProfiles;
RWStructuredBuffer<float> _DNCandidateNodes;
float4 _DNWallCandidateCounts[16];

float2 DNWallIncident(int lamp,float2 hit,float2 normal)
{
    float2 origin=_DNLightPositions[lamp].xy, delta=hit-origin, forward=_DNLightDirections[lamp].xy;
    float d=length(delta), range=_DNLightPositions[lamp].z;
    if(d>=range || d>=_DNDiffuseSettings[lamp].z || _DNLightColors[lamp].w<=0 || !DNKnown(hit) ||
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
    float incident=beam*pow(saturate(1-d/range),1.15)*_DNLightColors[lamp].w*visible;
    float quality=incident*saturate(dot(-delta/max(d,.001),normal));
    return float2((int)_DNDiffuseOptions[lamp].w!=0?incident:quality,quality);
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
        float wallLength=length(axis), sum=0, qualitySum=0, distanceSum=0, matchedMean=0;
        int steps=clamp((int)ceil(wallLength/.125),32,128), first=steps,last=0;
        for(int j=0;j<=steps;j++)
        {
            float2 hit=lerp(wall.ends.xy,wall.ends.zw,(float)j/steps);
            if(DNWallIncident(lamp,hit,wall.normal.xy).y>1e-6) { first=min(first,j); last=max(last,j); }
        }
        if(last>=first)
        {
            // 一段物理墙的影区保留为零值；仅裁去首尾未受光区，不拆分阴影。
            first=max(0,first-1); last=min(steps,last+1);
            float2 start=lerp(wall.ends.xy,wall.ends.zw,(float)first/steps);
            float litLength=wallLength*(last-first)/steps;
            for(int j=0;j<33;j++)
            {
                float at=j/32.0;
                if(_DNDiffuseBenchmark==1)
                {
                    at=saturate((floor(j/8.0)*8+4)/32.0);
                }
                // 裁出受光范围后重新求真实入射，不插值粗采样的遮挡结果。
                float2 hit=start+axis/wallLength*(litLength*at);
                float2 value=DNWallIncident(lamp,hit,wall.normal.xy);
                _DNCandidateNodes[index*33+j]=value.x;
                float weight=(j==0 || j==32)?.5:1;
                sum+=value.x*weight; qualitySum+=value.y*weight;
                matchedMean+=value.x*value.y*weight;
                distanceSum+=distance(_DNLightPositions[lamp].xy,hit)*value.y*weight;
            }
            float distance=distanceSum/max(qualitySum,1e-6);
            float fade=1-smoothstep(_DNDiffuseSettings[lamp].y,_DNDiffuseSettings[lamp].z,distance);
            profile.frame=float4(start,axis/wallLength);
            profile.shape=float4(wall.normal.xy,litLength,_DNDiffuseSettings[lamp].w*fade);
            float mean=(int)_DNDiffuseOptions[lamp].w!=0?matchedMean/max(qualitySum,1e-6):sum/32;
            profile.stats=float4(mean,fade,qualitySum*litLength/32,0);
        }
    }
    _DNCandidateProfiles[index]=profile;
}

// 必须有共同物理端点；连接半径只容许受光足迹在该拐点附近连接，不跨门洞或平行薄墙。
int DNWallJoin(int lamp,int one,int other)
{
    if(_DNLightBounce[lamp].z<=0) return 0;
    int a=lamp*64+one,b=lamp*64+other;
    DNWallCandidate ca=_DNWallCandidates[a], cb=_DNWallCandidates[b];
    if(dot(ca.normal.xy,cb.normal.xy)<-.75) return 0;
    DNWallProfile pa=_DNCandidateProfiles[a],pb=_DNCandidateProfiles[b];
    for(int end=0;end<2;end++) for(int second=0;second<2;second++)
    {
        float2 joint=end==0?ca.ends.xy:ca.ends.zw, next=second==0?cb.ends.xy:cb.ends.zw;
        if(distance(joint,next)>.001) continue;
        float da=distance(joint,pa.frame.xy), ea=distance(joint,pa.frame.xy+pa.frame.zw*pa.shape.z);
        float db=min(distance(joint,pb.frame.xy),distance(joint,pb.frame.xy+pb.frame.zw*pb.shape.z));
        if(min(da,ea)>_DNLightBounce[lamp].z || db>_DNLightBounce[lamp].z) continue;
        float2 air=joint+normalize(ca.normal.xy+cb.normal.xy)*.006;
        if(!DNKnown(air) || DNSolid(air)) continue;
        return da<=ea?1:2;
    }
    return 0;
}

[numthreads(16,1,1)]
void SelectWallProfiles(uint3 id:SV_DispatchThreadID)
{
    int lamp=id.x, chosen[8],used[64];
    for(int chosenIndex=0;chosenIndex<8;chosenIndex++) chosen[chosenIndex]=-1;
    for(int usedIndex=0;usedIndex<64;usedIndex++) used[usedIndex]=0;
    int count=(int)_DNWallCandidateCounts[lamp].x;
    for(int group=0;group<2;group++) for(int slot=0;slot<4;slot++)
    {
        int best=-1; float quality=0;
        for(int i=0;i<count;i++)
        {
            if(used[i]!=0) continue;
            float score=_DNCandidateProfiles[lamp*64+i].stats.z;
            if(score<=quality) continue;
            bool connected=slot==0;
            [unroll] for(int previous=0;previous<4;previous++)
                if(previous<slot && chosen[group*4+previous]>=0 && DNWallJoin(lamp,i,chosen[group*4+previous])!=0) connected=true;
            if(connected) { quality=score; best=i; }
        }
        if(best<0) break;
        chosen[group*4+slot]=best; used[best]=1;
    }
    for(int outputSlot=0;outputSlot<8;outputSlot++)
    {
        int target=lamp*8+outputSlot, source=lamp*64+chosen[outputSlot];
        DNWallProfile selected=(DNWallProfile)0;
        if(chosen[outputSlot]>=0)
        {
            selected=_DNCandidateProfiles[source]; int flags=0;
            int groupStart=outputSlot&~3;
            for(int other=groupStart;other<groupStart+4;other++)
                if(other!=outputSlot && chosen[other]>=0) flags|=DNWallJoin(lamp,chosen[outputSlot],chosen[other]);
            selected.stats.w=flags;
        }
        _DNWallProfiles[target]=selected;
        for(int node=0;node<33;node++) _DNWallNodes[target*33+node]=chosen[outputSlot]<0?0:_DNCandidateNodes[source*33+node];
    }
}
#endif
