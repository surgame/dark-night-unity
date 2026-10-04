using System;

namespace DarkNights.Editor
{
    /// <summary>
    /// 将项目维护命令注册到工作台，而非 Unity 顶部菜单。原路径用于检索与分组，执行仍归原工具。
    /// 只允许无参数静态方法；导航和目录重建不执行命令。
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    internal sealed class DarkNightsWorkbenchCommandAttribute : Attribute
    {
        internal string Path { get; }
        internal string Title { get; }
        internal DarkNightsWorkbenchCommandAttribute(string path, string title)
        {
            Path = path;
            Title = title;
        }
    }
}
