using System;
using DarkNights.Core.Config.Expedition;

namespace DarkNights.Runtime.Objects
{
    /// <summary>星图节点的作者输入；仅在进入会话时冻结，运行状态不能反写配置或修改地形。</summary>
    [Serializable]
    public sealed class NavigationNodePreset
    {
        public string Id = "grey-pine";
        public string Name = "灰松星";
        public float X;
        public float Y;
        public int Danger = 1;
        public string Risk = "矿洞与巢穴迹象（mock资料）";
        public bool PreviewOnly;

        public NavigationNodeDefinition Freeze() => new NavigationNodeDefinition(Id, Name, X, Y, Danger, Risk, PreviewOnly);
    }
}
