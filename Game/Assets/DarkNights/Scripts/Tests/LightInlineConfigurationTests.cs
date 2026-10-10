using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using DarkNights.Core.Config;
using DarkNights.Editor.Lighting;
using DarkNights.Entry;
using DarkNights.Runtime.Objects;
using DarkNights.View.Lighting;
using GameCore.Objects.Definition;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace DarkNights.Tests
{
    /// <summary>物体光照的继承与自动覆盖合同；验证直接编辑、单项恢复、无变化与整次拖动的原生 Undo，不修改正式 Definition 或启动会话。</summary>
    public sealed class LightInlineConfigurationTests
    {
        private ObjectDefinition definition;
        private LightEffect template;
        private EditorWindow host;

        [SetUp]
        public void SetUp()
        {
            template = AssetDatabase.LoadAssetAtPath<GameObject>(Editor.ReusableLightContentSetup.EffectPath).GetComponent<LightEffect>();
            definition = ScriptableObject.CreateInstance<ObjectDefinition>();
            definition.SharedConfigs.Add(new FlashlightToolConfig());
        }

        [TearDown]
        public void TearDown()
        {
            if (host != null) host.Close();
            Undo.ClearUndo(definition); Object.DestroyImmediate(definition);
        }

        [Test]
        public void MissingProfileUsesBuiltinValuesAndExplicitDefaultTemplate()
        {
            var config = LightProfileResources.RequireConfig(definition);
            Assert.That(LightProfileResources.HasPreset(config), Is.False);
            var snapshot = LightProfileResolver.Resolve(null, config.Overrides, template);
            Assert.That(snapshot.Template, Is.SameAs(template));
            Assert.That(snapshot.Rules.Range, Is.EqualTo(14));
            Assert.That(snapshot.Rules.Intensity, Is.EqualTo(1.35f));
        }

        [Test]
        public void DirectEditWithoutProfileCreatesOverrideAndUndoRestoresInheritance()
        {
            var panel = CreatePanel();
            var indicator = panel.Root.Q<Button>("overrideIntensity");
            var slider = panel.Root.Q<Slider>("Intensity");
            Assert.That(indicator, Is.Not.Null); Assert.That(indicator.enabledSelf, Is.False);
            Assert.That(slider.enabledSelf, Is.True);
            slider.value = 2;
            var config = LightDefinitionEditor.Config(definition);
            Assert.That(config.Overrides.Mask, Is.EqualTo(LightOverrideMask.Intensity));
            Assert.That(indicator.enabledSelf, Is.True);
            Assert.That(indicator.parent.ClassListContains("is-overridden"), Is.True);
            Assert.That(config.Profile.AssetGUID, Is.Empty);
            Assert.That(LightProfileResolver.Resolve(null, config.Overrides, template).Rules.Intensity, Is.EqualTo(2));
            Undo.PerformUndo(); panel.Refresh(); Assert.That(slider.value, Is.EqualTo(1.35f));
            Assert.That(LightDefinitionEditor.Config(definition).Overrides.Mask, Is.EqualTo(LightOverrideMask.None));
            Assert.That(indicator.enabledSelf, Is.False);
            Assert.That(indicator.parent.ClassListContains("is-overridden"), Is.False);
            Assert.That(slider.enabledSelf, Is.True);
            Undo.PerformRedo(); panel.Refresh();
            Assert.That(slider.value, Is.EqualTo(2)); Assert.That(indicator.enabledSelf, Is.True);
        }

        [Test]
        public void InheritedControlsFollowPresetEditsWhileObjectOverridesStayFixed()
        {
            var preset = ScriptableObject.CreateInstance<LightProfile>(); preset.Template = template;
            try
            {
                var values = LightDefinitionEditor.Config(definition).Overrides;
                values.Mask = LightOverrideMask.Intensity; values.Intensity = 2;
                var panel = CreatePanel(preset);
                preset.Range = 20; preset.Intensity = 3;
                panel.Refresh();
                Assert.That(panel.Root.Q<Slider>("Range").value, Is.EqualTo(20));
                Assert.That(panel.Root.Q<Slider>("Range").enabledSelf, Is.True);
                Assert.That(panel.Root.Q<Button>("overrideRange").enabledSelf, Is.False);
                Assert.That(panel.Root.Q<Slider>("Intensity").value, Is.EqualTo(2));
                Assert.That(panel.Root.Q<Slider>("Intensity").enabledSelf, Is.True);
                var effective = LightProfileResolver.Resolve(preset, values);
                Assert.That(effective.Rules.Range, Is.EqualTo(20));
                Assert.That(effective.Rules.Intensity, Is.EqualTo(2));
                Assert.That(preset.Intensity, Is.EqualTo(3));
            }
            finally { Object.DestroyImmediate(preset); }
        }

        [Test]
        public void RevertOneFieldFollowsLatestPresetAndPreservesOtherOverrides()
        {
            var preset = ScriptableObject.CreateInstance<LightProfile>(); preset.Template = template;
            try
            {
                var values = LightDefinitionEditor.Config(definition).Overrides;
                values.Mask = LightOverrideMask.Range | LightOverrideMask.Intensity;
                values.Range = 20; values.Intensity = 2;
                var panel = CreatePanel(preset);
                preset.Range = 18;
                var indicator = panel.Root.Q<Button>("overrideRange");
                using (var evt = NavigationSubmitEvent.GetPooled()) { evt.target = indicator; indicator.SendEvent(evt); }
                Assert.That(LightDefinitionEditor.Config(definition).Overrides.Mask, Is.EqualTo(LightOverrideMask.Intensity));
                Assert.That(panel.Root.Q<Slider>("Range").value, Is.EqualTo(18));
                Assert.That(panel.Root.Q<Slider>("Intensity").value, Is.EqualTo(2));
                preset.Range = 22; panel.Refresh();
                Assert.That(panel.Root.Q<Slider>("Range").value, Is.EqualTo(22));
                Undo.PerformUndo(); panel.Refresh();
                Assert.That(panel.Root.Q<Slider>("Range").value, Is.EqualTo(20));
                Assert.That(panel.Root.Q<Button>("overrideRange").enabledSelf, Is.True);
                Undo.PerformRedo(); panel.Refresh();
                Assert.That(panel.Root.Q<Slider>("Range").value, Is.EqualTo(22));
                Assert.That(panel.Root.Q<Button>("overrideRange").enabledSelf, Is.False);
            }
            finally { Object.DestroyImmediate(preset); }
        }

        [Test]
        public void EntireDirectDragUndoesOverrideAndValueTogether()
        {
            var history = new FlashlightDebugUndo();
            var panel = CreatePanel(history: history);
            var slider = panel.Root.Q<Slider>("Range");
            history.BeginGesture("Range");
            slider.value = 20; slider.value = 22; history.EndGesture();
            Undo.PerformUndo(); panel.Refresh();
            Assert.That(slider.value, Is.EqualTo(14));
            Assert.That(LightDefinitionEditor.Config(definition).Overrides.Mask, Is.EqualTo(LightOverrideMask.None));
            Undo.PerformRedo(); panel.Refresh();
            Assert.That(slider.value, Is.EqualTo(22));
            Assert.That(LightDefinitionEditor.Config(definition).Overrides.Mask, Is.EqualTo(LightOverrideMask.Range));
        }

        [Test]
        public void SameInheritedValueDoesNotCreateOverrideOrDropRedo()
        {
            var history = new FlashlightDebugUndo();
            LightDefinitionEditor.SetValue(definition, "Range", 20, history);
            Undo.PerformUndo();
            LightDefinitionEditor.SetValue(definition, "Range", 14, history);
            Assert.That(LightDefinitionEditor.Config(definition).Overrides.Mask, Is.EqualTo(LightOverrideMask.None));
            Undo.PerformRedo();
            Assert.That(LightDefinitionEditor.Config(definition).Overrides.Range, Is.EqualTo(20));
            Assert.That(LightDefinitionEditor.Config(definition).Overrides.Mask, Is.EqualTo(LightOverrideMask.Range));
        }

        [Test]
        public void ToggleAndColorEditsOverrideOnlyTheirOwnFields()
        {
            var panel = CreatePanel();
            panel.Root.Q<Toggle>("SoftShadows").value = false;
            var color = new Color(.2f, .4f, .6f, 1);
            panel.Root.Q<ColorField>("Color").value = color;
            var values = LightDefinitionEditor.Config(definition).Overrides;
            Assert.That(values.Mask, Is.EqualTo(LightOverrideMask.SoftShadows | LightOverrideMask.Color));
            Assert.That(values.SoftShadows, Is.False); Assert.That(values.Color, Is.EqualTo(color));
            Undo.PerformUndo(); panel.Refresh();
            Assert.That(LightDefinitionEditor.Config(definition).Overrides.Mask, Is.EqualTo(LightOverrideMask.SoftShadows));
            Undo.PerformUndo(); panel.Refresh();
            Assert.That(LightDefinitionEditor.Config(definition).Overrides.Mask, Is.EqualTo(LightOverrideMask.None));
        }

        private LightOverridePanel CreatePanel(LightProfile preset = null, FlashlightDebugUndo history = null)
        {
            host = ScriptableObject.CreateInstance<EditorWindow>();
            host.titleContent = new GUIContent("光照覆盖回归"); host.Show();
            var panel = new LightOverridePanel(definition, preset, false, history ?? new FlashlightDebugUndo());
            host.rootVisualElement.Add(panel.Root);
            Assert.That(panel.Root.panel, Is.Not.Null, "控件事件必须在真实 Editor 面板中分发。");
            return panel;
        }

        [Test]
        public void ClearingProfileRetainsObjectOverrides()
        {
            var config = LightDefinitionEditor.Config(definition);
            config.Profile = new AssetReference("ffffffffffffffffffffffffffffffff");
            config.Overrides.Mask = LightOverrideMask.Range; config.Overrides.Range = 20;
            LightDefinitionEditor.SelectPreset(definition, null);
            config = LightDefinitionEditor.Config(definition);
            Assert.That(config.Profile.AssetGUID, Is.Empty);
            Assert.That(LightProfileResolver.Resolve(null, config.Overrides, template).Rules.Range, Is.EqualTo(20));
            Assert.That(config.Starter, Is.True);
        }

        [Test]
        public void InvalidDisabledValuesAreIgnoredButActiveOverridesAreValidated()
        {
            var values = new LightOverrides { Range = float.NaN };
            Assert.That(LightProfileResolver.Resolve(null, values, template).Rules.Range, Is.EqualTo(14));
            values.Mask = LightOverrideMask.Range;
            Assert.Throws<System.ArgumentException>(() => LightProfileResolver.Resolve(null, values, template));
        }

        [UnityTest]
        public IEnumerator InlineConfigurationPreparesWithoutLoadingAProfile() => UniTask.ToCoroutine(async () =>
        {
            using var resources = await LightProfileResources.Prepare(new[] { definition }, template, CancellationToken.None);
            var first = resources.Resolve(definition);
            Assert.That(first.Template, Is.SameAs(template));
            Assert.That(resources.Resolve(definition), Is.SameAs(first));
            resources.Dispose();
            Assert.Throws<System.ObjectDisposedException>(() => resources.Resolve(definition));
        });
    }
}
