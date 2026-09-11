using System;
using System.IO;
using System.Linq;
using System.Reflection;
using DarkNights.Editor;
using DarkNights.View;
using GameCore.Objects.Views;
using GameCore.UI.UGUI;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace DarkNights.Tests
{
    /// <summary>
    /// 重开正式 UGUI Prefab，对照两套冻结源几何、原文和实际类型绑定，另验动态系统字体的持久引用。
    /// 测试只修改临时实例；不生成期望值或覆盖人工资源，也不把静态测试当作真实按钮／联网验收。
    /// </summary>
    public sealed class NativeUiTests
    {
        [TestCase(0)]
        [TestCase(1)]
        public void ReopenedPanelsMatchFrozenGeometryAndBindings(int profile)
        {
            JObject input = JObject.Parse(File.ReadAllText(NativeUiSetup.InputPath));
            JToken data = input["profiles"][profile];
            var viewport = new GameObject("Test viewport", typeof(RectTransform));
            var parent = (RectTransform)viewport.transform;
            parent.sizeDelta = profile == 0 ? new Vector2(1280, 800) : new Vector2(1600, 900);
            int checkedRows = 0;
            try
            {
                foreach (JObject page in data["pages"])
                {
                    string name = (string)page["name"];
                    GameObject root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(NativeUiSetup.Root + "/" + name + "/" + name + ".prefab"));
                    try
                    {
                        UnityEngine.Object.DestroyImmediate(root.GetComponent<GraphicRaycaster>());
                        UnityEngine.Object.DestroyImmediate(root.GetComponent<CanvasScaler>());
                        UnityEngine.Object.DestroyImmediate(root.GetComponent<Canvas>());
                        root.transform.SetParent(parent, false);
                        NativeUiBuilder.Stretch((RectTransform)root.transform);
                        foreach (JObject row in page["nodes"])
                        {
                            string path = (string)row["path"];
                            var rect = (RectTransform)(path == "." ? root.transform : root.transform.Find(path));
                            Assert.That(rect, Is.Not.Null, name + "/" + path);
                            var container = (RectTransform)rect.parent;
                            Vector3 min = container.InverseTransformPoint(rect.TransformPoint(rect.rect.min));
                            Vector3 max = container.InverseTransformPoint(rect.TransformPoint(rect.rect.max));
                            float[] actual = { min.x - container.rect.xMin, container.rect.yMax - max.y, max.x - min.x, max.y - min.y };
                            for (int i = 0; i < 4; i++) Assert.That(actual[i], Is.EqualTo((float)row["rect"][i]).Within(.05), name + "/" + path + " edge " + i);
                            string type = (string)row["type"];
                            if (type == "Label" || type == "Button")
                            {
                                Text text = type == "Label" ? rect.GetComponent<Text>() : rect.Find("Label").GetComponent<Text>();
                                Assert.That(text.text.Replace("\r\n", "\n"), Is.EqualTo(((string)row["text"]).Replace("\r\n", "\n")), name + "/" + path);
                                Assert.That(text.fontSize, Is.EqualTo((int)row["fontSize"]));
                            }
                            checkedRows++;
                        }
                        UGUIView view = root.GetComponent<UGUIView>();
                        foreach (UiShapeGraphic graphic in root.GetComponentsInChildren<UiShapeGraphic>(true))
                            Assert.That(graphic.GetComponent<CanvasRenderer>(), Is.Not.Null, graphic.name);
                        foreach (FieldInfo field in NativeUiSetup.Behaviour(name).GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
                        {
                            var attribute = field.GetCustomAttribute<ViewComponentAttribute>();
                            if (attribute == null) continue;
                            var binding = view.Bindings.Single(b => b.Key == attribute.Key);
                            Assert.That(binding.Target, Is.InstanceOf(field.FieldType), name + "/" + attribute.Key);
                        }
                    }
                    finally { UnityEngine.Object.DestroyImmediate(root); }
                }
                Assert.That(checkedRows, Is.EqualTo(162));
            }
            finally { UnityEngine.Object.DestroyImmediate(viewport); }
        }

        [Test]
        public void SavedDynamicFontsContainMaterialsAndCanRenderOriginalLanguages()
        {
            foreach (string name in new[] { "UIFont", "TitleFont" })
            {
                var font = AssetDatabase.LoadAssetAtPath<Font>(NativeUiSetup.Root + "/Shared/" + name + ".fontsettings");
                Assert.That(font.dynamic && font.material != null && font.material.mainTexture != null);
                string text = name == "UIFont" ? "灰松谷木材" : "DARK NIGHTS";
                font.RequestCharactersInTexture(text, 20, FontStyle.Normal);
                foreach (char character in text.Where(c => c != ' '))
                    Assert.That(font.GetCharacterInfo(character, out _, 20, FontStyle.Normal), Is.True, name + character);
                Assert.That(AssetDatabase.Contains(font.material), Is.True);
            }
        }
    }
}
