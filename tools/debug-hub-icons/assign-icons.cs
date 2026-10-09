// 有限的 Editor 资产接线；仅写清单中的 Definition.Icon，不加载运行对象或覆盖素材。
var path = System.IO.Path.GetFullPath("../tools/debug-hub-icons/icon-sources.json");
var mapping = Newtonsoft.Json.JsonConvert.DeserializeObject<System.Collections.Generic.Dictionary<string, string>>(System.IO.File.ReadAllText(path));
if (mapping.TryGetValue("item.jetpack", out var jetpackPath))
{
    UnityEditor.AssetDatabase.ImportAsset(jetpackPath);
    var importer = (UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(jetpackPath);
    if (importer.textureType != UnityEditor.TextureImporterType.Sprite || importer.filterMode != UnityEngine.FilterMode.Point ||
        importer.mipmapEnabled || importer.textureCompression != UnityEditor.TextureImporterCompression.Uncompressed)
    {
        importer.textureType = UnityEditor.TextureImporterType.Sprite;
        importer.spriteImportMode = UnityEditor.SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 32;
        importer.filterMode = UnityEngine.FilterMode.Point;
        importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
        importer.textureCompression = UnityEditor.TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
    }
}
var database = GameCore.Objects.Definition.ObjectDefinitionDatabase.Instance;
var prepared = new System.Collections.Generic.List<(GameCore.Objects.Definition.ObjectDefinition definition, UnityEngine.Sprite icon)>();
foreach (var pair in mapping)
{
    var definition = database.GetDefinitionByKey(pair.Key);
    var icon = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(pair.Value).OfType<UnityEngine.Sprite>().FirstOrDefault();
    if (definition == null || icon == null) throw new System.InvalidOperationException("Missing definition or sprite: " + pair.Key);
    prepared.Add((definition, icon));
}
foreach (var item in prepared)
{
    if (item.definition.Icon == item.icon) continue;
    UnityEditor.Undo.RecordObject(item.definition, "Dark Nights Debug Hub icons");
    item.definition.Icon = item.icon;
    UnityEditor.EditorUtility.SetDirty(item.definition);
}
UnityEditor.AssetDatabase.SaveAssets();
return new { assigned = prepared.Count, icons = prepared.Select(p => new { key = p.definition.Key, sprite = UnityEditor.AssetDatabase.GetAssetPath(p.icon) }).ToArray() };
