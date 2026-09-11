using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.View;
using GameCore.Objects.Views;
using GameCore.UI.UGUI;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace DarkNights.Editor
{
    /// <summary>
    /// 将冻结 UI 控件、原文、主题色与实际几何批量写成原生 UGUI 层级及显式绑定。
    /// 此类只负责首版控件，不绑定业务、加载存档或发起联机；每个面板保存后独立人工维护。
    /// </summary>
    public static class NativeUiBuilder
    {
        public static Dictionary<string, Component> Populate(GameObject root, JObject first, JObject second, Font font, Font title)
        {
            var controls = new Dictionary<string, RectTransform>();
            var rows = ((JArray)first["nodes"]).Cast<JObject>().ToDictionary(row => (string)row["path"]);
            var other = ((JArray)second["nodes"]).Cast<JObject>().ToDictionary(row => (string)row["path"]);
            var bindings = new Dictionary<string, Component>();
            foreach (JObject row in first["nodes"])
            {
                string path = (string)row["path"];
                string name = path.Split('/').Last();
                string parentPath = path.Contains("/") ? path.Substring(0, path.LastIndexOf('/')) : ".";
                GameObject target = path == "." ? root : new GameObject(name, typeof(RectTransform));
                var rect = (RectTransform)target.transform;
                if (path != ".") rect.SetParent(controls[parentPath], false);
                NativeUiGeometry.Set(rect, (JArray)row["rect"], (JArray)other[path]["rect"],
                    path == "." ? null : (JArray)rows[parentPath]["rect"], path == "." ? null : (JArray)other[parentPath]["rect"]);
                controls.Add(path, rect);
                target.SetActive((bool)row["visible"]);
                string type = (string)row["type"];
                Component binding = rect;
                if (name == "Map" && (bool)row["unique"]) binding = target.AddComponent<CampMap>();
                else if (type == "Label") binding = Text(target, row, font, title);
                else if (type == "Button")
                {
                    Image background = Panel(target, row["styles"]["normal"]);
                    var button = target.AddComponent<Button>();
                    button.targetGraphic = background;
                    button.interactable = !(bool)row["disabled"];
                    button.navigation = new Navigation { mode = Navigation.Mode.None };
                    ColorBlock colors = button.colors;
                    colors.normalColor = Color.white;
                    colors.highlightedColor = Ratio(ColorValue(row["styles"]["hover"]["background"]), background.color);
                    colors.pressedColor = Ratio(ColorValue(row["styles"]["pressed"]["background"]), background.color);
                    colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 1);
                    button.colors = colors;
                    var label = new GameObject("Label", typeof(RectTransform));
                    label.transform.SetParent(rect, false);
                    Stretch((RectTransform)label.transform);
                    Text text = Text(label, row, font, title);
                    text.alignment = TextAnchor.MiddleCenter;
                    if ((bool)row["unique"]) bindings.Add(name + "Label", text);
                    binding = button;
                }
                else if (type == "PanelContainer") Panel(target, row["styles"]["panel"]);
                else if (type == "ColorRect") target.AddComponent<Image>().color = ColorValue(row["color"]);
                else if (type == "TextureRect")
                {
                    var image = target.AddComponent<Image>();
                    string texture = (string)row["texture"];
                    if (!string.IsNullOrEmpty(texture)) image.sprite = NativeAnimationBuilder.Sprite(texture);
                    else image.color = Color.clear;
                    image.preserveAspect = true;
                    image.raycastTarget = false;
                    binding = image;
                }
                else if (type == "ProgressBar")
                {
                    Panel(target, row["styles"]["background"]);
                    var fill = new GameObject("Fill", typeof(RectTransform));
                    fill.transform.SetParent(rect, false);
                    Stretch((RectTransform)fill.transform);
                    Image image = fill.AddComponent<Image>();
                    image.sprite = UnityEditor.AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
                    image.type = Image.Type.Filled;
                    image.fillMethod = Image.FillMethod.Horizontal;
                    image.fillAmount = (float)row["value"];
                    image.color = ColorValue(row["styles"]["fill"]["background"]);
                    image.raycastTarget = false;
                    binding = image;
                }
                if ((bool)row["unique"]) bindings.Add(name, binding);
            }
            if ((string)first["name"] == "Chrome")
            {
                var layer = new GameObject("WorldOverlay", typeof(RectTransform));
                layer.transform.SetParent(root.transform, false);
                layer.transform.SetAsFirstSibling();
                Stretch((RectTransform)layer.transform);
                var overlay = layer.AddComponent<CampOverlay>();
                overlay.raycastTarget = false;
                bindings.Add("Overlay", overlay);
            }
            return bindings;
        }

        public static void Bind(UGUIView view, Dictionary<string, Component> bindings)
        {
            view.EditorSetBindings(bindings.Select(pair => new ViewComponentBinding(pair.Key, pair.Value)).ToArray(), false);
        }

        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        public static Text Text(GameObject target, JObject row, Font font, Font title)
        {
            var text = target.AddComponent<Text>();
            bool heading = ((string)row["fontName"]).Contains("Georgia") || ((string)row["fontName"]).Contains("Times");
            text.font = heading ? title : font;
            text.fontStyle = heading ? FontStyle.Bold : FontStyle.Normal;
            text.fontSize = (int)row["fontSize"];
            text.color = ColorValue(row["fontColor"]);
            text.text = (string)row["text"];
            text.alignment = (int)row["alignment"] == 1 ? TextAnchor.MiddleCenter : (int)row["alignment"] == 2 ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
            text.horizontalOverflow = (bool)row["wrap"] ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        public static Image Panel(GameObject target, JToken style)
        {
            var image = target.AddComponent<Image>();
            image.color = ColorValue(style["background"]);
            if ((int)style["borderWidth"] > 0)
            {
                var outline = target.AddComponent<Outline>();
                outline.effectColor = ColorValue(style["border"]);
                outline.effectDistance = new Vector2((int)style["borderWidth"], -(int)style["borderWidth"]);
            }
            return image;
        }

        private static Color ColorValue(JToken value) => new Color((float)value[0], (float)value[1], (float)value[2], (float)value[3]);
        private static Color Ratio(Color value, Color basis) => new Color(value.r / Mathf.Max(0.001f, basis.r),
            value.g / Mathf.Max(0.001f, basis.g), value.b / Mathf.Max(0.001f, basis.b), 1);
    }
}
