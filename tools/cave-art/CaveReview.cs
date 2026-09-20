using System;
using System.IO;
using System.Threading.Tasks;
using DarkNights.Entry.Terrain;
using DarkNights.Runtime.Terrain;
using DarkNights.Core.Config.Terrain;
using UnityEngine;

/// <summary>仅由 Editor run_script 显式运行的批量截图与落地探针；不进入 Player，不把传送落地当作全路线通行。</summary>
public static class CaveReview
{
    public static async Task<string> Run()
    {
        string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../artifacts/cave-workshop"));
        var b = UnityEngine.Object.FindFirstObjectByType<TerrainDebugBootstrap>();
        var input = UnityEngine.Object.FindFirstObjectByType<CaveWorkshopInput>();
        if (b == null || input == null) throw new InvalidOperationException("Open and play cave scene first.");
        input.Walking = false; Application.runInBackground = true;
        int groundChecks = 0;
        for (int seed = 0; seed < 8; seed++)
        {
            int generation = b.Generation;
            b.Settings.Seed = seed == 0 ? "CAVE-EXPLORATION-01" : "CAVE-REVIEW-" + seed;
            b.RequestRegenerate(); float deadline = Time.realtimeSinceStartup + 45;
            while (b.Generation == generation)
            {
                if (b.LastError != null || Time.realtimeSinceStartup > deadline) throw new InvalidOperationException(b.LastError ?? "Generation timeout");
                await Task.Yield();
            }
            for (int room = 0; room < b.Blueprint.Rooms.Count; room++)
            {
                var r = b.Blueprint.Rooms[room]; b.Workshop.Teleport(r.X, -r.Y);
                for (int frame = 0; frame < 600; frame++) b.Workshop.Tick(0,false,false,1f/60);
                if (!b.Workshop.Grounded) throw new InvalidOperationException("No landing: " + seed + "/" + room);
                float x = b.Workshop.X * 16, y = PlayableTerrain.OriginY + b.Workshop.Y * 16;
                if (TerrainHeroMotion.Solid(b.Workshop.Map,x,y+10)) throw new InvalidOperationException("Body inside rock");
                groundChecks++;
            }
            b.Flyer.CameraDistance=100; b.Flyer.Teleport(new Vector2(160,-96));
            await Settle(b);
            Capture(b.Flyer.ViewCamera,Path.Combine(output,"overview-"+seed+".png"),1600,1000);
            b.VisitRoom(seed % b.Blueprint.Rooms.Count);
            b.Flyer.CameraDistance=10;
            for (int frame=0; frame<600; frame++) b.Workshop.Tick(0,false,false,1f/60);
            b.Flyer.Teleport(new Vector2(b.Workshop.X,b.Workshop.Y)); await Settle(b);
            Capture(b.Flyer.ViewCamera,Path.Combine(output,"near-"+seed+".png"),1600,900);
            File.WriteAllText(Path.Combine(output,"visual-status.json"),"{\"seeds\":"+(seed+1)+",\"landingChecks\":"+groundChecks+",\"complete\":false}");
        }
        b.Settings.Seed="CAVE-EXPLORATION-01";b.RequestRegenerate(); input.Walking=true;
        File.WriteAllText(Path.Combine(output,"visual-status.json"),"{\"seeds\":8,\"landingChecks\":"+groundChecks+",\"complete\":true}");
        return "8 seeds captured; " + groundChecks + " authority landings checked";
    }
    private static async Task Settle(TerrainDebugBootstrap b)
    {
        await Task.Delay(1500); float deadline=Time.realtimeSinceStartup+30;
        while (!b.Preview.Ready || b.Preview.RefreshingReplica)
        { if(Time.realtimeSinceStartup>deadline)throw new Exception("Page visibility timeout"); await Task.Delay(40); }
    }
    private static void Capture(Camera camera,string path,int width,int height)
    {
        var previous=camera.targetTexture;var active=RenderTexture.active;
        var rt=RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
        var image=new Texture2D(width,height,TextureFormat.RGB24,false);
        try
        {
            camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
            image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());
        }
        finally {camera.targetTexture=previous;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.Destroy(image);}
    }
}
