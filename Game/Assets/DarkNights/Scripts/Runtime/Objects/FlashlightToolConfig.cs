using System;
using GameCore.Objects.Behaviours.Interfaces;

namespace DarkNights.Runtime.Objects
{
    /// <summary>照明道具的库存能力标记；仅声明新角色配发规则，视觉参数唯一保存于共用效果 Prefab。</summary>
    [Serializable]
    public sealed class FlashlightToolConfig : IConfigData
    {
        public string Name => "照明道具能力";
        public bool Starter = true;
    }
}
