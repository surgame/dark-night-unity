using UnityEditor;

namespace DarkNights.Editor.MapModule
{
    /// <summary>新版模块工作流独占的作者、派生及研究场景路径；仅在已授权初始化中补空目录，不复用旧地图输出目录。</summary>
    internal static class MapModulePaths
    {
        internal const string Root = "Assets/DarkNights/Res/Terrain/MapModule";
        internal const string Modules = Root + "/Modules";
        internal const string Bakes = Root + "/Bakes";
        internal const string Scene = "Assets/DarkNights/Res/Scenes/Workbenches/MapModule/MapModuleEntranceStudy.unity";
        internal const string Ui = "Assets/DarkNights/Scripts/Editor/MapModule/MapModuleWorkbench.uxml";
        internal const string Hero = "Assets/DarkNights/Res/Objects/Hero/AnimationFrames/idle/idle-000.png";
        internal static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
