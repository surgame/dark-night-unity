using System;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkNights.Editor
{
    /// <summary>为飞船 UI 初建运行时面板配置；仅向空目标写入主题引用，后续由人工维护且普通构建不覆盖。</summary>
    public static class ShipEquipmentUiSetup
    {
        public const string SettingsPath = "Assets/DarkNights/Res/UI/ShipEquipment/ShipEquipmentPanelSettings.asset";
        public const string ThemePath = "Assets/DarkNights/Res/UI/ShipEquipment/ShipEquipmentTheme.tss";

        [MenuItem("Dark Nights/Content/安装飞船界面配置")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || File.Exists(SettingsPath))
                throw new InvalidOperationException("退出 Play；面板配置目标必须为空，拒绝覆盖已有资产。");
            var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            if (theme == null) throw new InvalidOperationException("先完成飞船运行时主题导入。");
            var panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.themeStyleSheet = theme;
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1280, 720);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = .5f;
            panel.sortingOrder = 100;
            AssetDatabase.CreateAsset(panel, SettingsPath);
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(SettingsPath), settings.DefaultGroup).address =
                "dark_nights.ui.ship_equipment_settings";
            EditorUtility.SetDirty(settings); EditorUtility.SetDirty(settings.DefaultGroup);
            AssetDatabase.SaveAssets();
        }
    }
}
