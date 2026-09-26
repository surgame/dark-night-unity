using System;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;

namespace DarkNights.Core.Config.Expedition
{
    /// <summary>
    /// 星球预设的纯只读副本；由会话在生成前冻结，后台生成、网络目录及展示共享同一参数合同。
    /// 几何使用权威像素和向下的地图行；不拥有航程状态，不引用 Unity 资产或运行对象。
    /// </summary>
    public sealed class PlanetDefinition
    {
        public string Id { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public bool Enabled { get; }
        public string Seed { get; }
        public int DockColumn { get; }
        public int DockRow { get; }
        public int LandingWidth { get; }
        public float ArrivalHeight { get; }
        public float HorizontalRange { get; }
        public float MaximumLift { get; }
        public float TransitSeconds { get; }
        public int StarCount { get; }
        public float StarSpeed { get; }
        public string TransitionKind { get; }
        public string SpaceColorHex { get; }
        public string SkyColorHex { get; }
        public float DockX => (DockColumn - .5f) * PlayableTerrain.CellPixels;
        public float DockHeight => (PlayableTerrain.CampRow - DockRow) * PlayableTerrain.CellPixels;

        public PlanetDefinition(string id, string displayName, string description = "", bool enabled = true,
            string seed = "", int dockColumn = 36, int dockRow = 40, int landingWidth = 32,
            float arrivalHeight = 176, float horizontalRange = 240, float maximumLift = 448,
            float transitSeconds = .8f, int starCount = 120, float starSpeed = 110,
            string transitionKind = "star-shift", string spaceColorHex = "#060C20", string skyColorHex = "#243B55")
        {
            if (string.IsNullOrWhiteSpace(id) || id.Length > 48 || id != id.Trim())
                throw new ArgumentException("星球 ID 必须为 1–48 位稳定标识。", nameof(id));
            foreach (char c in id)
                if (!(c >= 'a' && c <= 'z') && !(c >= '0' && c <= '9') && c != '-' && c != '_')
                    throw new ArgumentException("星球 ID 仅允许小写英文字母、数字、连字符和下划线。", nameof(id));
            if (string.IsNullOrWhiteSpace(displayName) || displayName.Length > 64 || description == null || description.Length > 512)
                throw new ArgumentException("星球名称不得为空且不超过 64 字，说明不得超过 512 字。");
            if (seed == null || seed.Length > 80 || seed.Length > 0 && string.IsNullOrWhiteSpace(seed))
                throw new ArgumentException("固定种子为 1–80 字，留空才表示每次随机。", nameof(seed));
            if (landingWidth < 28 || landingWidth > 64 || landingWidth % 2 != 0 || dockRow < 28 || dockRow > 48 ||
                dockColumn - landingWidth / 2 < 3 || dockColumn + landingWidth / 2 >= TerrainGenerationSettings.Width - 3)
                throw new ArgumentException("降落平台须为 28–64 格偶数宽度，行号 28–48，左右距地图边界至少三格。");
            Positive(arrivalHeight, 600, nameof(arrivalHeight));
            Positive(horizontalRange, 1200, nameof(horizontalRange));
            Positive(maximumLift, 600, nameof(maximumLift));
            Positive(transitSeconds, 5, nameof(transitSeconds));
            Positive(starSpeed, 2000, nameof(starSpeed));
            float dockX = (dockColumn - .5f) * PlayableTerrain.CellPixels;
            if (arrivalHeight > maximumLift || maximumLift + ShipGeometry.Roof + 8 > dockRow * PlayableTerrain.CellPixels ||
                horizontalRange + ShipGeometry.HalfWidth + 16 > Math.Min(dockX, TerrainGenerationSettings.Width * 16 - dockX))
                throw new ArgumentException("到达高度或飞行包络超出开放天空与地图安全边界。");
            if (starCount < 0 || starCount > 512) throw new ArgumentOutOfRangeException(nameof(starCount), "星点数须为 0–512。");
            if (transitionKind != "star-shift" && transitionKind != "fade" && transitionKind != "none")
                throw new ArgumentException("未知过场策略。", nameof(transitionKind));
            ValidateColor(spaceColorHex); ValidateColor(skyColorHex);
            Id = id; DisplayName = displayName; Description = description; Enabled = enabled; Seed = seed;
            DockColumn = dockColumn; DockRow = dockRow; LandingWidth = landingWidth;
            ArrivalHeight = arrivalHeight; HorizontalRange = horizontalRange; MaximumLift = maximumLift;
            TransitSeconds = transitSeconds; StarCount = starCount; StarSpeed = starSpeed;
            TransitionKind = transitionKind; SpaceColorHex = spaceColorHex; SkyColorHex = skyColorHex;
        }

        private static void Positive(float value, float maximum, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0 || value > maximum)
                throw new ArgumentOutOfRangeException(name, "配置必须为有限正数并位于支持范围内。");
        }

        private static void ValidateColor(string value)
        {
            if (value == null || value.Length != 7 || value[0] != '#')
                throw new ArgumentException("颜色必须使用 #RRGGBB 格式。");
            for (int i = 1; i < value.Length; i++)
                if (!Uri.IsHexDigit(value[i])) throw new ArgumentException("颜色包含无效的十六进制字符。");
        }
    }
}
