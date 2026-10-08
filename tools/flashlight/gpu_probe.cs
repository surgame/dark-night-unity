// Unity CLI eval snippet: bounded real-compute probe, not a full game screenshot.
var folder=System.IO.Path.GetFullPath("../artifacts/flashlight-lighting-20261008/gpu");
System.IO.Directory.CreateDirectory(folder);
var shader=UnityEngine.Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.ComputeShader>("Assets/DarkNights/Res/Shared/Lighting/ExplorationLighting.compute"));
var cells=new UnityEngine.Texture2D(16,16,UnityEngine.TextureFormat.RGBA32,false,true);
var pixels=new UnityEngine.Color32[256];
for(int y=0;y<16;y++) for(int x=0;x<16;x++) pixels[y*16+x]=new UnityEngine.Color32((byte)(x>=9&&x<=10&&y>=4||y>=13?1:0),0,0,255);
cells.SetPixels32(pixels);cells.Apply();
var light=new UnityEngine.RenderTexture(128,128,0,UnityEngine.RenderTextureFormat.ARGBHalf,UnityEngine.RenderTextureReadWrite.Linear){enableRandomWrite=true}; light.Create();
var direction=new UnityEngine.RenderTexture(128,128,0,UnityEngine.RenderTextureFormat.ARGBHalf,UnityEngine.RenderTextureReadWrite.Linear){enableRandomWrite=true};direction.Create();
var bounce=new UnityEngine.ComputeBuffer(16,16);
var positions=new UnityEngine.Vector4[16];var directions=new UnityEngine.Vector4[16];var colors=new UnityEngine.Vector4[16];var near=new UnityEngine.Vector4[16];
positions[0]=new UnityEngine.Vector4(5,7,14,UnityEngine.Mathf.Cos(UnityEngine.Mathf.PI/4));
directions[0]=new UnityEngine.Vector4(1,0,2.2f,.55f); colors[0]=new UnityEngine.Vector4(1,.75f,.43f,1.35f);near[0]=new UnityEngine.Vector4(4.5f,7.5f,0,0);
int resolve=shader.FindKernel("ResolveSources"),illuminate=shader.FindKernel("Illuminate");
shader.SetVector("_DNKnownBounds",new UnityEngine.Vector4(0,0,16,16));shader.SetVector("_DNLightRect",new UnityEngine.Vector4(0,0,16,16));
shader.SetVector("_DNTargetSize",new UnityEngine.Vector4(128,128,0,0));shader.SetInt("_DNLightCount",1);
shader.SetVectorArray("_DNLightPositions",positions);shader.SetVectorArray("_DNLightDirections",directions);shader.SetVectorArray("_DNLightColors",colors);shader.SetVectorArray("_DNLightNearOrigins",near);
shader.SetTexture(resolve,"_DNLightCells",cells);shader.SetTexture(illuminate,"_DNLightCells",cells);
shader.SetBuffer(resolve,"_DNBounceSources",bounce);shader.SetBuffer(illuminate,"_DNBounceSources",bounce);
shader.SetTexture(illuminate,"_DNLightOutput",light);shader.SetTexture(illuminate,"_DNDirectionOutput",direction);
var read=new UnityEngine.Texture2D(128,128,UnityEngine.TextureFormat.RGBAFloat,false,true);
var png=new UnityEngine.Texture2D(128,128,UnityEngine.TextureFormat.RGBA32,false,false);
var previous=UnityEngine.RenderTexture.active;
var results=new System.Collections.Generic.List<object>();
UnityEngine.Color[] hardPixels=null;
try
{
    foreach(var setting in new[] {("hard",0f,.375f), ("soft",.22f,.375f), ("zero-wall",.22f,0f)})
    {
        shader.SetVector("_DNShadowSettings",new UnityEngine.Vector4(setting.Item2,setting.Item3,.65f,.045f));shader.SetFloat("_DNBounceStrength",.12f);
        shader.Dispatch(resolve,1,1,1);shader.Dispatch(illuminate,16,16,1);
        UnityEngine.RenderTexture.active=light;read.ReadPixels(new UnityEngine.Rect(0,0,128,128),0,0,false);read.Apply(false,false);
        float front=read.GetPixel(64,56).r,wall=read.GetPixel(73,56).r,far=read.GetPixel(96,56).r,self=read.GetPixel(36,69).r;
        if(front<.1f||self<.01f||far>.0001f||(setting.Item3==0&&wall>.0001f)) throw new System.Exception("GPU lighting invariant failed: "+setting.Item1);
        var image=read.GetPixels();
        int changed=0;
        if(setting.Item1=="hard") hardPixels=(UnityEngine.Color[])image.Clone();
        else if(setting.Item1=="soft")
        {
            for(int i=0;i<image.Length;i++) if(UnityEngine.Mathf.Abs(image[i].r-hardPixels[i].r)>.001f) changed++;
            if(changed<8) throw new System.Exception("Soft shadow did not change corner coverage.");
        }
        for(int y=0;y<128;y++) for(int x=0;x<128;x++)
        {
            bool rock=x/8>=9&&x/8<=10||y/8>=13;
            var albedo=rock?new UnityEngine.Color(.14f,.11f,.085f):new UnityEngine.Color(.085f,.075f,.058f);
            var energy=image[y*128+x];
            var color=new UnityEngine.Color(albedo.r*(.1f+energy.r),albedo.g*(.1f+energy.g),albedo.b*(.1f+energy.b),1).gamma;
            image[y*128+x]=color;
        }
        png.SetPixels(image);png.Apply();System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder,setting.Item1+".png"),png.EncodeToPNG());
        results.Add(new {mode=setting.Item1,front,wall,behind_thick_wall=far,near_body=self,wall_depth=setting.Item3,changed_corner_pixels=changed});
    }
    var report=new {passed=true,scope="Actual Unity compute shader; small geometric probe, not a game screenshot",results};
    System.IO.File.WriteAllText(System.IO.Path.Combine(folder,"result.json"),Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));
    return report;
}
finally
{
    UnityEngine.RenderTexture.active=previous;bounce.Dispose();light.Release();direction.Release();
    foreach(var value in new UnityEngine.Object[]{shader,cells,light,direction,read,png}) UnityEngine.Object.DestroyImmediate(value);
}
