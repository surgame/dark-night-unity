using System;

namespace DarkNights.Runtime.Framework
{
    /// <summary>
    /// 标记只读 JSON 目录中的规则引用字段，供 Editor 的 Workshop 下拉与数值预览使用。
    /// 属性只保存目录名称，不在运行时加载文件，也不复制规则数值到对象配置。
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class RuleKeyAttribute : Attribute
    {
        public string Family { get; }
        public RuleKeyAttribute(string family) { Family = family; }
    }
}
