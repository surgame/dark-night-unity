// 旧版真实 Compute Shader 离屏基线；不改场景、作者配置或存档。
var output = System.IO.Path.GetFullPath("../artifacts/wall-diffuse-20261012/baseline");
var shader = UnityEngine.Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.ComputeShader>("Assets/DarkNights/Res/Shared/Lighting/ExplorationLighting.compute"));
var cells = new UnityEngine.Texture2D(24,24,UnityEngine.TextureFormat.RGBA32,false,true);
var light = new UnityEngine.RenderTexture(192,192,0,UnityEngine.RenderTextureFormat.ARGBFloat) { enableRandomWrite=true };
var direction = new UnityEngine.RenderTexture(192,192,0,UnityEngine.RenderTextureFormat.ARGBFloat) { enableRandomWrite=true };
var bounce = new UnityEngine.ComputeBuffer(16,16);
var read = new UnityEngine.Texture2D(192,192,UnityEngine.TextureFormat.RGBAFloat,false,true);
light.Create(); direction.Create();
try
{
    foreach(var shape in new[] { "flat", "slope", "door" })
    {
        for(int y=0;y<24;y++) for(int x=0;x<24;x++)
        {
            bool solid=x>=10;
            if(shape=="door" && y>=10 && y<12) solid=false;
            int slope=shape=="slope" && x==10 ? 7 : 0;
            cells.SetPixel(x,y,new UnityEngine.Color(solid?1:0,slope/255f,0,1));
        }
        cells.Apply();
        foreach(int count in new[] {1,2,4})
        {
            var positions=new UnityEngine.Vector4[16]; var directions=new UnityEngine.Vector4[16];
            var colors=new UnityEngine.Vector4[16]; var mouths=new UnityEngine.Vector4[16]; var shadows=new UnityEngine.Vector4[16];
            for(int n=0;n<count;n++)
            {
                positions[n]=new UnityEngine.Vector4(7,8+n*2,14,.7071068f);
                directions[n]=new UnityEngine.Vector4(1,0,0,0); colors[n]=new UnityEngine.Vector4(1,.75f,.45f,1.35f);
                mouths[n]=new UnityEngine.Vector4(.1875f,0,0,0); shadows[n]=new UnityEngine.Vector4(.18f,.045f,0,0);
            }
            shader.SetVector("_DNKnownBounds",new UnityEngine.Vector4(0,0,24,24));
            shader.SetVector("_DNLightRect",new UnityEngine.Vector4(0,0,24,24)); shader.SetVector("_DNTargetSize",new UnityEngine.Vector4(192,192,0,0));
            shader.SetVector("_DNShadowSettings",new UnityEngine.Vector4(0,.375f,.65f,0)); shader.SetInt("_DNLightCount",count); shader.SetFloat("_DNBounceStrength",.12f);
            shader.SetVectorArray("_DNLightPositions",positions); shader.SetVectorArray("_DNLightDirections",directions); shader.SetVectorArray("_DNLightColors",colors);
            shader.SetVectorArray("_DNLightNearOrigins",positions); shader.SetVectorArray("_DNLightApertures",mouths); shader.SetVectorArray("_DNLightShadows",shadows);
            int resolve=shader.FindKernel("ResolveSources"), illuminate=shader.FindKernel("Illuminate");
            shader.SetTexture(resolve,"_DNLightCells",cells); shader.SetBuffer(resolve,"_DNBounceSources",bounce);
            shader.SetTexture(illuminate,"_DNLightCells",cells); shader.SetBuffer(illuminate,"_DNBounceSources",bounce);
            shader.SetTexture(illuminate,"_DNLightOutput",light); shader.SetTexture(illuminate,"_DNDirectionOutput",direction);
            shader.Dispatch(resolve,1,1,1); shader.Dispatch(illuminate,24,24,1);
            var previous=UnityEngine.RenderTexture.active; UnityEngine.RenderTexture.active=light;
            read.ReadPixels(new UnityEngine.Rect(0,0,192,192),0,0); read.Apply(); UnityEngine.RenderTexture.active=previous;
            System.IO.File.WriteAllBytes(output+"/legacy-"+shape+"-"+count+".exr",read.EncodeToEXR());
            var png=new UnityEngine.Texture2D(192,192,UnityEngine.TextureFormat.RGBA32,false);
            png.SetPixels(read.GetPixels()); png.Apply(); System.IO.File.WriteAllBytes(output+"/legacy-"+shape+"-"+count+".png",png.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(png);
        }
    }
    return "9 个旧版真实 GPU 基线已保存（平墙／坡面／门洞 × 1／2／4灯）。";
}
finally { bounce.Dispose(); light.Release(); direction.Release(); foreach(var obj in new UnityEngine.Object[] {shader,cells,light,direction,read}) UnityEngine.Object.DestroyImmediate(obj); }
