// Unity CLI eval_file：只制作两个原生 URP 模板，不编译游戏源码，不启动 Play。
var folder = "Assets/Temp/LightingBackendTemplates20261009";
if (UnityEditor.AssetDatabase.IsValidFolder(folder)) throw new System.InvalidOperationException("模板暂存目录已存在，拒绝覆盖。");
if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/Temp")) UnityEditor.AssetDatabase.CreateFolder("Assets", "Temp");
UnityEditor.AssetDatabase.CreateFolder("Assets/Temp", "LightingBackendTemplates20261009");
var preview = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
var roots = new System.Collections.Generic.List<UnityEngine.GameObject>();
var reports = new System.Collections.Generic.List<object>();
try
{
    var lampRoot = new UnityEngine.GameObject("UrpEnvironmentLight"); lampRoot.SetActive(false); roots.Add(lampRoot);
    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(lampRoot, preview);
    var lamp = lampRoot.AddComponent<UnityEngine.Rendering.Universal.Light2D>();
    lamp.lightType = UnityEngine.Rendering.Universal.Light2D.LightType.Sprite;
    lamp.blendStyleIndex = 0; lamp.shadowsEnabled = true;
    var layers = new int[UnityEngine.SortingLayer.layers.Length];
    for (int n = 0; n < layers.Length; n++) layers[n] = UnityEngine.SortingLayer.layers[n].id;
    lamp.targetSortingLayers = layers;
    var lampData = new UnityEditor.SerializedObject(lamp);
    lampData.FindProperty("m_NormalMapQuality").intValue = 1;
    lampData.FindProperty("m_NormalMapDistance").floatValue = 3;
    lampData.ApplyModifiedPropertiesWithoutUndo();
    var lampPrefab = UnityEditor.PrefabUtility.SaveAsPrefabAsset(lampRoot, folder + "/UrpEnvironmentLight.prefab");
    var shadowRoot = new UnityEngine.GameObject("UrpTerrainShadow"); shadowRoot.layer = 2; roots.Add(shadowRoot);
    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(shadowRoot, preview);
    var collider = shadowRoot.AddComponent<UnityEngine.PolygonCollider2D>();
    collider.isTrigger = true; collider.excludeLayers = ~0; collider.callbackLayers = 0; collider.pathCount = 0;
    var caster = shadowRoot.AddComponent<UnityEngine.Rendering.Universal.ShadowCaster2D>(); caster.selfShadows = false;
    var shadowData = new UnityEditor.SerializedObject(caster);
    var targets = shadowData.FindProperty("m_ApplyToSortingLayers"); targets.arraySize = layers.Length;
    for (int n = 0; n < layers.Length; n++) targets.GetArrayElementAtIndex(n).intValue = layers[n];
    shadowData.ApplyModifiedPropertiesWithoutUndo();
    if (shadowData.FindProperty("m_ShadowShape2DProvider").managedReferenceValue == null)
        throw new System.InvalidOperationException("原生 Collider 阴影 provider 未初始化，不能保存回退模板。");
    shadowRoot.SetActive(false);
    var shadowPrefab = UnityEditor.PrefabUtility.SaveAsPrefabAsset(shadowRoot, folder + "/UrpTerrainShadow.prefab");
    foreach (var item in new[] { lampPrefab, shadowPrefab })
    {
        var component = item.name == "UrpEnvironmentLight" ? (UnityEngine.Component)item.GetComponent<UnityEngine.Rendering.Universal.Light2D>() : item.GetComponent<UnityEngine.Rendering.Universal.ShadowCaster2D>();
        string guid; long fileId;
        if (!UnityEditor.AssetDatabase.TryGetGUIDAndLocalFileIdentifier(component, out guid, out fileId)) throw new System.Exception("模板引用身份缺失。");
        var path = UnityEditor.AssetDatabase.GetAssetPath(item);
        reports.Add(new { path, guid, fileId, bytes = new System.IO.FileInfo(path).Length });
    }
    UnityEditor.AssetDatabase.SaveAssets();
    return new { templates = reports, playing = UnityEditor.EditorApplication.isPlaying, scope = "Native template authoring only; no game compilation or Play" };
}
finally
{
    foreach (var root in roots) UnityEngine.Object.DestroyImmediate(root);
    UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(preview);
}
