using System;
using System.Linq;
using DarkNights.Entry;
using DarkNights.Editor.Terrain;
using DarkNights.Runtime.Objects;
using DarkNights.Runtime.Terrain;
using DarkNights.View;
using GameCore.Objects.Definition;
using UnityEditor;
using UnityEngine.SceneManagement;

namespace DarkNights.Editor
{
    /// <summary>
    /// 游戏业务页的关卡装配、飞船和角色装备摘要；只读取现有会话Definition与场景引用。
    /// 地图制作、对象编辑和航程草稿转交原有窗口，浏览不冻结地图、不创建业务对象或保存作者资源。
    /// </summary>
    internal static class DarkNightsWorkbenchGame
    {
        internal static void Build(string module, DarkNightsWorkbenchPage page)
        {
            if (module == "level") Level(page);
            else if (module == "ship") Ship(page);
            else if (module == "objects") Objects(page);
            else if (module == "map")
                page.Note("地形、矿物、环境、巢穴和塌方统一在地图设计工作台制作与保存；此处不重复地图参数。");
        }

        private static ObjectDefinition Session() =>
            AssetDatabase.LoadAssetAtPath<ObjectDefinition>(DarkNightsWorkbenchCatalog.Session);

        private static void Level(DarkNightsWorkbenchPage page)
        {
            var source = Session();
            var flow = source?.SharedConfigs.OfType<ExpeditionFlowConfig>().SingleOrDefault();
            page.Heading("正式游戏装配");
            page.Asset("启动场景", AssetDatabase.LoadAssetAtPath<SceneAsset>(GameScenePaths.Bootstrap));
            page.Asset("内容场景", AssetDatabase.LoadAssetAtPath<SceneAsset>(Entry.Terrain.RandomLevelEntry.ExpeditionScenePath));
            page.Asset("正式地图", flow?.ReviewedMap);
            page.Note("地图由正式会话引用，内容场景承载镜头和对象。切换地图请在地图工作台保存后执行“将当前配置用于新局”。");
            page.Heading("当前试玩来源");
            var active = SceneManager.GetActiveScene();
            bool level = active.IsValid() && active.isLoaded && active.GetRootGameObjects().Any(root =>
                root.GetComponentInChildren<LevelLayoutAuthoring>(true) != null ||
                root.GetComponentInChildren<Entry.Terrain.GeologyLabEntry>(true) != null);
            page.Value("活动场景", string.IsNullOrEmpty(active.path) ? "尚未保存" : active.path);
            page.Value("Play入口", level ? "经Bootstrap服务加载当前关卡" :
                active.path == GameScenePaths.Bootstrap ? "Bootstrap主菜单，默认加载Expedition" : "当前场景直接Play");
            page.Note("普通新局读取正式地图；继续游戏恢复所选存档；F1矿镐快速局独立生成，不用于核对正式保存地图。");
            if (active.isDirty) page.Note("当前场景有未保存标记，进入Play时仍执行原场景保护检查。");
            page.Extend(extra =>
            {
                extra.Heading("会话与地形来源");
                extra.Asset("会话定义", source);
                var profile = source?.SharedConfigs.OfType<TerrainProfileConfig>().SingleOrDefault();
                extra.Asset("正式地形表现", profile?.CaveStyle);
                extra.Definition("编辑正式会话配置", DarkNightsWorkbenchCatalog.Session);
                extra.Heading("场景、实验与参考");
                var launcher = new DarkNightsWorkbenchLauncher("", "全部", _ => { }, _ => { });
                extra.Own(launcher); extra.Add(launcher);
            });
        }

        private static void Ship(DarkNightsWorkbenchPage page)
        {
            var source = Session();
            var flow = source?.SharedConfigs.OfType<ExpeditionFlowConfig>().SingleOrDefault();
            page.Heading("船体与船舱服务");
            page.Definition("编辑正式飞船", DarkNightsWorkbenchCatalog.Root + "Objects/Expedition/ship/ship.asset");
            page.Definition("补给商店", DarkNightsWorkbenchCatalog.ShipTrade + "ship-service-shop.asset");
            page.Definition("交货终端", DarkNightsWorkbenchCatalog.ShipTrade + "ship-service-sale.asset");
            page.Heading("正式航程");
            page.Value("远征流程", flow == null ? "缺少正式会话配置" : flow.Enabled ? "启用" : "关闭");
            page.Value("星球目录", flow?.Planets == null ? "缺失" : flow.Planets.Count + "项");
            page.Value("准备／到达超时", flow == null ? "缺失" :
                flow.PreparationTimeoutSeconds + "秒 / " + flow.ArrivalTimeoutSeconds + "秒");
            page.Note("正式航程参数由会话配置拥有。地图“用于新局”保留正式流程开关、Navigation和两项超时，不能用地图应用成功代替航程生效。");
            page.Extend(extra =>
            {
                extra.Note("下方“游戏HUD、背包与飞船服务界面”是共享布局源：包括四格装备栏、B背包、补给商店和操控室星图；并非只有飞船装备商店。");
            });
        }

        private static void Objects(DarkNightsWorkbenchPage page)
        {
            page.Heading("角色与手持装备");
            page.Definition("主角", DarkNightsWorkbenchCatalog.Root + "Objects/Worker/Worker.asset");
            page.Definition("手电", DarkNightsWorkbenchCatalog.Root + "Objects/Flashlight/Flashlight.asset");
            page.Definition("矿镐", DarkNightsWorkbenchCatalog.ShipTrade + "item-pickaxe.asset");
            page.Definition("手枪", DarkNightsWorkbenchCatalog.ShipTrade + "item-pistol.asset");
            page.Definition("炸药", DarkNightsWorkbenchCatalog.ShipTrade + "item-bomb.asset");
            page.Note("定义、能力和Prefab绑定仍由原生工坊编辑。喷气背包暂退、旧营地业务暂停，原资产保留但不列为常用操作。");
            page.Extend(extra =>
            {
                extra.Definition("矿床定义", DarkNightsWorkbenchCatalog.Root + "Objects/MineralDeposit/MineralDeposit.asset");
                extra.Note("初始手电由唯一FlashlightToolConfig.Starter选择；完整能力和历史定义可在原生工坊查看。");
            });
        }
    }
}
