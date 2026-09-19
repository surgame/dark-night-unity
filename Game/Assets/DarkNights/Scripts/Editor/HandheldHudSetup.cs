using System;
using System.Linq;
using GameCore.UI.UGUI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace DarkNights.Editor
{
    /// <summary>
    /// 将已存在的主角 HUD 定向编辑为底部四格道具栏，保留旧绑定并新增第四格及三个独立图标。
    /// 只由一次性手持安装入口调用；不修改其他菜单、营地面板或原始 UI 素材。
    /// </summary>
    internal static class HandheldHudSetup
    {
        internal static void Install()
        {
            const string path = "Assets/DarkNights/Res/UI/Hero/Hero.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view = root.GetComponent<UGUIView>();
                var bindings = view.Bindings.ToDictionary(b => b.Key, b => b.Target);
                if (bindings.ContainsKey("Item4")) throw new InvalidOperationException("Fourth slot already exists.");
                var status = (Text)bindings["Status"];
                var panel = (RectTransform)status.transform.parent;
                panel.anchoredPosition = new Vector2(0, 12); panel.sizeDelta = new Vector2(490, 84);
                panel.GetComponent<Image>().raycastTarget = false;
                NativeUiExtras.AddButton(panel, "Item4", "4 背包", 370, 8, 112, 34, status.font, bindings);
                for (int i = 1; i <= 4; i++)
                {
                    var button = (Button)bindings["Item" + i];
                    var rect = (RectTransform)button.transform;
                    rect.anchoredPosition = new Vector2(8 + (i - 1) * 120, 8); rect.sizeDelta = new Vector2(114, 34);
                    if (i == 4) continue;
                    var icon = new GameObject("EquipmentIcon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                    icon.transform.SetParent(rect, false); icon.raycastTarget = false; icon.preserveAspect = true;
                    icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0, .5f);
                    icon.rectTransform.anchoredPosition = new Vector2(18, 0); icon.rectTransform.sizeDelta = new Vector2(24, 24);
                    icon.sprite = HandheldContentSetup.ArtSprite(new[] { "PistolIcon", "PickaxeIcon", "BombIcon" }[i - 1]);
                    var label = ((Text)bindings["Item" + i + "Label"]).rectTransform;
                    label.offsetMin = new Vector2(31, 0); label.offsetMax = Vector2.zero;
                }
                status.rectTransform.sizeDelta = new Vector2(466, 28);
                status.fontSize = 12;
                ((RectTransform)bindings["Toggle"].transform).anchoredPosition = new Vector2(-154, 8);
                ((RectTransform)bindings["Rebind"].transform).anchoredPosition = new Vector2(498, 8);
                NativeUiBuilder.Bind(view, bindings);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
