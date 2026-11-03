var project = System.IO.Path.GetFullPath(UnityEngine.Application.dataPath+"/../..");
var output = System.IO.Path.Combine(project,"artifacts/vault-svg-styles-20261102");
var manifest = Newtonsoft.Json.Linq.JObject.Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(output,"design-inputs.json")));
var variants = (Newtonsoft.Json.Linq.JArray)manifest["profiles"];
if (UnityEditor.EditorApplication.isPlaying || UnityEditor.EditorApplication.isCompiling ||
    UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
    throw new System.InvalidOperationException("需要保存且空闲的编辑态场景。");
string assetRoot = DarkNights.Editor.Terrain.WhiteboxModuleBaker.AssetRoot+"/VaultStyles";
foreach (Newtonsoft.Json.Linq.JObject variant in variants)
{
    string directory = assetRoot+"/"+(string)variant["folder"];
    if (UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(directory+"/"+(string)variant["id"]+".prefab") != null)
        throw new System.InvalidOperationException("已存在作者Prefab，拒绝初始化覆盖。");
    foreach (string layer in new[] {"Terrain","Backdrop"})
        if (UnityEditor.AssetImporter.GetAtPath(directory+"/"+layer+".png") == null)
            throw new System.InvalidOperationException("PNG仍未完成首次导入，请等待同一Editor完成导入。");
}
var shader = UnityEngine.Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
var material = new UnityEngine.Material(shader) { name="VaultSvgUnlit" };
material.SetColor("_Color",UnityEngine.Color.white);
UnityEditor.AssetDatabase.CreateAsset(material,assetRoot+"/VaultSvgUnlit.mat");
var results = new System.Collections.Generic.List<object>();
foreach (Newtonsoft.Json.Linq.JObject variant in variants)
{
    string folder=(string)variant["folder"],id=(string)variant["id"],title=(string)variant["name"];
    string directory=assetRoot+"/"+folder;
    var sprites = new System.Collections.Generic.List<UnityEngine.Sprite>();
    foreach (string layer in new[] {"Terrain","Backdrop"})
    {
        string png=directory+"/"+layer+".png";
        var importer=(UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(png);
        importer.textureType=UnityEditor.TextureImporterType.Sprite;
        importer.spriteImportMode=UnityEditor.SpriteImportMode.Single;
        importer.spritePixelsPerUnit=125;
        importer.sRGBTexture=true; importer.alphaIsTransparency=true; importer.mipmapEnabled=false;
        importer.alphaSource=UnityEditor.TextureImporterAlphaSource.FromInput;
        importer.filterMode=UnityEngine.FilterMode.Point;
        importer.wrapMode=UnityEngine.TextureWrapMode.Clamp;
        importer.textureCompression=UnityEditor.TextureImporterCompression.Uncompressed;
        importer.maxTextureSize=2048; importer.npotScale=UnityEditor.TextureImporterNPOTScale.None;
        var settings=new UnityEditor.TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType=UnityEngine.SpriteMeshType.FullRect;
        settings.spriteAlignment=0;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
        var sprite=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Sprite>(png);
        if(sprite==null || UnityEngine.Mathf.Abs(sprite.bounds.size.x-15.36f)>.001f ||
            UnityEngine.Mathf.Abs(sprite.bounds.size.y-9.6f)>.001f)
            throw new System.InvalidOperationException("PNG尺寸、Sprite或像素密度不符："+png);
        sprites.Add(sprite);
    }
    string scenePath=DarkNights.Editor.Terrain.TerrainScenePaths.Workbenches+"/"+(string)variant["scene"]+".unity";
    if(UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>(scenePath)!=null)
        throw new System.InvalidOperationException("目标场景已存在，不覆盖。");
    if(!UnityEditor.AssetDatabase.CopyAsset(DarkNights.Editor.Terrain.TerrainScenePaths.EntranceVault,scenePath))
        throw new System.InvalidOperationException("原生场景复制失败。");
    var scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);
    var roots=scene.GetRootGameObjects();
    var terrain=roots.Single(r=>r.name=="01_Terrain");
    var entry=roots.SelectMany(r=>r.GetComponentsInChildren<DarkNights.Entry.Terrain.WhiteboxEntry>(true)).Single();
    foreach(var child in Enumerable.Range(0,terrain.transform.childCount).Select(i=>terrain.transform.GetChild(i).gameObject).ToArray())
        UnityEngine.Object.DestroyImmediate(child);
    var basePrefab=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(DarkNights.Editor.Terrain.WhiteboxModuleBaker.AssetRoot+"/Vault/M01-Vault.prefab");
    var root=(UnityEngine.GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(basePrefab,terrain.transform);
    UnityEditor.PrefabUtility.UnpackPrefabInstance(root,UnityEditor.PrefabUnpackMode.Completely,UnityEditor.InteractionMode.AutomatedAction);
    root.name=id+" · "+title;
    var module=root.GetComponent<DarkNights.View.Terrain.WhiteboxTerrainAuthoring>();
    module.ModuleId=id; module.DisplayName=title;
    module.Floor=((Newtonsoft.Json.Linq.JArray)variant["floor"]).Select(p=>new UnityEngine.Vector2((float)p[0],(float)p[1])).ToArray();
    module.Ceiling=((Newtonsoft.Json.Linq.JArray)variant["ceiling"]).Select(p=>new UnityEngine.Vector2((float)p[0],(float)p[1])).ToArray();
    DarkNights.Editor.Terrain.WhiteboxModuleBaker.Bake(module,directory);
    for(int layer=0;layer<2;layer++)
    {
        var go=new UnityEngine.GameObject(layer==0?"SVG地形前景":"SVG洞内后壁",typeof(UnityEngine.SpriteRenderer));
        go.transform.SetParent(root.transform,false);
        go.transform.localPosition=new UnityEngine.Vector3(7.6f,0,layer==0?-.04f:1);
        var renderer=go.GetComponent<UnityEngine.SpriteRenderer>();
        renderer.sprite=sprites[layer]; renderer.sharedMaterial=material;
        renderer.sortingOrder=layer==0?2:-40;
        if(layer==0) module.TerrainArtwork=renderer; else module.BackgroundArtwork=renderer;
    }
    module.ArtworkFingerprint=module.BakedFingerprint;
    module.TerrainMesh.GetComponent<UnityEngine.MeshRenderer>().enabled=false;
    module.BackgroundMesh.GetComponent<UnityEngine.MeshRenderer>().enabled=false;
    module.FloorLine.enabled=false; module.CeilingLine.enabled=false;
    string prefabPath=directory+"/"+id+".prefab";
    UnityEditor.PrefabUtility.SaveAsPrefabAssetAndConnect(root,prefabPath,UnityEditor.InteractionMode.AutomatedAction);
    entry.TerrainModule=module; entry.Ground=null;
    entry.HeroPlacement.transform.position=new UnityEngine.Vector3(2,0,0);
    entry.ViewCamera.transform.position=new UnityEngine.Vector3(7.64f,.1f,-10);
    entry.ViewCamera.orthographicSize=4.6f;
    UnityEngine.Color sky;
    UnityEngine.ColorUtility.TryParseHtmlString((string)variant["palette"]["sky"],out sky);
    entry.ViewCamera.backgroundColor=sky;
    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
    if(!UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene))
        throw new System.InvalidOperationException("场景保存失败。");
    results.Add(new {id,title,scene=scenePath,prefab=prefabPath,fingerprint=module.BakedFingerprint,
        artworkFingerprint=module.ArtworkFingerprint,vertices=module.TerrainMesh.sharedMesh.vertexCount});
}
UnityEditor.AssetDatabase.SaveAssets();
UnityEditor.SceneManagement.EditorSceneManager.OpenScene(DarkNights.Editor.Terrain.TerrainScenePaths.VaultStrata);
if(UnityEditor.SceneView.lastActiveSceneView!=null)
{
    var view=UnityEditor.SceneView.lastActiveSceneView; view.in2DMode=true;
    view.LookAt(new UnityEngine.Vector3(7.64f,-.4f,0),UnityEngine.Quaternion.identity,8.5f,true,true);
}
string result=Newtonsoft.Json.JsonConvert.SerializeObject(new {completed=true,results,playing=UnityEditor.EditorApplication.isPlaying},Newtonsoft.Json.Formatting.Indented);
System.IO.File.WriteAllText(System.IO.Path.Combine(output,"creation-result.json"),result,new System.Text.UTF8Encoding(false));
return result;
