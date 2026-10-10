using System.Collections;
using System.Reflection;
using DarkNights.Editor.Lighting;
using DarkNights.View.Lighting;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace DarkNights.Tests
{
    /// <summary>光照工作台的运行草稿与独立历史回归；检查调参不反写来源，拖动、输入和重建保留临时历史，关闭丢弃未保存草稿。</summary>
    public sealed class FlashlightDebugWindowTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private LightProfile preset;
        private FlashlightDebugWindow window;

        [SetUp]
        public void SetUp()
        {
            preset = ScriptableObject.CreateInstance<LightProfile>();
            preset.Template = AssetDatabase.LoadAssetAtPath<GameObject>(Editor.ReusableLightContentSetup.EffectPath).GetComponent<LightEffect>();
            OpenWindow();
        }

        [TearDown]
        public void TearDown()
        {
            if (window != null) window.Close();
            Undo.ClearUndo(preset); Object.DestroyImmediate(preset);
        }

        [TestCase("Range", 20f)]
        [TestCase("Cone", 120f)]
        [TestCase("ApertureWidth", .75f)]
        [TestCase("Intensity", 2f)]
        [TestCase("Softness", .5f)]
        [TestCase("ConeFeather", .2f)]
        [TestCase("NearRange", 3f)]
        [TestCase("NearIntensity", 1f)]
        [TestCase("FillRadius", 3f)]
        [TestCase("FillIntensity", 1f)]
        public void SliderRestoresDraftAndLeavesSourceUnchanged(string field, float changed)
        {
            var member = typeof(LightProfile).GetField(field);
            float initial = (float)member.GetValue(preset);
            var slider = window.rootVisualElement.Q<Slider>(field);
            slider.value = changed;
            Assert.That(window.Draft.Read(field), Is.EqualTo(changed));
            Assert.That(member.GetValue(preset), Is.EqualTo(initial));
            Command("Undo"); Assert.That(window.Draft.Read(field), Is.EqualTo(initial));
            Assert.That(slider.value, Is.EqualTo(initial));
            Command("Redo"); Assert.That(window.Draft.Read(field), Is.EqualTo(changed));
            Assert.That(member.GetValue(preset), Is.EqualTo(initial));
            Assert.That(slider.value, Is.EqualTo(changed));
        }

        [Test]
        public void NoOpPreservesRedoAndNewEditDropsIt()
        {
            var slider = window.rootVisualElement.Q<Slider>("Range");
            slider.value = 20; Command("Undo"); slider.value = 14;
            Command("Redo"); Assert.That(window.Draft.Read("Range"), Is.EqualTo(20));
            Command("Undo"); slider.value = 8; Command("Redo");
            Assert.That(window.Draft.Read("Range"), Is.EqualTo(8)); Assert.That(preset.Range, Is.EqualTo(14));
        }

        [UnityTest]
        public IEnumerator PointerDragAcrossRefreshesIsOneUndoAndRedo()
        {
            yield return null;
            var slider = window.rootVisualElement.Q<Slider>("Intensity");
            var track = slider.Q("unity-drag-container");
            Vector2 start = slider.Q("unity-dragger").worldBound.center;
            Assert.That(track.worldBound.width, Is.GreaterThan(80));
            Pointer(track, EventType.MouseDown, start);
            Pointer(track, EventType.MouseDrag, start - Vector2.right * 20);
            yield return null;
            Refresh();
            Pointer(track, EventType.MouseDrag, start - Vector2.right * 60);
            Pointer(track, EventType.MouseUp, start - Vector2.right * 60);
            float final = (float)window.Draft.Read("Intensity");
            Assert.That(final, Is.LessThan(1.35f));
            Command("Undo"); Assert.That(window.Draft.Read("Intensity"), Is.EqualTo(1.35f));
            Command("Redo"); Assert.That(window.Draft.Read("Intensity"), Is.EqualTo(final));
            Assert.That(preset.Intensity, Is.EqualTo(1.35f));
        }

        [Test]
        public void FocusedInputUsesCtrlZCtrlYAndCtrlShiftZ()
        {
            var slider = window.rootVisualElement.Q<Slider>("Range");
            slider.value = 18; slider.value = 20;
            var input = slider.Q<TextField>(); Assert.That(input.isDelayed, Is.True);
            input.Focus(); Key(input, KeyCode.Z, EventModifiers.Control);
            Assert.That(window.Draft.Read("Range"), Is.EqualTo(18));
            Key(input, KeyCode.Y, EventModifiers.Control); Assert.That(window.Draft.Read("Range"), Is.EqualTo(20));
            Key(input, KeyCode.Z, EventModifiers.Control);
            Key(input, KeyCode.Z, EventModifiers.Control | EventModifiers.Shift);
            Assert.That(window.Draft.Read("Range"), Is.EqualTo(20)); Assert.That(preset.Range, Is.EqualTo(14));
        }

        [Test]
        public void RecreateGuiKeepsDraftHistoryAndCloseDiscardsIt()
        {
            window.CreateGUI(); window.CreateGUI();
            window.rootVisualElement.Q<Slider>("Range").value = 18;
            window.rootVisualElement.Q<Slider>("Range").value = 20;
            Refresh(); Refresh();
            Command("Undo"); Assert.That(window.Draft.Read("Range"), Is.EqualTo(18));
            window.Close(); window = null;
            Assert.That(preset.Range, Is.EqualTo(14));
            OpenWindow(); Assert.That(window.Draft.Read("Range"), Is.EqualTo(14));
        }

        private void OpenWindow()
        {
            window = ScriptableObject.CreateInstance<FlashlightDebugWindow>();
            typeof(FlashlightDebugWindow).GetField("preset", Private).SetValue(window, preset);
            window.Show(); window.position = new Rect(50, 50, 620, 900); window.CreateGUI();
        }

        private void Refresh() => typeof(FlashlightDebugWindow).GetMethod("Refresh", Private).Invoke(window, null);
        private static void Key(VisualElement target, KeyCode key, EventModifiers modifiers)
        {
            using (var evt = KeyDownEvent.GetPooled('\0', key, modifiers)) { evt.target = target; target.SendEvent(evt); }
        }
        private void Command(string command)
        {
            using (var evt = ExecuteCommandEvent.GetPooled(command)) { evt.target = window.rootVisualElement; window.rootVisualElement.SendEvent(evt); }
        }
        private static void Pointer(VisualElement target, EventType type, Vector2 position)
        {
            var source = new Event { type = type, mousePosition = position, button = 0 };
            if (type == EventType.MouseDown)
            { using (var evt = PointerDownEvent.GetPooled(source)) { evt.target = target; target.SendEvent(evt); } }
            else if (type == EventType.MouseUp)
            { using (var evt = PointerUpEvent.GetPooled(source)) { evt.target = target; target.SendEvent(evt); } }
            else
            { using (var evt = PointerMoveEvent.GetPooled(source)) { evt.target = target; target.SendEvent(evt); } }
        }
    }
}
