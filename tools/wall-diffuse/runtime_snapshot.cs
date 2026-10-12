// 真实 Bootstrap 相机和后端生命周期快照；仅写本任务证据，不保存游戏或作者场景。
var network=UnityEngine.Object.FindFirstObjectByType<DarkNights.Runtime.Network.SessionNetwork>();
var presentation=UnityEngine.Object.FindFirstObjectByType<DarkNights.Entry.HeroLightPresentation>();
var preview=UnityEngine.Object.FindFirstObjectByType<DarkNights.View.Terrain.TerrainPreview>();
var camera=preview.ViewCamera;
var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
var environment=(DarkNights.View.Lighting.EnvironmentLighting)typeof(DarkNights.Entry.HeroLightPresentation).GetField("field",flags).GetValue(presentation);
var backend=typeof(DarkNights.View.Lighting.EnvironmentLighting).GetField("backend",flags).GetValue(environment);
var diffuse=(backend as DarkNights.View.Lighting.ExplorationLightField)?.DiffuseResources;
var output=System.IO.Path.GetFullPath("../artifacts/wall-diffuse-20261012/runtime"); System.IO.Directory.CreateDirectory(output);
var key=presentation.Settings.Backend+"-epoch"+presentation.WorldEpoch+"-"+System.DateTime.UtcNow.ToString("HHmmss");
var target=new UnityEngine.RenderTexture(1280,720,24,UnityEngine.RenderTextureFormat.ARGB32);
var read=new UnityEngine.Texture2D(1280,720,UnityEngine.TextureFormat.RGBA32,false);
var oldTarget=camera.targetTexture; var previous=UnityEngine.RenderTexture.active;
try
{
    target.Create(); camera.targetTexture=target; camera.Render(); UnityEngine.RenderTexture.active=target;
    read.ReadPixels(new UnityEngine.Rect(0,0,1280,720),0,0); read.Apply();
    System.IO.File.WriteAllBytes(output+"/"+key+".png",read.EncodeToPNG());
    var report=new { utc=System.DateTime.UtcNow, ready=network.Client.Ready, hosting=network.Hosting, sources=presentation.SourceCount,
        epoch=presentation.WorldEpoch, backend=backend.GetType().Name, settings=UnityEngine.JsonUtility.ToJson(presentation.Settings),
        failed=(bool)typeof(DarkNights.Entry.HeroLightPresentation).GetField("failed",flags).GetValue(presentation),
        buildCount=diffuse?.BuildCount, candidates=diffuse?.CandidateCount, bufferBytes=diffuse?.BufferBytes, workerMs=diffuse?.WorkerMilliseconds,
        sourceRevision=preview.LightingSource.GeometryRevision, cameraPosition=new[]{camera.transform.position.x,camera.transform.position.y,camera.transform.position.z} };
    System.IO.File.WriteAllText(output+"/"+key+".json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));
    return report;
}
finally { camera.targetTexture=oldTarget; UnityEngine.RenderTexture.active=previous; target.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(read); }
