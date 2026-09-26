using System;
using System.Reflection;
using UnityEditor;

namespace DarkNights.Tests
{
    /// <summary>仅供 Editor 画面验收的临时 GameView 固定分辨率；销毁时恢复原选项并移除本测试添加的尺寸。</summary>
    public sealed class TerrainGameViewTestSize : IDisposable
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private readonly EditorWindow game;
        private readonly object group;
        private readonly PropertyInfo selected;
        private readonly int original;
        private readonly int index;
        public TerrainGameViewTestSize(EditorWindow game, int width, int height)
        {
            this.game = game;
            var assembly = typeof(EditorWindow).Assembly;
            var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
            var sizes = sizesType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy).GetValue(null);
            var groupType = assembly.GetType("UnityEditor.GameViewSizeGroupType");
            group = sizesType.GetMethod("GetGroup", Flags).Invoke(sizes, new[] { Enum.Parse(groupType, "Standalone") });
            selected = game.GetType().GetProperty("selectedSizeIndex", Flags);
            original = (int)selected.GetValue(game);
            int builtIn = (int)group.GetType().GetMethod("GetBuiltinCount", Flags).Invoke(group, null);
            int total = (int)group.GetType().GetMethod("GetTotalCount", Flags).Invoke(group, null);
            // 只回收本验证在中断／旧版清理失败时遗留的精确标签，不碰用户自定义尺寸。
            for (int i = total - 1; i >= builtIn; i--)
            {
                var previous = group.GetType().GetMethod("GetGameViewSize", Flags).Invoke(group, new object[] { i });
                if ((string)previous.GetType().GetProperty("baseText", Flags).GetValue(previous) != "Terrain UI verification") continue;
                if (original >= i) original = original == i ? 0 : original - 1;
                group.GetType().GetMethod("RemoveCustomSize", Flags).Invoke(group, new object[] { i });
            }
            index = (int)group.GetType().GetMethod("GetTotalCount", Flags).Invoke(group, null);
            var size = Activator.CreateInstance(assembly.GetType("UnityEditor.GameViewSize"), Flags, null,
                new[] { Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeType"), "FixedResolution"), (object)width, height, "Terrain UI verification" }, null);
            group.GetType().GetMethod("AddCustomSize", Flags).Invoke(group, new[] { size });
            selected.SetValue(game, index); game.Repaint();
        }
        public void Dispose()
        {
            selected.SetValue(game, original);
            group.GetType().GetMethod("RemoveCustomSize", Flags).Invoke(group, new object[] { index });
            if ((int)group.GetType().GetMethod("GetTotalCount", Flags).Invoke(group, null) != index)
                throw new InvalidOperationException("临时 GameView 分辨率未完整恢复。");
            game.Repaint();
        }
    }
}
