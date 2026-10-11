using System;

namespace DarkNights.Core.Config.Terrain
{
    /// <summary>两个作者洞室之间的步行道输入；Points 按 x、脚底行成对排列，封岩只改变前景，不奖励矿物。</summary>
    [Serializable]
    public sealed class MineMapPath
    {
        public string Id;
        public string From;
        public string To;
        public int[] Points;
        public int SealX = -1;
        public MineMapPath Copy()
        {
            var copy = (MineMapPath)MemberwiseClone();
            copy.Points = Points == null ? null : (int[])Points.Clone();
            return copy;
        }
    }
}
