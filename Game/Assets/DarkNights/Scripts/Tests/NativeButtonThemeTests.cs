using System.Collections;
using DarkNights.Editor;
using DarkNights.View;
using GameCore.UI.UGUI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace DarkNights.Tests
{
    /// <summary>
    /// 从正式 Prefab 重开按钮，验证文字与背景共用指针／禁用状态，并覆盖禁用中断和池化重入。
    /// 只操作临时对象；期望颜色来自冻结原主题，不读取本次实现生成期望，也不触发存档或网络。
    /// </summary>
    public sealed class NativeButtonThemeTests
    {
        private static readonly Color Normal = new Color32(228, 229, 215, 255);
        private static readonly Color Hover = new Color32(255, 241, 206, 255);
        private static readonly Color Pressed = new Color32(217, 195, 147, 255);
        private static readonly Color Disabled = new Color32(104, 123, 123, 255);

        [TestCase("Chrome")]
        [TestCase("MainMenu")]
        [TestCase("PauseMenu")]
        [TestCase("Help")]
        [TestCase("Result")]
        public void AllButtonsKeepExplicitTextAndBackgroundStates(string page)
        {
            GameObject root = Open(page);
            var events = new GameObject("Button events", typeof(EventSystem));
            try
            {
                var pointer = new PointerEventData(events.GetComponent<EventSystem>());
                Button[] buttons = root.GetComponentsInChildren<Button>(true);
                Assert.That(buttons, Is.Not.Empty);
                foreach (Button button in buttons)
                {
                    NativePanelTheme theme = button.GetComponent<NativePanelTheme>();
                    Assert.That(theme, Is.Not.Null, page + "/" + button.name);
                    var saved = new SerializedObject(theme);
                    Text label = saved.FindProperty("label").objectReferenceValue as Text;
                    Assert.That(label, Is.SameAs(button.transform.Find("Label").GetComponent<Text>()));
                    Color normal = saved.FindProperty("normalText").colorValue;
                    bool primary = normal.r < .1f;
                    AssertColor(normal, primary ? new Color32(23, 32, 38, 255) : Normal);
                    button.interactable = true;
                    theme.OnPointerExit(pointer);
                    AssertColor(label.color, normal);
                    theme.OnPointerEnter(pointer);
                    AssertColor(label.color, primary ? new Color32(17, 27, 32, 255) : Hover);
                    pointer.button = PointerEventData.InputButton.Right;
                    theme.OnPointerDown(pointer);
                    AssertColor(label.color, primary ? new Color32(17, 27, 32, 255) : Hover);
                    pointer.button = PointerEventData.InputButton.Left;
                    theme.OnPointerDown(pointer);
                    AssertColor(label.color, Pressed);
                    theme.OnPointerExit(pointer);
                    AssertColor(label.color, normal);
                    theme.OnPointerEnter(pointer);
                    AssertColor(label.color, Pressed);
                    button.interactable = false;
                    theme.OnPointerUp(pointer);
                    AssertColor(label.color, Disabled);
                    button.interactable = true;
                    theme.OnPointerEnter(pointer);
                    AssertColor(label.color, primary ? new Color32(17, 27, 32, 255) : Hover);
                    theme.OnPointerDown(pointer);
                    button.gameObject.SetActive(false);
                    button.gameObject.SetActive(true);
                    AssertColor(label.color, normal);
                    button.interactable = false;
                    button.gameObject.SetActive(false);
                    button.gameObject.SetActive(true);
                    AssertColor(label.color, Disabled);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(events);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [UnityTest]
        public IEnumerator InteractableChangesUpdateWithoutPointerMovement()
        {
            GameObject root = Open("PauseMenu");
            try
            {
                var view = root.GetComponent<UGUIView>();
                Button button = view.Get<Button>("ControlMode");
                Text label = view.Get<Text>("ControlModeLabel");
                button.interactable = false;
                EditorApplication.QueuePlayerLoopUpdate();
                yield return null;
                AssertColor(label.color, Disabled);
                button.interactable = true;
                EditorApplication.QueuePlayerLoopUpdate();
                yield return null;
                AssertColor(label.color, Normal);
                var group = root.AddComponent<CanvasGroup>();
                group.interactable = false;
                EditorApplication.QueuePlayerLoopUpdate();
                yield return null;
                AssertColor(label.color, Disabled);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static GameObject Open(string page) => UnityEngine.Object.Instantiate(
            AssetDatabase.LoadAssetAtPath<GameObject>(NativeUiSetup.Root + "/" + page + "/" + page + ".prefab"));

        private static void AssertColor(Color actual, Color expected)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(.0001));
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(.0001));
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(.0001));
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(.0001));
        }
    }
}
