using System;
using DarkNights.Core.Config.Terrain;

namespace DarkNights.Core.Logic.Terrain
{
    /// <summary>可替换的背景布局算法；参数须先冻结，版本身份覆盖全部生成输入，只能读取初始参考，不能读取挖掘后的当前格子。</summary>
    public interface ICaveBackgroundGenerator
    {
        string Identity { get; }
        ICaveBackgroundLayout Build(BackgroundBakeDescriptor source, CaveOutlineSettings outline, Action checkpoint = null);
    }
}
