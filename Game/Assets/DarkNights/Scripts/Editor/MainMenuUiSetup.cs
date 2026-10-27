using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkNights.Editor
{
    /// <summary>
    /// 主菜单原生UITK的首次PanelSettings与Addressables注册；仅创建指定空目标，不覆盖旧UGUI作者资源或Bootstrap。
    /// 后续布局、图标、背景和主题均由已保存的UXML／USS／原生资产维护，普通启动不执行此安装。
    /// </summary>
    public static class MainMenuUiSetup
    {
        private const string Root = "Assets/DarkNights/Res/UI/MainMenu/";
        public const string SettingsPath = Root + "MainMenuPanelSettings.asset";

        [DarkNightsWorkbenchCommand("Dark Nights/Content/安装主菜单UITK配置", "安装主菜单UITK配置")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || File.Exists(SettingsPath))
                throw new InvalidOperationException("退出Play；主菜单PanelSettings必须为空目标，拒绝覆盖。");
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Root + "MainMenu.uxml");
            var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(Root + "MainMenuTheme.tss");
            if (tree == null || theme == null) throw new InvalidOperationException("主菜单UXML／TSS尚未导入。");
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var background = AssetImporter.GetAtPath(Root + "Artwork/MenuLandscape.png") as TextureImporter;
            if (settings == null || settings.DefaultGroup == null || background == null)
                throw new InvalidOperationException("缺少已有Addressables默认组或主菜单背景，尚未创建配置。");
            if (settings.groups.Where(group => group != null).SelectMany(group => group.entries).Any(entry =>
                entry.address == "dark_nights.ui.main_menu" || entry.address == "dark_nights.ui.main_menu_settings"))
                throw new InvalidOperationException("主菜单Addressables地址已存在，拒绝创建冲突配置。");
            var panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.themeStyleSheet = theme;
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1280, 720);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = .5f; panel.sortingOrder = 150;
            AssetDatabase.CreateAsset(panel, SettingsPath);
            settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(SettingsPath), settings.DefaultGroup).address =
                "dark_nights.ui.main_menu_settings";
            settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(Root + "MainMenu.uxml"), settings.DefaultGroup).address =
                "dark_nights.ui.main_menu";
            background.filterMode = FilterMode.Point; background.mipmapEnabled = false;
            background.textureCompression = TextureImporterCompression.Uncompressed;
            background.SaveAndReimport();
            EditorUtility.SetDirty(settings); EditorUtility.SetDirty(settings.DefaultGroup);
            AssetDatabase.SaveAssetIfDirty(panel);
            AssetDatabase.SaveAssetIfDirty(settings);
            AssetDatabase.SaveAssetIfDirty(settings.DefaultGroup);
            Debug.Log("DARK_NIGHTS_MAIN_MENU_UITK_INSTALLED");
        }
    }
}
