using System.IO;
using DarkNights.Core.Config.Terrain;
using DarkNights.Runtime.Terrain;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace DarkNights.Editor.Terrain
{
    /// <summary>.minemap 的确定性原生导入器；校验作者 JSON 后生成单份资源，每张新图无需修改菜单、物理或注册表。</summary>
    [ScriptedImporter(1, "minemap")]
    public sealed class MineMapImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext context)
        {
            var data = JsonUtility.FromJson<MineMapDefinition>(File.ReadAllText(context.assetPath)).CopyValidated();
            var asset = ScriptableObject.CreateInstance<MineMapAsset>();
            asset.name = data.Title; asset.Definition = data;
            context.AddObjectToAsset("map", asset); context.SetMainObject(asset);
        }
    }
}
