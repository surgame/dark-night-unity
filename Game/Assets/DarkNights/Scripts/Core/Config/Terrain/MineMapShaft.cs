using System;

namespace DarkNights.Core.Config.Terrain
{
    /// <summary>作者声明的短竖井及梯顶落脚方向；只生成真实地形，不预搭梯子，也不赋予主角攀爬能力。</summary>
    [Serializable]
    public sealed class MineMapShaft
    {
        public string Id;
        public string Lower;
        public string Upper;
        public int X;
        public int Top;
        public int Bottom;
        public int Side = 1;
        public MineMapShaft Copy() => (MineMapShaft)MemberwiseClone();
    }
}
