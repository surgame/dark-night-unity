using System;

namespace DarkNights.Core.Config.Terrain
{
    /// <summary>作者矿洞的单个洞室输入；坐标为矿洞局部格，Floor 是脚底支撑行，实例进度不写回本项。</summary>
    [Serializable]
    public sealed class MineMapRoom
    {
        public string Id;
        public string Name;
        public int X;
        public int Floor;
        public int Radius = 4;
        public int Height = 5;
        public bool Base;
        public bool SurfaceOre;
        public bool BuriedOre;
        public MineMapRoom Copy() => (MineMapRoom)MemberwiseClone();
    }
}
