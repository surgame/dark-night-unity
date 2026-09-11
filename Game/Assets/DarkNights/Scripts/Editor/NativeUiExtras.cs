using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DarkNights.Editor
{
    /// <summary>
    /// 在原版面板的底部留白中增加已确认的 LAN 加入和共享控制入口，不改写原文或已有控件布局。
    /// 新增控件仍保存为原生 UGUI 并通过显式键绑定，不在运行时全局创建 UI。
    /// </summary>
    public static class NativeUiExtras
    {
        public static void Add(GameObject root, string page, Font font, Dictionary<string, Component> bindings)
        {
            if (page == "MainMenu")
            {
                AddButton(root.transform, "Join", "加入房间", 88, 18, 160, 34, font, bindings);
                RectTransform box = Rect(root.transform, "Address", 260, 18, 210, 34);
                Image background = box.gameObject.AddComponent<Image>();
                background.color = new Color(0.07f, 0.12f, 0.15f, 1);
                var input = box.gameObject.AddComponent<InputField>();
                input.targetGraphic = background;
                Text text = Label(box, "Value", "127.0.0.1", font);
                ((RectTransform)text.transform).offsetMin = new Vector2(8, 0);
                ((RectTransform)text.transform).offsetMax = new Vector2(-8, 0);
                input.textComponent = text;
                input.text = "127.0.0.1";
                input.characterLimit = 253;
                bindings.Add("Address", input);
                RectTransform status = Rect(root.transform, "ConnectionStatus", 488, 18, 700, 34);
                bindings.Add("ConnectionStatus", Label(status, "Value", "创建房间后，可供同一局域网的玩家加入。", font));
            }
            if (page == "PauseMenu")
            {
                RectTransform button = AddButton(root.transform, "ControlMode", "切换共享营地控制", -150, 28, 300, 34, font, bindings);
                button.anchorMin = button.anchorMax = new Vector2(0.5f, 0);
            }
        }

        public static RectTransform AddButton(Transform parent, string key, string text, float x, float y,
            float width, float height, Font font, Dictionary<string, Component> bindings)
        {
            RectTransform rect = Rect(parent, key, x, y, width, height);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.133333f, 0.196078f, 0.223529f, 1);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            Text label = Label(rect, "Label", text, font);
            label.alignment = TextAnchor.MiddleCenter;
            bindings.Add(key, button);
            bindings.Add(key + "Label", label);
            return rect;
        }

        private static RectTransform Rect(Transform parent, string name, float x, float y, float width, float height)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        private static Text Label(Transform parent, string name, string value, Font font)
        {
            var root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            NativeUiBuilder.Stretch((RectTransform)root.transform);
            var text = root.AddComponent<Text>();
            text.font = font;
            text.fontSize = 14;
            text.text = value;
            text.color = new Color32(228, 229, 215, 255);
            text.alignment = TextAnchor.MiddleLeft;
            text.raycastTarget = false;
            return text;
        }
    }
}
