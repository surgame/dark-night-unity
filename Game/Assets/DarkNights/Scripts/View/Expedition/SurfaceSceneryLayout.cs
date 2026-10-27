using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using DarkNights.Core.Config.Terrain;
using UnityEngine;

namespace DarkNights.View.Expedition
{
    /// <summary>
    /// 单张预制地图的装饰制作资产；绑定规范化原始地形摘要并保留稳定锚点，显式保存且不在运行中重抽。
    /// 仅供各端只读表现；存档原始背景足以匹配同一资产，实际地形变化决定支撑是否仍有效。
    /// </summary>
    public sealed class SurfaceSceneryLayout : ScriptableObject
    {
        public const int Version = 1;
        public const string DefaultRecipe = "surface-pine-forest-v4";
        public int SchemaVersion = Version;
        public string TerrainKey;
        public string Recipe = DefaultRecipe;
        [Range(0, 1)] public float Density = 1;
        [Range(5, 24)] public int TreeSpacing = 6;
        [Range(3, 20)] public int StoneSpacing = 7;
        public SurfaceSceneryPlacement[] Placements = Array.Empty<SurfaceSceneryPlacement>();
        public string ContentIdentity => Hash(Encoding.UTF8.GetBytes(SchemaVersion + "|" + TerrainKey + "|" + Recipe + "|" +
            string.Join(";", Array.ConvertAll(Placements ?? Array.Empty<SurfaceSceneryPlacement>(), p => p?.Identity ?? "null"))));

        public SurfaceSceneryPlacement[] Capture()
        {
            if (SchemaVersion != Version || TerrainKey == null || TerrainKey.Length != 64 || Placements == null || Placements.Length > 1024)
                throw new ArgumentException("逐图装饰摘要或数量无效。");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var result = new SurfaceSceneryPlacement[Placements.Length];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = Placements[i]?.Capture() ?? throw new ArgumentException("装饰条目缺失。");
                if (!ids.Add(result[i].Id)) throw new ArgumentException("装饰ID重复：" + result[i].Id);
            }
            return result;
        }

        public static string Key(BackgroundBakeDescriptor background) => background == null ? "" :
            new BackgroundBakeDescriptor("00000000000000000000000000000001", background.LayoutSeed,
                background.CopyMaterials(), background.CopyShapes()).ReferenceHash;

        public static SurfaceSceneryLayout Find(SurfaceEnvironmentSettings settings, BackgroundBakeDescriptor background)
        {
            string key = Key(background);
            SurfaceSceneryLayout found = null;
            foreach (var layout in settings.Layouts ?? Array.Empty<SurfaceSceneryLayout>())
            {
                if (layout == null || layout.TerrainKey != key) continue;
                if (found != null) throw new InvalidOperationException("同一地形绑定了多个装饰布局。");
                found = layout;
            }
            return found;
        }

        /// <summary>识别历史生成器的独立草皮锚点；该外覆盖层已暂停，旧作者条目保留，制作校验与表现共同跳过。</summary>
        public static bool IsGroundTrim(SurfaceSceneryPlacement placement) =>
            placement?.Id?.StartsWith("grass-", StringComparison.Ordinal) == true;

        /// <summary>检查根部及整幅素材所在列的露天轮廓；阻止洞室底部、窄井和侧壁下方继续显示地表装饰。</summary>
        public static bool IsOutdoor(SurfaceSceneryPlacement placement, BackgroundBakeDescriptor background)
        {
            if (placement == null || background == null || placement.Row < 1 || placement.Row >= background.Height)
                return false;
            int radius = Math.Max(placement.SupportRadius, Mathf.CeilToInt(placement.Pixels.width * placement.Scale / 32));
            int left = placement.Column - radius, right = placement.Column + radius;
            if (left < 0 || right >= background.Width || background.Material(placement.Column, placement.Row) == 0 ||
                background.Shape(placement.Column, placement.Row) != placement.Shape) return false;
            for (int x = left; x <= right; x++)
            {
                int row = 0;
                while (row < background.Height && background.Material(x, row) == 0) row++;
                if (x == placement.Column ? row != placement.Row : Math.Abs(row - placement.Row) > 3) return false;
            }
            return true;
        }

        private static string Hash(byte[] bytes)
        {
            using var hash = SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
    }
}
