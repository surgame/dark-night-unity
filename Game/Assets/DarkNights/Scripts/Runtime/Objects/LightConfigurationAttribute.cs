using System;

namespace DarkNights.Runtime.Objects
{
    /// <summary>照明配置的制作入口标记；仅让逐字段绘制的 Definition 工坊识别预设及覆盖面板，不保存业务状态或依赖 Editor 程序集。</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class LightConfigurationAttribute : Attribute
    {
    }
}
