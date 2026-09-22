using System;

namespace DarkNights.Core.Logic.Terrain
{
    /// <summary>可插拔的纯轮廓修饰阶段；实现持有冻结参数、声明完整版本身份，不修改输入或权威地形，输出仍采用原生像素向下坐标。</summary>
    public interface ICaveMaskModifier
    {
        string Identity { get; }
        CaveMaskField Apply(CaveMaskField source, string worldSeed, Action checkpoint = null);
    }
}
