using UnityEditor;
using UnityEngine;

namespace DarkNights.Editor.Terrain
{
    /// <summary>功能场景原生uGUI状态栏首版制作；沿用项目中文字体和Canvas模式，运行只更新绑定文本。不修改共享HUD、字体源或输入布局。</summary>
    internal static class GeologyLabUi
    {
        internal static UnityEngine.UI.Text Create(Transform parent)
        {
            var root = new GameObject("LabStatusCanvas", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            root.transform.SetParent(parent, false); root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>(); scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = .5f;
            var panel = new GameObject("StatusPanel", typeof(RectTransform), typeof(UnityEngine.UI.Image)); panel.transform.SetParent(root.transform, false);
            var rect = panel.GetComponent<RectTransform>(); rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(.5f, 1); rect.sizeDelta = new Vector2(0, 150);
            var image = panel.GetComponent<UnityEngine.UI.Image>(); image.color = new Color(.035f, .045f, .06f, .92f); image.raycastTarget = false;
            var label = new GameObject("Status", typeof(RectTransform), typeof(UnityEngine.UI.Text)); label.transform.SetParent(panel.transform, false);
            var labelRect = label.GetComponent<RectTransform>(); labelRect.anchorMin = Vector2.zero; labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(20, 10); labelRect.offsetMax = new Vector2(-20, -10);
            var text = label.GetComponent<UnityEngine.UI.Text>(); text.font = AssetDatabase.LoadAssetAtPath<Font>(NativeUiSetup.Root + "/Shared/UIFont.fontsettings");
            text.fontSize = 20; text.color = new Color(.92f, .94f, .96f); text.raycastTarget = false;
            text.text = "场景功能开发 · 地质事件\n左：挖断黄色支撑岩触发；右：进入橙色区域触发。\n前兆后避让；落地后用矿镐清障。基础跳跃可越过一格瓦砾。\n移动／跳跃／左键沿用正式改键，Esc暂停，重新Play重置。";
            return text;
        }
    }
}
