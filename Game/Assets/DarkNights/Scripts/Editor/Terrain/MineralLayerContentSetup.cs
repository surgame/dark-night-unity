using System;
using System.Linq;
using DarkNights.Core.Config;
using DarkNights.Runtime.Objects;
using DarkNights.View.Terrain;
using GameCore.Objects.Definition;
using UnityEditor;

namespace DarkNights.Editor.Terrain
{
    /// <summary>首版矿层的有限原生装配批次；仅绑定新增矿目录、为背景保留排序空隙并启用矿镐的矿床类别，保留已有资产 GUID 和数值。</summary>
    public static class MineralLayerContentSetup
    {
        public static void Install()
        {
            var definition = MineralLayerAssets.Ensure();
            var paths = new[] { "Assets/DarkNights/Res/Terrain/StrataCave/Style.asset",
                "Assets/DarkNights/Res/Terrain/CaveExploration/Style/CaveStyle.asset" };
            foreach (var path in paths)
            {
                var style = AssetDatabase.LoadAssetAtPath<CaveTerrainStyle>(path);
                if (style == null) throw new InvalidOperationException("缺少矿层目标样式：" + path);
                style.MineralDefinition = definition; EditorUtility.SetDirty(style);
                if (style.Background != null)
                {
                    style.Background.FarOrder = -100; style.Background.DeepOrder = -80;
                    style.Background.MiddleOrder = -70; style.Background.NearOrder = -60;
                    EditorUtility.SetDirty(style.Background);
                    AssetDatabase.SaveAssetIfDirty(style.Background);
                }
                AssetDatabase.SaveAssetIfDirty(style);
            }
            var database = ObjectDefinitionDatabase.Instance; database.RebuildLookup();
            var pickaxe = database.GetDefinitionByKey("item.pickaxe");
            var mining = pickaxe.SharedConfigs.OfType<MiningToolConfig>().Single();
            mining.Targets |= MiningTargetKinds.MineralDeposit;
            mining.Freeze(); EditorUtility.SetDirty(pickaxe);
            AssetDatabase.SaveAssetIfDirty(pickaxe);
        }
    }
}
