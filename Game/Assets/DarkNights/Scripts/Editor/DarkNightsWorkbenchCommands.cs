using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine.UIElements;

namespace DarkNights.Editor
{
    /// <summary>
    /// 低频安装、构建与验证命令的工作台目录；元数据只扫描本游戏 Editor 程序集。
    /// 写入与构建须点击并确认，原工具继续负责具体输出和验证；浏览及搜索不修改资源。
    /// </summary>
    internal static class DarkNightsWorkbenchCommands
    {
        internal static IReadOnlyList<DarkNightsWorkbenchEntry> Entries { get; } = Discover();

        private static DarkNightsWorkbenchEntry[] Discover()
        {
            return typeof(DarkNightsMenu).Assembly.GetTypes()
                .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                .Where(method => method.IsDefined(typeof(DarkNightsWorkbenchCommandAttribute), false))
                .Select(Create).OrderBy(entry => entry.Group).ThenBy(entry => entry.Title).ToArray();
        }

        private static DarkNightsWorkbenchEntry Create(MethodInfo method)
        {
            var command = method.GetCustomAttribute<DarkNightsWorkbenchCommandAttribute>();
            var action = (Action)Delegate.CreateDelegate(typeof(Action), method);
            string category = command.Path.Split('/')[1];
            string group = category == "Build" ? "构建" : category == "Verify" ? "专项验证" : "安装与迁移";
            bool writes = group != "专项验证";
            string description = group == "构建" ? "生成构建产物；完成结果见原工具日志。" :
                writes ? "一次性内容安装或迁移；按原工具约束执行，操作前请检查现有资产。" :
                "运行专项检查；部分检查需要切换场景或进入 Play，结果见原工具报告。";
            return new DarkNightsWorkbenchEntry("command-" + method.DeclaringType.Name + "-" + method.Name,
                DarkNightsWorkbenchEntryKind.Tool, group, command.Title, description,
                command.Path, "运行…", () =>
                {
                    if (DarkNightsNativeWorkspace.Blocked) return;
                    if (!EditorUtility.DisplayDialog(command.Title, description + "\n\n" + command.Path, "运行", "取消")) return;
                    action();
                });
        }

        internal static VisualElement CreatePanel(Action<DarkNightsWorkbenchEntry> execute)
        {
            var panel = new VisualElement();
            var note = new Label("低频工程操作集中在这里。使用顶部搜索查找名称或原菜单路径。");
            note.AddToClassList("dn-description"); panel.Add(note);
            foreach (var group in Entries.GroupBy(entry => entry.Group))
            {
                var foldout = new Foldout { text = group.Key + "  /  " + group.Count(), value = false };
                foldout.AddToClassList("dn-command-group"); panel.Add(foldout);
                foreach (var entry in group) foldout.Add(DarkNightsWorkbenchCards.Create(entry, () => execute(entry)));
            }
            return panel;
        }
    }
}
