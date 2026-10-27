using System.Linq;
using DarkNights.View.Terrain;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace DarkNights.Editor
{
    /// <summary>
    /// 键位与图形设置的原生来源入口；展示项目默认配置，玩家改键和运行画质仍归原有设置生命周期。
    /// 场景光照只读取已加载正式场景的显式引用，不为查看摘要加载场景或改写渲染配置。
    /// </summary>
    internal static class DarkNightsWorkbenchSettings
    {
        internal static void Build(string module, DarkNightsWorkbenchPage page)
        {
            if (module == "input") Input(page);
            else if (module == "graphics") Graphics(page);
        }

        private static void Input(DarkNightsWorkbenchPage page)
        {
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(
                DarkNightsWorkbenchCatalog.Root + "Input/Gameplay.inputactions");
            page.Heading("项目默认键位");
            page.Asset("动作资产", asset);
            page.Value("动作范围", asset == null ? "缺少默认输入资产" :
                string.Join(" / ", asset.actionMaps.Select(map => map.name)));
            page.Note("这里编辑项目默认绑定；玩家自己的改键在游戏设置中操作，由现有YY输入服务保存。默认资产与本机覆盖不互相替代。");
            page.Extend(extra =>
            {
                extra.Heading("默认动作与绑定");
                if (asset == null) return;
                foreach (var map in asset.actionMaps)
                {
                    extra.Heading(map.name);
                    foreach (var action in map.actions)
                        extra.Value(action.name, action.bindings.Count + "条默认绑定");
                }
            });
        }

        private static void Graphics(DarkNightsWorkbenchPage page)
        {
            page.Heading("项目图形设置");
            int quality = QualitySettings.GetQualityLevel();
            page.Value("颜色空间", PlayerSettings.colorSpace.ToString());
            page.Value("当前质量档", quality >= 0 && quality < QualitySettings.names.Length ?
                QualitySettings.names[quality] : "未选择");
            page.Asset("当前渲染管线", GraphicsSettings.currentRenderPipeline);
            page.Note("颜色空间保持项目约定的Linear；摘要显示当前Editor／项目状态，不新增玩家端图形设置或自动切换画质。天空、岩壁和逐图环境继续在地图工作台编辑。");
            page.Extend(extra =>
            {
                extra.Heading("关卡光照");
                var scene = SceneManager.GetSceneByPath(Entry.Terrain.RandomLevelEntry.ExpeditionScenePath);
                var template = scene.IsValid() && scene.isLoaded ? scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<RandomLevelTemplate>(true)).SingleOrDefault() : null;
                if (template != null) extra.Asset("场景光照引用", template.Lighting);
                else extra.Note("正式内容场景尚未加载；需在其RandomLevelTemplate.Lighting核对实际光照引用，查看摘要不会另开场景。");
                extra.Note("光照调参台管理单灯、预设、场景和灯口；打开后核对目标及保存范围。其默认目标不自动代表当前正式场景。");
            });
        }
    }
}
