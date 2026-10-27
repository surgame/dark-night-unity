using System;
using System.Linq;
using DarkNights.Core.Config.Terrain;
using DarkNights.Editor.Terrain;
using UnityEditor;

namespace DarkNights.Editor.Lighting
{
    /// <summary>
    /// 照明工具的已保存规则地图入口；只读取预制地图格子，不执行旧H5或其他地图生成器。
    /// 返回独立冻结蓝图供离屏表现使用；地图变化后必须重新记录身份，历史照明证据不沿用。
    /// </summary>
    public static class LightingPreviewMap
    {
        public static readonly string[] Ids = { "erosion", "switchback", "cluster", "rift", "roots" };
        public static readonly string[] Names = { "侵蚀曲廊", "断层折返", "洞厅星簇", "裂谷侧坡", "密岔根网" };

        public static TerrainBlueprint Load(string id)
        {
            int index = Array.IndexOf(Ids, id);
            if (index < 0) throw new ArgumentException("请选择现有规则预制地图；旧H5底图已退场：" + id);
            string path = "Assets/DarkNights/Res/Terrain/StrataCave/MapDocuments/RuleMaps20261021/" +
                Names[index] + ".asset";
            var document = AssetDatabase.LoadAssetAtPath<TerrainMapDocument>(path);
            if (document == null || document.Map == null || document.Input == null)
                throw new InvalidOperationException("缺少照明预览地图：" + path);
            document.Map.VerifySource(document.Input);
            var map = document.Map.Freeze("00000000000000000000000000000001");
            var settings = document.Input.FreezeCaveMap();
            settings.Seed = map.Seed;
            var surface = new int[TerrainGenerationSettings.Width];
            for (int x = 0; x < surface.Length; x++)
            {
                int row = 0;
                while (row < TerrainGenerationSettings.Height && map.Material(x, row) == 0) row++;
                surface[x] = row;
            }
            return new TerrainBlueprint(settings, map.CopyMaterials(), map.CopyProtection(), surface,
                map.Rooms.ToArray(), map.CopySoftRock(), map.Deposits.ToArray(), shapes: map.CopyShapes());
        }
    }
}
