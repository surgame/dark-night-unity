using System;
using System.Linq;
using GameCore.Objects.Views;
using GameCore.UI.UGUI;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Editor
{
    /// <summary>
    /// 显式向既有菜单留白添加两个原生槽位按钮，保存现有 GUID 和所有原版控件几何。
    /// 新键已存在即拒绝修改；只安装一次，正常导入与构建不调用此入口。
    /// </summary>
    public static class NativeSaveUiSetup
    {
        public static void Install()
        {
            string[] pages = { "MainMenu", "PauseMenu" };
            foreach (string page in pages)
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{NativeUiSetup.Root}/{page}/{page}.prefab");
                if (asset.GetComponent<UGUIView>().Bindings.Any(b => b.Key == "Slot"))
                    throw new InvalidOperationException("Slot controls already exist: " + page);
            }
            Font font = AssetDatabase.LoadAssetAtPath<Font>(NativeUiSetup.Root + "/Shared/UIFont.fontsettings");
            foreach (string page in pages)
            {
                string path = $"{NativeUiSetup.Root}/{page}/{page}.prefab";
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var view = root.GetComponent<UGUIView>();
                    var bindings = view.Bindings.ToDictionary(b => b.Key, b => b.Target);
                    RectTransform button = NativeUiExtras.AddButton(root.transform, "Slot", "存档槽位 1 / 10 · 空槽位 · 点击切换",
                        page == "MainMenu" ? 88 : -190, page == "MainMenu" ? 62 : 70, 380, 34, font, bindings);
                    if (page == "PauseMenu") button.anchorMin = button.anchorMax = new Vector2(.5f, 0);
                    NativeUiBuilder.Bind(view, bindings);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
        }
    }
}
